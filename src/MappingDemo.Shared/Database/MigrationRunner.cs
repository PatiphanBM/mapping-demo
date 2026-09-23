using Dapper;
using Npgsql;

namespace MappingDemo.Shared.Database;

public sealed class MigrationRunner
{
    private const long MigrationLockId = 4_627_308_179;

    private const string MigrationResourcePrefix =
        "MappingDemo.Shared.Database.Migrations.";

    private const string AcquireMigrationLockSql = """
        SELECT pg_advisory_lock(@LockId);
        """;

    private const string ReleaseMigrationLockSql = """
        SELECT pg_advisory_unlock(@LockId);
        """;

    private const string CreateHistoryTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version text PRIMARY KEY,
            applied_at timestamptz NOT NULL
        );
        """;

    private const string GetAppliedVersionsSql = """
        SELECT version
        FROM schema_migrations;
        """;

    private const string RecordMigrationSql = """
        INSERT INTO schema_migrations (version, applied_at)
        VALUES (@Version, now());
        """;

    private readonly NpgsqlDataSource _dataSource;

    public MigrationRunner(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public IReadOnlyList<string> GetMigrationResourceNames()
    {
        return typeof(MigrationRunner).Assembly
            .GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var lockParameters = new { LockId = MigrationLockId };
        var acquireLockCommand = new CommandDefinition(
            AcquireMigrationLockSql,
            lockParameters,
            cancellationToken: cancellationToken);
        var lockAcquired = false;

        try
        {
            await connection.ExecuteAsync(acquireLockCommand);
            lockAcquired = true;

            var createHistoryCommand = new CommandDefinition(
                CreateHistoryTableSql,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(createHistoryCommand);

            var getAppliedVersionsCommand = new CommandDefinition(
                GetAppliedVersionsSql,
                cancellationToken: cancellationToken);
            var appliedVersions = (await connection.QueryAsync<string>(
                    getAppliedVersionsCommand))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var resourceName in GetMigrationResourceNames())
            {
                var version = resourceName[MigrationResourcePrefix.Length..];
                if (appliedVersions.Contains(version))
                {
                    continue;
                }

                var migrationSql = await ReadMigrationSqlAsync(
                    resourceName,
                    cancellationToken);

                await using var transaction =
                    await connection.BeginTransactionAsync(cancellationToken);

                var migrationCommand = new CommandDefinition(
                    migrationSql,
                    transaction: transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(migrationCommand);

                var recordMigrationCommand = new CommandDefinition(
                    RecordMigrationSql,
                    new { Version = version },
                    transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(recordMigrationCommand);

                await transaction.CommitAsync(cancellationToken);
                appliedVersions.Add(version);
            }
        }
        finally
        {
            if (lockAcquired)
            {
                var releaseLockCommand = new CommandDefinition(
                    ReleaseMigrationLockSql,
                    lockParameters,
                    cancellationToken: CancellationToken.None);
                await connection.ExecuteAsync(releaseLockCommand);
            }
        }
    }

    private static async Task<string> ReadMigrationSqlAsync(
        string resourceName,
        CancellationToken cancellationToken)
    {
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Migration resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
