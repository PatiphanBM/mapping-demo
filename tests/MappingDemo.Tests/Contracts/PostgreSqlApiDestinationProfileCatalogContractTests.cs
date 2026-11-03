using Dapper;
using System.Diagnostics;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

[CollectionDefinition(
    PostgreSqlPerformanceCollection.Name,
    DisableParallelization = true)]
public sealed class PostgreSqlPerformanceCollection
{
    public const string Name = "PostgreSQL performance";
}

[Collection(PostgreSqlPerformanceCollection.Name)]
public sealed class PostgreSqlApiDestinationProfileCatalogContractTests
{
    private static readonly string ContentHashA =
        $"sha256:{new string('a', 64)}";

    private static readonly string ContentHashB =
        $"sha256:{new string('b', 64)}";

    private static readonly string ContentHashC =
        $"sha256:{new string('c', 64)}";

    [Fact]
    public async Task Configuration_adapter_follows_catalog_contract()
    {
        var active = Profile(
            "partner-a",
            11,
            1,
            "Active",
            ContentHashA);
        var disabled = Profile(
            "partner-b",
            21,
            1,
            "Disabled",
            ContentHashB);
        var catalog = new ApiDestinationProfileCatalog([active, disabled]);

        await AssertCatalogContractAsync(catalog, 11, 21);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Postgres_adapter_matches_contract_and_shares_activation()
    {
        await WithCatalogAsync(async dataSource =>
        {
            long activeVersionId;
            long disabledVersionId;
            long activeProfileId;
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                activeProfileId = await InsertProfileAsync(
                    connection,
                    "partner-a");
                var disabledProfileId = await InsertProfileAsync(
                    connection,
                    "partner-b");
                activeVersionId = await InsertAndActivateVersionAsync(
                    connection,
                    activeProfileId,
                    1,
                    ContentHashA);
                disabledVersionId = await InsertAndActivateVersionAsync(
                    connection,
                    disabledProfileId,
                    1,
                    ContentHashB);
                await DisableAsync(
                    connection,
                    disabledProfileId,
                    disabledVersionId);
            }

            var catalog = new PostgreSqlApiDestinationProfileCatalog(
                dataSource);
            await AssertCatalogContractAsync(
                catalog,
                activeVersionId,
                disabledVersionId);

            long nextVersionId;
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                nextVersionId = await InsertAndActivateVersionAsync(
                    connection,
                    activeProfileId,
                    2,
                    ContentHashC);
            }

            var apiCatalog = new PostgreSqlApiDestinationProfileCatalog(
                dataSource);
            var workerCatalog = new PostgreSqlApiDestinationProfileCatalog(
                dataSource);
            var apiActive = await apiCatalog.ResolveActiveAsync("partner-a");
            var workerActive = await workerCatalog.ResolveActiveAsync("partner-a");
            var historical = await workerCatalog.ResolvePinnedAsync(
                activeVersionId);

            Assert.Equal(nextVersionId, apiActive.Profile!.ProfileVersionId);
            Assert.Equal(nextVersionId, workerActive.Profile!.ProfileVersionId);
            Assert.Equal(ContentHashC, apiActive.Profile.ContentHash);
            Assert.Equal(ContentHashC, workerActive.Profile.ContentHash);
            Assert.Equal("Disabled", historical.Profile!.Lifecycle);
            Assert.Equal(ContentHashA, historical.Profile.ContentHash);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Active_resolution_p95_is_bounded_under_expected_concurrency()
    {
        await WithCatalogAsync(async dataSource =>
        {
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                var profileId = await InsertProfileAsync(
                    connection,
                    "partner-performance");
                _ = await InsertAndActivateVersionAsync(
                    connection,
                    profileId,
                    1,
                    ContentHashA);
            }

            var catalog = new PostgreSqlApiDestinationProfileCatalog(
                dataSource);
            Assert.True((await catalog.ResolveActiveAsync(
                "partner-performance")).IsSuccess);
            var measurements = await Task.WhenAll(
                Enumerable.Range(0, 32).Select(async _ =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await catalog.ResolveActiveAsync(
                        "partner-performance");
                    stopwatch.Stop();
                    return (result, stopwatch.Elapsed.TotalMilliseconds);
                }));

            Assert.All(measurements, measurement =>
                Assert.True(measurement.result.IsSuccess));
            var ordered = measurements
                .Select(measurement => measurement.TotalMilliseconds)
                .Order()
                .ToArray();
            var p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
            Assert.True(
                p95 <= 250,
                $"Active resolution p95 was {p95:F2} ms; limit is 250 ms.");
        });
    }

    private static async Task AssertCatalogContractAsync(
        IApiDestinationProfileCatalog catalog,
        long activeVersionId,
        long disabledVersionId)
    {
        var safe = await catalog.GetSafeProfilesAsync();
        var active = await catalog.ResolveActiveAsync("partner-a");
        var pinnedActive = await catalog.ResolvePinnedAsync(activeVersionId);
        var disabled = await catalog.ResolveActiveAsync("partner-b");
        var pinnedDisabled = await catalog.ResolvePinnedAsync(
            disabledVersionId);
        var unknown = await catalog.ResolveActiveAsync("missing");

        Assert.Equal("partner-a", Assert.Single(safe).Id);
        Assert.True(active.IsSuccess);
        Assert.Equal(activeVersionId, active.Profile!.ProfileVersionId);
        Assert.True(pinnedActive.IsSuccess);
        Assert.Equal(activeVersionId, pinnedActive.Profile!.ProfileVersionId);
        Assert.False(disabled.IsSuccess);
        Assert.Equal(
            "ProfileDisabled",
            Assert.Single(disabled.Diagnostics).Code);
        Assert.True(pinnedDisabled.IsSuccess);
        Assert.Equal("Disabled", pinnedDisabled.Profile!.Lifecycle);
        Assert.False(unknown.IsSuccess);
        Assert.Equal(
            "UnknownProfile",
            Assert.Single(unknown.Diagnostics).Code);
    }

    private static ApiDestinationProfile Profile(
        string id,
        long profileVersionId,
        int versionNo,
        string lifecycle,
        string contentHash) => new(
        id,
        id,
        lifecycle == "Active",
        "https://contracts.example.test",
        "/schemas/{contractReference}",
        "https://delivery.example.test",
        "/deliveries/{contractVersion}",
        "POST",
        ["Idempotency-Key", "X-Correlation-Id"],
        string.Empty,
        10,
        [new SupportedContract("proposal", "1", "proposal/1")],
        [new PayloadBinding("meta.fileJobId", RuntimeValueSource.FileJobId)],
        ProfileVersionId: profileVersionId,
        VersionNo: versionNo,
        Lifecycle: lifecycle,
        ContentHash: contentHash);

    private static async Task<long> InsertProfileAsync(
        NpgsqlConnection connection,
        string profileKey) =>
        await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profiles (
                profile_key,
                display_name,
                created_by,
                updated_by)
            VALUES (
                @ProfileKey,
                @ProfileKey,
                'integration-test',
                'integration-test')
            RETURNING id;
            """, new { ProfileKey = profileKey });

    private static async Task<long> InsertAndActivateVersionAsync(
        NpgsqlConnection connection,
        long profileId,
        int versionNo,
        string contentHash)
    {
        var versionId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profile_versions (
                profile_id,
                version_no,
                lifecycle,
                environment,
                schema_base_url,
                schema_path_template,
                delivery_base_url,
                delivery_path_template,
                method,
                auth_scheme,
                auth_secret_reference,
                network_policy_reference,
                timeout_seconds,
                idempotency_header_name,
                correlation_header_name,
                allowed_header_names,
                content_hash,
                created_by,
                updated_by)
            VALUES (
                @ProfileId,
                @VersionNo,
                'Draft',
                'Sandbox',
                'https://contracts.example.test',
                '/schemas/{contractReference}',
                'https://delivery.example.test',
                '/deliveries/{contractVersion}',
                'POST',
                'None',
                NULL,
                'test-egress',
                10,
                'Idempotency-Key',
                'X-Correlation-Id',
                ARRAY['Idempotency-Key', 'X-Correlation-Id'],
                @ContentHash,
                'integration-test',
                'integration-test')
            RETURNING id;

            INSERT INTO api_destination_profile_contracts (
                profile_version_id,
                ordinal,
                contract_id,
                contract_version,
                contract_reference)
            VALUES (
                currval(pg_get_serial_sequence(
                    'api_destination_profile_versions',
                    'id')),
                1,
                'proposal',
                '1',
                'proposal/1');

            INSERT INTO api_destination_profile_bindings (
                profile_version_id,
                ordinal,
                target_path,
                runtime_value_source)
            VALUES (
                currval(pg_get_serial_sequence(
                    'api_destination_profile_versions',
                    'id')),
                1,
                'meta.fileJobId',
                'FileJobId');
            """, new
        {
            ProfileId = profileId,
            VersionNo = versionNo,
            ContentHash = contentHash
        });

        await connection.ExecuteAsync("""
            UPDATE api_destination_profile_versions
            SET validated_at = now(),
                validated_content_hash = content_hash
            WHERE id = @VersionId;
            """, new { VersionId = versionId });

        await using var transaction =
            await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            UPDATE api_destination_profile_versions
            SET lifecycle = 'Disabled',
                disabled_at = now(),
                disabled_by = 'integration-test'
            WHERE profile_id = @ProfileId
              AND lifecycle = 'Active';

            UPDATE api_destination_profile_versions
            SET lifecycle = 'Active',
                activated_at = now(),
                activated_by = 'integration-test'
            WHERE id = @VersionId;

            UPDATE api_destination_profiles
            SET active_version_id = @VersionId
            WHERE id = @ProfileId;
            """, new { ProfileId = profileId, VersionId = versionId },
            transaction);
        await transaction.CommitAsync();

        return versionId;
    }

    private static async Task DisableAsync(
        NpgsqlConnection connection,
        long profileId,
        long versionId)
    {
        await using var transaction =
            await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            UPDATE api_destination_profile_versions
            SET lifecycle = 'Disabled',
                disabled_at = now(),
                disabled_by = 'integration-test'
            WHERE id = @VersionId;

            UPDATE api_destination_profiles
            SET active_version_id = NULL
            WHERE id = @ProfileId;
            """, new { ProfileId = profileId, VersionId = versionId },
            transaction);
        await transaction.CommitAsync();
    }

    private static async Task WithCatalogAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"profile_module_{Guid.NewGuid():N}"[..40];
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
                    id bigint PRIMARY KEY);
                CREATE TABLE delivery_jobs (
                    id bigint PRIMARY KEY);
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync());

            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task<string> ReadMigrationAsync()
    {
        const string resourceName =
            "MappingDemo.Shared.Database.Migrations." +
            "0014_api_destination_profile_catalog.sql";
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
