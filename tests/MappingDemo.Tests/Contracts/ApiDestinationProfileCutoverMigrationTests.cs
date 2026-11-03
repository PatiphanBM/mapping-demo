using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileCutoverMigrationTests
{
    [Fact]
    public async Task Cutover_migration_seeds_backfills_and_guards_drift()
    {
        var sql = await ReadMigrationAsync(
            "0015_api_destination_profile_cutover.sql");

        Assert.Contains("'pa-mtls'", sql);
        Assert.Contains("'pa-mtls-inform'", sql);
        Assert.Contains("'claims-envelope'", sql);
        Assert.Contains("api_destination_profile_version_id", sql);
        Assert.Contains("destination_profile_version_id", sql);
        Assert.Contains("unmapped API Destination Profile key", sql);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Cutover_is_idempotent_and_preserves_exact_pins()
    {
        await WithCutoverAsync(async dataSource =>
        {
            var migration = await ReadMigrationAsync(
                "0015_api_destination_profile_cutover.sql");
            await using var connection =
                await dataSource.OpenConnectionAsync();

            var firstPins = (await connection.QueryAsync<PinRow>("""
                SELECT
                    mapping.api_destination_profile_id AS "ProfileKey",
                    mapping.api_destination_profile_version_id AS "MappingPin",
                    delivery.destination_profile_version_id AS "DeliveryPin"
                FROM mapping_config_versions AS mapping
                INNER JOIN delivery_jobs AS delivery
                    ON delivery.id = mapping.id
                ORDER BY mapping.id;
                """)).ToArray();

            await connection.ExecuteAsync(migration);

            var secondPins = (await connection.QueryAsync<PinRow>("""
                SELECT
                    mapping.api_destination_profile_id AS "ProfileKey",
                    mapping.api_destination_profile_version_id AS "MappingPin",
                    delivery.destination_profile_version_id AS "DeliveryPin"
                FROM mapping_config_versions AS mapping
                INNER JOIN delivery_jobs AS delivery
                    ON delivery.id = mapping.id
                ORDER BY mapping.id;
                """)).ToArray();
            Assert.Equal(firstPins, secondPins);
            Assert.All(secondPins, pin =>
            {
                Assert.NotNull(pin.MappingPin);
                Assert.Equal(pin.MappingPin, pin.DeliveryPin);
            });
            Assert.Equal(3, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM api_destination_profiles;"));
            Assert.Equal(3, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM api_destination_profile_versions;"));
            Assert.Equal(3, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM api_destination_profile_audit;"));

            var catalog = new PostgreSqlApiDestinationProfileCatalog(dataSource);
            var profiles = await catalog.GetSafeProfilesAsync();
            Assert.Equal(3, profiles.Count);
            foreach (var metadata in profiles)
            {
                var result = await catalog.ResolvePinnedAsync(
                    metadata.ProfileVersionId);
                Assert.True(result.IsSuccess);
                Assert.Equal(
                    result.Profile!.ContentHash,
                    ApiDestinationProfileCanonicalizer.ComputeContentHash(
                        result.Profile));
            }
        });
    }

    private static async Task WithCutoverAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"profile_cutover_{Guid.NewGuid():N}"[..40];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                scopedConnectionString);
            await using var setup = await dataSource.OpenConnectionAsync();
            await setup.ExecuteAsync("""
                CREATE TABLE mapping_config_versions (
                    id bigint PRIMARY KEY,
                    api_destination_profile_id text);
                CREATE TABLE delivery_jobs (
                    id bigint PRIMARY KEY,
                    destination_profile_id text);

                INSERT INTO mapping_config_versions (
                    id,
                    api_destination_profile_id)
                VALUES
                    (1, 'pa-mtls'),
                    (2, 'pa-mtls-inform'),
                    (3, 'claims-envelope');
                INSERT INTO delivery_jobs (
                    id,
                    destination_profile_id)
                VALUES
                    (1, 'pa-mtls'),
                    (2, 'pa-mtls-inform'),
                    (3, 'claims-envelope');
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0014_api_destination_profile_catalog.sql"));
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0015_api_destination_profile_cutover.sql"));

            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task<string> ReadMigrationAsync(string fileName)
    {
        var resourceName =
            $"MappingDemo.Shared.Database.Migrations.{fileName}";
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed record PinRow(
        string ProfileKey,
        long? MappingPin,
        long? DeliveryPin);
}
