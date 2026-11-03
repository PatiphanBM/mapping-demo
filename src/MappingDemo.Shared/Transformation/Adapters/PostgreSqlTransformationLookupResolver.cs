using Dapper;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Shared.Transformation.Adapters;

public sealed class PostgreSqlTransformationLookupResolver(
    NpgsqlDataSource dataSource) : ITransformationLookupResolver
{
    private const string MtiOriginalTable = "mti_original";

    private static readonly string[] PolicyRequiredColumns =
        ["system_id", "cproductname", "cloannumber"];

    private static readonly string[] LoanRequiredColumns =
        ["system_id", "cloannumber"];

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
        var requiredColumns = RequiredColumnsFor(capability);

        await using var connection = await dataSource.OpenConnectionAsync(
            cancellationToken);
        var metadata = await ResolveMetadataAsync(connection, cancellationToken);
        var metadataError = ValidateMetadata(metadata, requiredColumns);
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
                    RequiredColumns = requiredColumns
                },
                cancellationToken: cancellationToken));

        return hasRequiredIndex
            ? new TransformationLookupValidationResult(true, null)
            : new TransformationLookupValidationResult(
                false,
                $"Normalized table '{MtiOriginalTable}' requires a composite " +
                $"index on ({string.Join(", ", requiredColumns)}).");
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

        if (requests.Any(request => !IsSupported(request.Capability)))
        {
            throw new ArgumentException(
                "The batch contains an unsupported lookup capability.",
                nameof(requests));
        }

        await using var connection = await dataSource.OpenConnectionAsync(
            cancellationToken);
        var metadata = await ResolveMetadataAsync(connection, cancellationToken);
        var requiredColumns = requests
            .SelectMany(request => RequiredColumnsFor(request.Capability))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var metadataError = ValidateMetadata(metadata, requiredColumns);
        if (metadataError is not null)
        {
            throw new InvalidOperationException(metadataError);
        }

        var columns = metadata!.Columns.ToDictionary(
            column => column,
            StringComparer.OrdinalIgnoreCase);
        var matches = new List<TransformationLookupMatch>();
        foreach (var group in requests
            .Distinct()
            .GroupBy(request => request.Capability, StringComparer.Ordinal))
        {
            var rows = group.Key switch
            {
                TransformationCapabilities.MtiOriginalPolicyExistsV1 =>
                    await ResolvePolicyAsync(
                        connection,
                        metadata.TableName,
                        columns,
                        group.ToArray(),
                        cancellationToken),
                TransformationCapabilities.MtiOriginalLoanExistsV1 =>
                    await ResolveLoanAsync(
                        connection,
                        metadata.TableName,
                        columns,
                        group.ToArray(),
                        cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Unsupported lookup capability '{group.Key}'.")
            };
            matches.AddRange(rows.Select(row => new TransformationLookupMatch(
                new TransformationLookupRequest(
                    group.Key,
                    row.SystemId,
                    row.ProductName,
                    row.LoanNumber),
                row.SourceRowId)));
        }

        return matches;
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

    private static string? ValidateMetadata(
        LookupMetadata? metadata,
        IReadOnlyCollection<string> requiredColumns)
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

        var missingColumns = requiredColumns
            .Where(required => !metadata.Columns.Contains(
                required,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return missingColumns.Length == 0
            ? null
            : $"Normalized table '{MtiOriginalTable}' is missing required " +
              $"columns: {string.Join(", ", missingColumns)}.";
    }

    private static async Task<IReadOnlyList<LookupRow>> ResolvePolicyAsync(
        NpgsqlConnection connection,
        string tableName,
        IReadOnlyDictionary<string, string> columns,
        IReadOnlyList<TransformationLookupRequest> requests,
        CancellationToken cancellationToken)
    {
        var sql = BuildPolicyBatchSql(
            tableName,
            columns[PolicyRequiredColumns[0]],
            columns[PolicyRequiredColumns[1]],
            columns[PolicyRequiredColumns[2]]);
        return (await connection.QueryAsync<LookupRow>(new CommandDefinition(
            sql,
            new
            {
                SystemIds = requests.Select(request => request.SystemId).ToArray(),
                ProductNames = requests.Select(request => request.ProductName!).ToArray(),
                LoanNumbers = requests.Select(request => request.LoanNumber).ToArray()
            },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static async Task<IReadOnlyList<LookupRow>> ResolveLoanAsync(
        NpgsqlConnection connection,
        string tableName,
        IReadOnlyDictionary<string, string> columns,
        IReadOnlyList<TransformationLookupRequest> requests,
        CancellationToken cancellationToken)
    {
        var sql = BuildLoanBatchSql(
            tableName,
            columns[LoanRequiredColumns[0]],
            columns[LoanRequiredColumns[1]]);
        return (await connection.QueryAsync<LookupRow>(new CommandDefinition(
            sql,
            new
            {
                SystemIds = requests.Select(request => request.SystemId).ToArray(),
                LoanNumbers = requests.Select(request => request.LoanNumber).ToArray()
            },
            cancellationToken: cancellationToken))).ToArray();
    }

    private static string BuildPolicyBatchSql(
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

    private static string BuildLoanBatchSql(
        string tableName,
        string systemIdColumn,
        string loanNumberColumn)
    {
        return $"""
            WITH lookup_keys AS (
                SELECT *
                FROM unnest(
                    @SystemIds::text[],
                    @LoanNumbers::text[])
                    AS keys(system_id, loan_number)
            )
            SELECT
                lookup_keys.system_id AS "SystemId",
                NULL::text AS "ProductName",
                lookup_keys.loan_number AS "LoanNumber",
                target.source_row_id AS "SourceRowId"
            FROM lookup_keys
            INNER JOIN {SqlIdentifier.Quote(tableName)} AS target
                ON target.{SqlIdentifier.Quote(systemIdColumn)} = lookup_keys.system_id
                AND target.{SqlIdentifier.Quote(loanNumberColumn)} = lookup_keys.loan_number;
            """;
    }

    private static void EnsureSupported(string capability)
    {
        if (!IsSupported(capability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                "Unsupported lookup capability.");
        }
    }

    private static bool IsSupported(string capability) =>
        capability is TransformationCapabilities.MtiOriginalPolicyExistsV1 or
            TransformationCapabilities.MtiOriginalLoanExistsV1;

    private static string[] RequiredColumnsFor(string capability)
    {
        return capability switch
        {
            TransformationCapabilities.MtiOriginalPolicyExistsV1 =>
                PolicyRequiredColumns,
            TransformationCapabilities.MtiOriginalLoanExistsV1 =>
                LoanRequiredColumns,
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                "Unsupported lookup capability.")
        };
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
        string? ProductName,
        string LoanNumber,
        long SourceRowId);
}
