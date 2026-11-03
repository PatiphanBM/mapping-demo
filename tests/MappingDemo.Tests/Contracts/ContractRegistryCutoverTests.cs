using Dapper;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class ContractRegistryCutoverTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Bootstrap_is_idempotent_links_profiles_and_matches_bundled_source()
    {
        await WithDatabaseAsync(async dataSource =>
        {
            var environment = new TestEnvironment(ApiContentRoot());
            var schemas = new ContractSchemaModule();
            var postgres = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource, schemas));
            var bootstrapper = new ContractRegistryBootstrapper(
                postgres,
                dataSource,
                environment,
                schemas);

            await bootstrapper.RunAsync();
            await using var connection =
                await dataSource.OpenConnectionAsync();
            var firstAuditCount = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM contract_audit;");
            await bootstrapper.RunAsync();

            Assert.Equal(4, firstAuditCount);
            Assert.Equal(firstAuditCount,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM contract_audit;"));
            Assert.Equal(2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT count(*) FROM contract_versions
                    WHERE lifecycle = 'Published';
                    """));
            Assert.NotNull(await connection.ExecuteScalarAsync<long?>("""
                SELECT contract_version_id
                FROM api_destination_profile_contracts
                WHERE contract_reference = 'proposal/1';
                """));

            var bundled = new VersionedContractRegistryModule(
                BundledVersionedContractRegistryFactory.Create(environment));
            foreach (var reference in new[] { "proposal/1", "claims/2" })
            {
                var expected = await bundled.ResolveExactAsync(reference);
                var actual = await postgres.ResolveExactAsync(reference);
                Assert.True(expected.IsSuccess);
                Assert.True(actual.IsSuccess);
                Assert.Equal(
                    expected.ContractVersion!.Version.CanonicalSchema,
                    actual.ContractVersion!.Version.CanonicalSchema);
                Assert.Equal(
                    expected.ContractVersion.Version.SchemaHash,
                    actual.ContractVersion.Version.SchemaHash);
                Assert.Equal(
                    expected.ContractVersion.PathCatalog,
                    actual.ContractVersion.PathCatalog);
            }
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Registry_fetcher_rejects_retired_for_new_work_but_exact_history_resolves()
    {
        await WithDatabaseAsync(async dataSource =>
        {
            var environment = new TestEnvironment(ApiContentRoot());
            var schemas = new ContractSchemaModule();
            var registry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource, schemas));
            var bootstrapper = new ContractRegistryBootstrapper(
                registry,
                dataSource,
                environment,
                schemas);
            await bootstrapper.RunAsync();
            var proposal = (await registry.GetAsync("proposal"))
                .Contract!.Versions.Single();

            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await connection.ExecuteAsync("""
                    DELETE FROM api_destination_profile_contracts
                    WHERE contract_reference = 'proposal/1';
                    """);
            }
            var retired = await registry.ExecuteAsync(
                new RetireContractVersionCommand(
                    proposal.ContractVersionId,
                    proposal.ConcurrencyToken,
                    "operator-a",
                    "Retire proposal after profile removal",
                    "retire-cutover-proposal"));
            Assert.True(retired.IsSuccess);
            Assert.True((await registry.ResolveExactAsync("proposal/1"))
                .IsSuccess);

            var fetcher = new RegistryContractSchemaFetcher(registry);
            var error = await Assert.ThrowsAsync<ContractFetchException>(
                async () => await fetcher.FetchAsync(
                    new TestProfile(),
                    new ContractReference("proposal", "1", "proposal/1", ""),
                    CancellationToken.None));
            Assert.Equal("ContractRetired", error.Code);
        });
    }

    private static async Task WithDatabaseAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"contract_cutover_{Guid.NewGuid():N}"[..40];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scoped = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(scoped);
            await using var setup = await dataSource.OpenConnectionAsync();
            await setup.ExecuteAsync("""
                CREATE TABLE mapping_config_versions (id bigint PRIMARY KEY);
                CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);
                """);
            await setup.ExecuteAsync(await MigrationAsync(
                "0014_api_destination_profile_catalog.sql"));
            await InsertLegacyProfileAsync(setup);
            await setup.ExecuteAsync(await MigrationAsync(
                "0016_versioned_contract_registry.sql"));
            await setup.ExecuteAsync(await MigrationAsync(
                "0017_contract_registry_cutover.sql"));
            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task InsertLegacyProfileAsync(
        NpgsqlConnection connection)
    {
        var profileId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profiles (
                profile_key, display_name, created_by, updated_by)
            VALUES ('seed-profile', 'Seed Profile', 'test', 'test')
            RETURNING id;
            """);
        var versionId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profile_versions (
                profile_id, version_no, lifecycle, environment,
                schema_base_url, schema_path_template, delivery_base_url,
                delivery_path_template, method, auth_scheme,
                network_policy_reference, timeout_seconds,
                idempotency_header_name, correlation_header_name,
                allowed_header_names, content_hash, created_by, updated_by)
            VALUES (
                @ProfileId, 1, 'Draft', 'Sandbox',
                'https://contracts.example.test',
                '/schemas/{contractReference}',
                'https://delivery.example.test',
                '/deliveries/{contractVersion}', 'POST', 'None',
                'test-egress', 10, 'Idempotency-Key', 'X-Correlation-Id',
                ARRAY['Idempotency-Key', 'X-Correlation-Id'],
                'sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                'test', 'test')
            RETURNING id;
            """, new { ProfileId = profileId });
        await connection.ExecuteAsync("""
            INSERT INTO api_destination_profile_contracts (
                profile_version_id, ordinal, contract_id,
                contract_version, contract_reference)
            VALUES (@VersionId, 1, 'proposal', '1', 'proposal/1');
            """, new { VersionId = versionId });
    }

    private static async Task<string> MigrationAsync(string fileName)
    {
        var resource = "MappingDemo.Shared.Database.Migrations." + fileName;
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string ApiContentRoot() => Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MappingDemo.Api"));

    private sealed class TestEnvironment(string contentRootPath) :
        IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "MappingDemo.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class TestProfile : IContractProfile
    {
        public string Id => "test";
        public bool Enabled => true;
        public int TimeoutSeconds => 10;
        public IReadOnlyList<SupportedContract> SupportedContracts =>
            [new("proposal", "1", "proposal/1")];
        public IReadOnlyList<PayloadBinding> PayloadBindings => [];
    }
}
