using Dapper;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Shared.Transformation.Adapters;

public sealed class PostgreSqlTransformationLookupResolver(
    NpgsqlDataSource dataSource) : ITransformationLookupResolver
{
    private const string MtiOriginalTable = "mti_original";

    private static readonly string[] RequiredColumns =
        ["system_id", "cproductname", "cloannumber"];

    private const string ResolveMetadataSql = """
        SELECT
            table_definitions.name AS "TableName",
            table_definitions.kind AS "TableKind",
            table_columns.name AS "ColumnName"
        FROM table_definitions
        INNER JOIN table_columns
            ON table_columns.table_id = table_definitions.id
        WHERE table_definitions.name = @TableName
        ORDER BY table_columns.ordinal;
        """;

    private const string HasRequiredIndexSql = """
        SELECT EXISTS (
            SELECT 1
            FROM pg_catalog.pg_index AS index_definition
            INNER JOIN pg_catalog.pg_class AS table_definition
                ON table_definition.oid = index_definition.indrelid
            INNER JOIN pg_catalog.pg_namespace AS table_namespace
                ON table_namespace.oid = table_definition.relnamespace
            CROSS JOIN LATERAL unnest(index_definition.indkey)
                WITH ORDINALITY AS index_key(attribute_number, ordinal)
            INNER JOIN pg_catalog.pg_attribute AS column_definition
                ON column_definition.attrelid = table_definition.oid
                AND column_definition.attnum = index_key.attribute_number
            WHERE table_namespace.nspname = current_schema()
                AND table_definition.relname = @TableName
                AND index_definition.indisvalid
                AND index_definition.indisready
                AND index_definition.indnkeyatts = cardinality(@RequiredColumns::text[])
                AND index_key.ordinal <= index_definition.indnkeyatts
            GROUP BY index_definition.indexrelid
            HAVING array_agg(
                column_definition.attname::text
                ORDER BY index_key.ordinal) = @RequiredColumns::text[]
        );
        """;

    public async ValueTask<TransformationLookupValidationResult> ValidateAsync(
        string capability,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(capability);

        await using var connection = await dataSource.OpenConnectionAsync(
            cancellationToken);
        var metadata = await ResolveMetadataAsync(connection, cancellationToken);
        var metadataError = ValidateMetadata(metadata);
        if (metadataError is not null)
        {
            return new TransformationLookupValidationResult(false, metadataError);
        }

        var hasRequiredIndex = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                HasRequiredIndexSql,
                new
                {
                    TableName = metadata!.TableName,
                    RequiredColumns
                },
                cancellationToken: cancellationToken));

        return hasRequiredIndex
            ? new TransformationLookupValidationResult(true, null)
            : new TransformationLookupValidationResult(
                false,
                $"Normalized table '{MtiOriginalTable}' requires a composite " +
                $"index on ({string.Join(", ", RequiredColumns)}).");
    }

    public async ValueTask<IReadOnlyList<TransformationLookupMatch>> ResolveAsync(
        IReadOnlyList<TransformationLookupRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return [];
        }

        if (requests.Any(request => !string.Equals(
            request.Capability,
            TransformationCapabilities.MtiOriginalPolicyExistsV1,
            StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "The batch contains an unsupported lookup capability.",
                nameof(requests));
        }

        await using var connection = await dataSource.OpenConnectionAsync(
            cancellationToken);
        var metadata = await ResolveMetadataAsync(connection, cancellationToken);
        var metadataError = ValidateMetadata(metadata);
        if (metadataError is not null)
        {
            throw new InvalidOperationException(metadataError);
        }

        var columns = metadata!.Columns.ToDictionary(
            column => column,
            StringComparer.OrdinalIgnoreCase);
        var sql = BuildBatchSql(
            metadata.TableName,
            columns[RequiredColumns[0]],
            columns[RequiredColumns[1]],
            columns[RequiredColumns[2]]);
        var distinctRequests = requests.Distinct().ToArray();
        var rows = await connection.QueryAsync<LookupRow>(
            new CommandDefinition(
                sql,
                new
                {
                    SystemIds = distinctRequests
                        .Select(request => request.SystemId)
                        .ToArray(),
                    ProductNames = distinctRequests
                        .Select(request => request.ProductName)
                        .ToArray(),
                    LoanNumbers = distinctRequests
                        .Select(request => request.LoanNumber)
                        .ToArray()
                },
                cancellationToken: cancellationToken));

        return rows.Select(row => new TransformationLookupMatch(
            new TransformationLookupRequest(
                TransformationCapabilities.MtiOriginalPolicyExistsV1,
                row.SystemId,
                row.ProductName,
                row.LoanNumber),
            row.SourceRowId)).ToArray();
    }

    private static async Task<LookupMetadata?> ResolveMetadataAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<MetadataRow>(
            new CommandDefinition(
                ResolveMetadataSql,
                new { TableName = MtiOriginalTable },
                cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            return null;
        }

        return new LookupMetadata(
            rows[0].TableName,
            rows[0].TableKind,
            rows.Select(row => row.ColumnName).ToArray());
    }

    private static string? ValidateMetadata(LookupMetadata? metadata)
    {
        if (metadata is null)
        {
            return $"Normalized table '{MtiOriginalTable}' was not found in trusted metadata.";
        }

        if (!string.Equals(
            metadata.TableKind,
            TableKind.Normalized.ToString(),
            StringComparison.Ordinal))
        {
            return $"Table '{MtiOriginalTable}' must be a Normalized table.";
        }

        var missingColumns = RequiredColumns
            .Where(required => !metadata.Columns.Contains(
                required,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return missingColumns.Length == 0
            ? null
            : $"Normalized table '{MtiOriginalTable}' is missing required " +
              $"columns: {string.Join(", ", missingColumns)}.";
    }

    private static string BuildBatchSql(
        string tableName,
        string systemIdColumn,
        string productNameColumn,
        string loanNumberColumn)
    {
        return $"""
            WITH lookup_keys AS (
                SELECT *
                FROM unnest(
                    @SystemIds::text[],
                    @ProductNames::text[],
                    @LoanNumbers::text[])
                    AS keys(system_id, product_name, loan_number)
            )
            SELECT
                lookup_keys.system_id AS "SystemId",
                lookup_keys.product_name AS "ProductName",
                lookup_keys.loan_number AS "LoanNumber",
                target.source_row_id AS "SourceRowId"
            FROM lookup_keys
            INNER JOIN {SqlIdentifier.Quote(tableName)} AS target
                ON target.{SqlIdentifier.Quote(systemIdColumn)} = lookup_keys.system_id
                AND target.{SqlIdentifier.Quote(productNameColumn)} = lookup_keys.product_name
                AND target.{SqlIdentifier.Quote(loanNumberColumn)} = lookup_keys.loan_number;
            """;
    }

    private static void EnsureSupported(string capability)
    {
        if (!string.Equals(
            capability,
            TransformationCapabilities.MtiOriginalPolicyExistsV1,
            StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                "Unsupported lookup capability.");
        }
    }

    private sealed record MetadataRow(
        string TableName,
        string TableKind,
        string ColumnName);

    private sealed record LookupMetadata(
        string TableName,
        string TableKind,
        IReadOnlyList<string> Columns);

    private sealed record LookupRow(
        string SystemId,
        string ProductName,
        string LoanNumber,
        long SourceRowId);
}
