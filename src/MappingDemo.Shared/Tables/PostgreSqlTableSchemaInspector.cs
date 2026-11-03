using System.Diagnostics;
using Dapper;
using Npgsql;

namespace MappingDemo.Shared.Tables;

public sealed class PostgreSqlTableSchemaInspector : ITableSchemaInspector
{
    private const string InspectSql = """
        SELECT EXISTS (
            SELECT 1
            FROM pg_catalog.pg_class AS relation
            INNER JOIN pg_catalog.pg_namespace AS namespace
                ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = current_schema()
              AND relation.relname = @TableName
              AND relation.relkind IN ('r', 'p'));

        SELECT
            attribute.attname AS "Name",
            pg_catalog.format_type(
                attribute.atttypid,
                attribute.atttypmod) AS "DataType",
            NOT attribute.attnotnull AS "IsNullable",
            attribute.attidentity <> '' AS "IsIdentity",
            attribute.attnum AS "Ordinal"
        FROM pg_catalog.pg_attribute AS attribute
        INNER JOIN pg_catalog.pg_class AS relation
            ON relation.oid = attribute.attrelid
        INNER JOIN pg_catalog.pg_namespace AS namespace
            ON namespace.oid = relation.relnamespace
        WHERE namespace.nspname = current_schema()
          AND relation.relname = @TableName
          AND relation.relkind IN ('r', 'p')
          AND attribute.attnum > 0
          AND NOT attribute.attisdropped
        ORDER BY attribute.attnum;

        SELECT
            constraint_row.contype::text AS "ConstraintType",
            array_agg(
                attribute.attname::text
                ORDER BY key_column.ordinality) AS "Columns"
        FROM pg_catalog.pg_constraint AS constraint_row
        INNER JOIN pg_catalog.pg_class AS relation
            ON relation.oid = constraint_row.conrelid
        INNER JOIN pg_catalog.pg_namespace AS namespace
            ON namespace.oid = relation.relnamespace
        CROSS JOIN LATERAL unnest(constraint_row.conkey)
            WITH ORDINALITY AS key_column(attnum, ordinality)
        INNER JOIN pg_catalog.pg_attribute AS attribute
            ON attribute.attrelid = relation.oid
           AND attribute.attnum = key_column.attnum
        WHERE namespace.nspname = current_schema()
          AND relation.relname = @TableName
          AND constraint_row.contype IN ('p', 'u')
        GROUP BY constraint_row.oid, constraint_row.contype
        ORDER BY constraint_row.contype, constraint_row.oid;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;

    public PostgreSqlTableSchemaInspector(
        NpgsqlDataSource dataSource,
        TimeProvider? timeProvider = null)
    {
        _dataSource = dataSource;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TableSchemaVerificationResult> InspectAsync(
        long tableId,
        TableDefinition expectedDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tableId);
        ArgumentNullException.ThrowIfNull(expectedDefinition);

        var expected = TablePhysicalSchema.Expected(expectedDefinition);
        var started = Stopwatch.GetTimestamp();

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            var command = new CommandDefinition(
                InspectSql,
                new { TableName = expectedDefinition.Name },
                commandTimeout: 5,
                cancellationToken: cancellationToken);
            using var result = await connection.QueryMultipleAsync(command);
            var exists = await result.ReadSingleAsync<bool>();
            var columns = (await result.ReadAsync<CatalogColumn>()).ToArray();
            var constraints = (await result.ReadAsync<CatalogConstraint>())
                .ToArray();

            if (!exists)
            {
                return Result(
                    TableVerificationStatus.Missing,
                    expected,
                    actual: null,
                    [new(
                        "table",
                        "TableMissing",
                        "The physical table was not found.")],
                    started);
            }

            var actual = Snapshot(columns, constraints);
            var diagnostics = Compare(expected, actual);
            return Result(
                diagnostics.Count == 0
                    ? TableVerificationStatus.Healthy
                    : TableVerificationStatus.Drifted,
                expected,
                actual,
                diagnostics,
                started);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested)
        {
            return Failed(expected, "CatalogTimeout", started);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            return Failed(expected, "CatalogPermissionDenied", started);
        }
        catch (NpgsqlException)
        {
            return Failed(expected, "CatalogUnavailable", started);
        }
        catch (TimeoutException)
        {
            return Failed(expected, "CatalogTimeout", started);
        }
    }

    private TableSchemaVerificationResult Failed(
        TableSchemaSnapshot expected,
        string code,
        long started) =>
        Result(
            TableVerificationStatus.VerificationFailed,
            expected,
            actual: null,
            [new(
                "catalog",
                code,
                "The PostgreSQL catalog could not be inspected safely.")],
            started);

    private TableSchemaVerificationResult Result(
        TableVerificationStatus status,
        TableSchemaSnapshot expected,
        TableSchemaSnapshot? actual,
        IReadOnlyList<TableSchemaDiagnostic> diagnostics,
        long started) =>
        new(
            status,
            expected,
            actual,
            diagnostics,
            _timeProvider.GetUtcNow(),
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    internal static IReadOnlyList<TableSchemaDiagnostic> Compare(
        TableSchemaSnapshot expected,
        TableSchemaSnapshot actual)
    {
        var diagnostics = new List<TableSchemaDiagnostic>();
        var expectedColumns = expected.Columns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);
        var actualColumns = actual.Columns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);

        foreach (var expectedColumn in expected.Columns)
        {
            if (!actualColumns.TryGetValue(expectedColumn.Name, out var actualColumn))
            {
                diagnostics.Add(new(
                    $"columns.{expectedColumn.Name}",
                    "ColumnMissing",
                    "An expected column is missing.",
                    Expected: expectedColumn.DataType));
                continue;
            }

            CompareColumn(expectedColumn, actualColumn, diagnostics);
        }

        foreach (var actualColumn in actual.Columns)
        {
            if (!expectedColumns.ContainsKey(actualColumn.Name))
            {
                diagnostics.Add(new(
                    $"columns.{actualColumn.Name}",
                    "UnexpectedColumn",
                    "An unexpected physical column exists.",
                    Actual: actualColumn.DataType));
            }
        }

        CompareConstraint(
            expected.PrimaryKey,
            actual.PrimaryKey,
            "constraints.primaryKey",
            "PrimaryKeyMismatch",
            diagnostics);
        CompareUniqueConstraints(expected, actual, diagnostics);

        return diagnostics;
    }

    private static void CompareColumn(
        TableSchemaColumn expected,
        TableSchemaColumn actual,
        ICollection<TableSchemaDiagnostic> diagnostics)
    {
        var expectedType = PostgresTypeCanonicalizer.Canonicalize(
            expected.DataType);
        var actualType = PostgresTypeCanonicalizer.Canonicalize(
            actual.DataType);
        if (!string.Equals(expectedType, actualType, StringComparison.Ordinal))
        {
            diagnostics.Add(new(
                $"columns.{expected.Name}.dataType",
                "ColumnTypeMismatch",
                "The physical column type differs from metadata.",
                expectedType,
                actualType));
        }
        if (expected.IsNullable != actual.IsNullable)
        {
            diagnostics.Add(new(
                $"columns.{expected.Name}.isNullable",
                "ColumnNullabilityMismatch",
                "The physical column nullability differs from metadata.",
                expected.IsNullable.ToString(),
                actual.IsNullable.ToString()));
        }
        if (expected.IsIdentity != actual.IsIdentity)
        {
            diagnostics.Add(new(
                $"columns.{expected.Name}.isIdentity",
                "ColumnIdentityMismatch",
                "The physical identity setting differs from metadata.",
                expected.IsIdentity.ToString(),
                actual.IsIdentity.ToString()));
        }
    }

    private static void CompareUniqueConstraints(
        TableSchemaSnapshot expected,
        TableSchemaSnapshot actual,
        ICollection<TableSchemaDiagnostic> diagnostics)
    {
        var expectedKeys = expected.UniqueConstraints
            .Select(ConstraintKey)
            .ToHashSet(StringComparer.Ordinal);
        var actualKeys = actual.UniqueConstraints
            .Select(ConstraintKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in expectedKeys.Except(actualKeys))
        {
            diagnostics.Add(new(
                "constraints.unique",
                "UniqueConstraintMissing",
                "An expected unique constraint is missing.",
                Expected: key));
        }
        foreach (var key in actualKeys.Except(expectedKeys))
        {
            diagnostics.Add(new(
                "constraints.unique",
                "UnexpectedUniqueConstraint",
                "An unexpected unique constraint exists.",
                Actual: key));
        }
    }

    private static void CompareConstraint(
        TableSchemaConstraint expected,
        TableSchemaConstraint actual,
        string field,
        string code,
        ICollection<TableSchemaDiagnostic> diagnostics)
    {
        var expectedKey = ConstraintKey(expected);
        var actualKey = ConstraintKey(actual);
        if (!string.Equals(expectedKey, actualKey, StringComparison.Ordinal))
        {
            diagnostics.Add(new(
                field,
                code,
                "The physical constraint differs from metadata.",
                expectedKey,
                actualKey));
        }
    }

    private static string ConstraintKey(TableSchemaConstraint constraint) =>
        string.Join(",", constraint.Columns);

    private static TableSchemaSnapshot Snapshot(
        IReadOnlyList<CatalogColumn> columns,
        IReadOnlyList<CatalogConstraint> constraints)
    {
        var primaryKey = constraints.SingleOrDefault(
            constraint => constraint.ConstraintType == "p");
        var unique = constraints
            .Where(constraint => constraint.ConstraintType == "u")
            .Select(constraint => new TableSchemaConstraint(
                "Unique",
                ConstraintColumns(constraint)))
            .ToArray();

        return new TableSchemaSnapshot(
            columns.OrderBy(column => column.Ordinal)
                .Select(column => new TableSchemaColumn(
                    column.Name,
                    PostgresTypeCanonicalizer.Canonicalize(column.DataType),
                    column.IsNullable,
                    column.IsIdentity))
                .ToArray(),
            new TableSchemaConstraint(
                "PrimaryKey",
                primaryKey is null
                    ? []
                    : ConstraintColumns(primaryKey)),
            unique);
    }

    private static string[] ConstraintColumns(
        CatalogConstraint constraint) =>
        constraint.Columns.Cast<string>().ToArray();

    private sealed record CatalogColumn(
        string Name,
        string DataType,
        bool IsNullable,
        bool IsIdentity,
        short Ordinal);

    private sealed record CatalogConstraint(
        string ConstraintType,
        Array Columns);
}
