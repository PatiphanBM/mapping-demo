using System.Collections.Concurrent;
using System.Data.Common;
using Dapper;

namespace MappingDemo.Shared.DatabaseDestinations;

public interface IDatabaseTargetSchemaInspector
{
    Task<IReadOnlyList<TargetTableSummary>> ListTablesAsync(
        string profileId,
        string schema,
        CancellationToken cancellationToken = default);

    Task<TargetTableSchema> InspectAsync(
        TargetTableReference target,
        bool refresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseTargetSchemaInspector(
    IDatabaseDestinationProfileCatalog profiles,
    IDatabaseDestinationConnectionFactory connections)
    : IDatabaseTargetSchemaInspector
{
    private const int MaximumCacheEntries = 256;

    private const string PostgreSqlTablesSql = """
        SELECT
            table_schema AS "Schema",
            table_name AS "Table"
        FROM information_schema.tables
        WHERE table_type = 'BASE TABLE'
          AND table_schema = @Schema
        ORDER BY table_name;
        """;

    private const string PostgreSqlColumnsSql = """
        SELECT
            c.column_name AS "Name",
            COALESCE(NULLIF(c.udt_name, ''), c.data_type) AS "ProviderType",
            c.ordinal_position AS "Ordinal",
            (c.is_nullable = 'YES') AS "IsNullable",
            (c.column_default IS NOT NULL) AS "HasDefault",
            (c.is_identity = 'YES') AS "IsIdentity",
            (c.is_generated <> 'NEVER') AS "IsGenerated",
            false AS "IsComputed",
            CASE
                WHEN c.is_identity = 'YES' OR c.is_generated <> 'NEVER'
                    THEN false
                ELSE true
            END AS "IsWritable",
            c.character_maximum_length AS "Length",
            c.numeric_precision AS "Precision",
            c.numeric_scale AS "Scale"
        FROM information_schema.columns c
        INNER JOIN pg_catalog.pg_namespace n
            ON n.nspname = c.table_schema
        INNER JOIN pg_catalog.pg_class t
            ON t.relnamespace = n.oid
           AND t.relname = c.table_name
           AND t.relkind IN ('r', 'p')
        WHERE c.table_schema = @Schema
          AND c.table_name = @Table
        ORDER BY c.ordinal_position;
        """;

    private const string PostgreSqlUniqueConstraintsSql = """
        SELECT
            constraint_rows.constraint_name AS "Name",
            constraint_rows.column_name AS "Column",
            constraint_rows.ordinal AS "Ordinal"
        FROM (
            SELECT
                con.conname AS constraint_name,
                att.attname AS column_name,
                key_column.ordinality AS ordinal
            FROM pg_catalog.pg_constraint con
            INNER JOIN pg_catalog.pg_class rel ON rel.oid = con.conrelid
            INNER JOIN pg_catalog.pg_namespace n ON n.oid = rel.relnamespace
            CROSS JOIN LATERAL unnest(con.conkey)
                WITH ORDINALITY AS key_column(attnum, ordinality)
            INNER JOIN pg_catalog.pg_attribute att
                ON att.attrelid = rel.oid
               AND att.attnum = key_column.attnum
            WHERE con.contype IN ('p', 'u')
              AND n.nspname = @Schema
              AND rel.relname = @Table
        ) constraint_rows
        ORDER BY constraint_rows.constraint_name, constraint_rows.ordinal;
        """;

    private const string SqlServerTablesSql = """
        SELECT
            s.name AS [Schema],
            t.name AS [Table]
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @Schema
          AND t.is_ms_shipped = 0
        ORDER BY t.name;
        """;

    private const string SqlServerColumnsSql = """
        SELECT
            c.name AS [Name],
            ty.name AS [ProviderType],
            c.column_id AS [Ordinal],
            CONVERT(bit, c.is_nullable) AS [IsNullable],
            CONVERT(bit, CASE WHEN dc.object_id IS NULL THEN 0 ELSE 1 END)
                AS [HasDefault],
            CONVERT(bit, c.is_identity) AS [IsIdentity],
            CONVERT(bit, c.is_computed) AS [IsGenerated],
            CONVERT(bit, c.is_computed) AS [IsComputed],
            CONVERT(bit, CASE
                WHEN c.is_identity = 1 OR c.is_computed = 1 OR
                     ty.name IN ('timestamp', 'rowversion') THEN 0
                ELSE 1 END) AS [IsWritable],
            CASE
                WHEN c.max_length < 0 THEN NULL
                WHEN ty.name IN ('nchar', 'nvarchar') THEN c.max_length / 2
                ELSE c.max_length
            END AS [Length],
            NULLIF(CONVERT(int, c.precision), 0) AS [Precision],
            NULLIF(CONVERT(int, c.scale), 0) AS [Scale]
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN sys.columns c ON c.object_id = t.object_id
        INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.default_constraints dc
            ON dc.parent_object_id = c.object_id
           AND dc.parent_column_id = c.column_id
        WHERE s.name = @Schema
          AND t.name = @Table
          AND t.is_ms_shipped = 0
        ORDER BY c.column_id;
        """;

    private const string SqlServerUniqueConstraintsSql = """
        SELECT
            i.name AS [Name],
            c.name AS [Column],
            ic.key_ordinal AS [Ordinal]
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN sys.indexes i
            ON i.object_id = t.object_id
           AND i.is_unique = 1
           AND i.is_hypothetical = 0
           AND i.is_disabled = 0
           AND i.has_filter = 0
        INNER JOIN sys.index_columns ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
           AND ic.key_ordinal > 0
        INNER JOIN sys.columns c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE s.name = @Schema
          AND t.name = @Table
          AND t.is_ms_shipped = 0
        ORDER BY i.name, ic.key_ordinal;
        """;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public async Task<IReadOnlyList<TargetTableSummary>> ListTablesAsync(
        string profileId,
        string schema,
        CancellationToken cancellationToken = default)
    {
        var profile = profiles.GetRequired(profileId);
        ValidateAllowedSchema(profile, schema);
        var sql = profile.Provider == DatabaseProvider.PostgreSql
            ? PostgreSqlTablesSql
            : SqlServerTablesSql;
        try
        {
            await using var connection = connections.CreateConnection(profileId);
            await connection.OpenAsync(cancellationToken);
            var rows = await connection.QueryAsync<TableRow>(new CommandDefinition(
                sql,
                new { Schema = schema },
                commandTimeout: profile.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));
            return rows.Select(row => new TargetTableSummary(
                    profile.Id,
                    profile.Provider,
                    row.Schema,
                    row.Table))
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbException)
        {
            throw DatabaseSchemaInspectionException.Unavailable(profile.Id);
        }
        catch (TimeoutException)
        {
            throw DatabaseSchemaInspectionException.Unavailable(profile.Id);
        }
    }

    public async Task<TargetTableSchema> InspectAsync(
        TargetTableReference target,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var profile = profiles.GetRequired(target.ProfileId);
        ValidateAllowedSchema(profile, target.Schema);
        ValidateIdentifier(target.Table, nameof(target.Table));
        var cacheKey = string.Join(
            '|',
            profile.ConfigurationHash,
            target.Schema,
            target.Table);
        if (!refresh && TryGetCached(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            await using var connection = connections.CreateConnection(profile.Id);
            await connection.OpenAsync(cancellationToken);
            var columns = await ReadColumnsAsync(
                connection,
                profile,
                target,
                cancellationToken);
            if (columns.Count == 0)
            {
                throw new DatabaseSchemaInspectionException(
                    "TargetTableNotFound",
                    "The target table was not found in an allowed schema.");
            }

            var constraints = await ReadUniqueConstraintsAsync(
                connection,
                profile,
                target,
                cancellationToken);
            var schema = TargetTableSchema.Create(
                profile.Id,
                profile.Provider,
                target.Schema,
                target.Table,
                columns,
                constraints);
            Store(cacheKey, schema, profile.SchemaCacheSeconds);
            return schema;
        }
        catch (DatabaseSchemaInspectionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbException)
        {
            throw DatabaseSchemaInspectionException.Unavailable(profile.Id);
        }
        catch (TimeoutException)
        {
            throw DatabaseSchemaInspectionException.Unavailable(profile.Id);
        }
    }

    private static async Task<IReadOnlyList<TargetColumnSchema>> ReadColumnsAsync(
        DbConnection connection,
        DatabaseDestinationProfile profile,
        TargetTableReference target,
        CancellationToken cancellationToken)
    {
        var sql = profile.Provider == DatabaseProvider.PostgreSql
            ? PostgreSqlColumnsSql
            : SqlServerColumnsSql;
        var rows = await connection.QueryAsync<ColumnRow>(new CommandDefinition(
            sql,
            new { target.Schema, target.Table },
            commandTimeout: profile.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
        return rows.Select(row => new TargetColumnSchema(
                row.Name,
                row.ProviderType,
                DatabaseTargetTypeCanonicalizer.Canonicalize(
                    profile.Provider,
                    row.ProviderType),
                row.Ordinal,
                row.IsNullable,
                row.HasDefault,
                row.IsIdentity,
                row.IsGenerated,
                row.IsComputed,
                row.IsWritable,
                row.Length,
                row.Precision,
                row.Scale))
            .ToArray();
    }

    private static async Task<IReadOnlyList<TargetUniqueConstraint>>
        ReadUniqueConstraintsAsync(
            DbConnection connection,
            DatabaseDestinationProfile profile,
            TargetTableReference target,
            CancellationToken cancellationToken)
    {
        var sql = profile.Provider == DatabaseProvider.PostgreSql
            ? PostgreSqlUniqueConstraintsSql
            : SqlServerUniqueConstraintsSql;
        var rows = await connection.QueryAsync<UniqueColumnRow>(
            new CommandDefinition(
                sql,
                new { target.Schema, target.Table },
                commandTimeout: profile.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));
        return rows.GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group => new TargetUniqueConstraint(
                group.Key,
                group.OrderBy(row => row.Ordinal)
                    .Select(row => row.Column)
                    .ToArray()))
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateAllowedSchema(
        DatabaseDestinationProfile profile,
        string schema)
    {
        ValidateIdentifier(schema, nameof(schema));
        var comparison = profile.Provider == DatabaseProvider.SqlServer
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (!profile.AllowedSchemas.Contains(schema, comparison))
        {
            throw new DatabaseSchemaInspectionException(
                "SchemaNotAllowed",
                "The target schema is not allowed by the selected profile.");
        }
    }

    private static void ValidateIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128 ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Target identifiers must contain 1 to 128 non-control characters.",
                name);
        }
    }

    private bool TryGetCached(string key, out TargetTableSchema schema)
    {
        if (_cache.TryGetValue(key, out var entry) &&
            entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            schema = entry.Schema;
            return true;
        }

        _cache.TryRemove(key, out _);
        schema = null!;
        return false;
    }

    private void Store(string key, TargetTableSchema schema, int cacheSeconds)
    {
        if (cacheSeconds <= 0)
        {
            return;
        }

        if (_cache.Count >= MaximumCacheEntries)
        {
            var oldest = _cache.OrderBy(pair => pair.Value.ExpiresAt).FirstOrDefault();
            if (!string.IsNullOrEmpty(oldest.Key))
            {
                _cache.TryRemove(oldest.Key, out _);
            }
        }

        _cache[key] = new CacheEntry(
            schema,
            DateTimeOffset.UtcNow.AddSeconds(cacheSeconds));
    }

    private sealed record CacheEntry(
        TargetTableSchema Schema,
        DateTimeOffset ExpiresAt);

    private sealed class TableRow
    {
        public string Schema { get; init; } = string.Empty;
        public string Table { get; init; } = string.Empty;
    }

    private sealed class ColumnRow
    {
        public string Name { get; init; } = string.Empty;
        public string ProviderType { get; init; } = string.Empty;
        public int Ordinal { get; init; }
        public bool IsNullable { get; init; }
        public bool HasDefault { get; init; }
        public bool IsIdentity { get; init; }
        public bool IsGenerated { get; init; }
        public bool IsComputed { get; init; }
        public bool IsWritable { get; init; }
        public int? Length { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
    }

    private sealed class UniqueColumnRow
    {
        public string Name { get; init; } = string.Empty;
        public string Column { get; init; } = string.Empty;
        public int Ordinal { get; init; }
    }
}

public sealed class DatabaseSchemaInspectionException : Exception
{
    public DatabaseSchemaInspectionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }

    internal static DatabaseSchemaInspectionException Unavailable(
        string profileId) =>
        new(
            "TargetSchemaUnavailable",
            $"Target schema inspection is unavailable for profile '{profileId}'.");
}
