using Dapper;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class ContractRegistryMigrationTests
{
    private const string HashA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Migration_preserves_snapshots_and_enforces_identity()
    {
        await WithRegistryAsync(async dataSource =>
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var afterXmin = await connection.ExecuteScalarAsync<string>(
                "SELECT xmin::text FROM mapping_config_versions WHERE id = 1;");
            var beforeXmin = await connection.ExecuteScalarAsync<string>(
                "SELECT version_token FROM migration_baseline WHERE row_id = 1;");
            Assert.Equal(beforeXmin, afterXmin);

            var contractId = await InsertContractAsync(
                connection,
                "proposal");
            _ = await InsertDraftAsync(
                connection,
                contractId,
                "1",
                "proposal/1");

            var badReference = await Assert.ThrowsAsync<PostgresException>(
                async () => await InsertDraftAsync(
                    connection,
                    contractId,
                    "2",
                    "wrong/2"));
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                badReference.SqlState);

            var duplicateDraft = await Assert.ThrowsAsync<PostgresException>(
                async () => await InsertDraftAsync(
                    connection,
                    contractId,
                    "2",
                    "proposal/2"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation,
                duplicateDraft.SqlState);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Published_content_and_audit_are_immutable_and_retire_keeps_hash()
    {
        await WithRegistryAsync(async dataSource =>
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var contractId = await InsertContractAsync(
                connection,
                "claims");
            var versionId = await InsertDraftAsync(
                connection,
                contractId,
                "2",
                "claims/2");
            await PublishAsync(connection, versionId);
            await connection.ExecuteAsync("""
                INSERT INTO contract_audit (
                    contract_id, contract_version_id, action, actor, reason,
                    request_id, before_lifecycle, after_lifecycle,
                    before_schema_hash, after_schema_hash, concurrency_token)
                VALUES (
                    @ContractId, @VersionId, 'Publish', 'operator-a',
                    'Publish tested contract version', 'publish-claims-2',
                    'Draft', 'Published', NULL, @Hash, 2);
                """, new
            {
                ContractId = contractId,
                VersionId = versionId,
                Hash = HashA
            });

            var contentMutation = await Assert.ThrowsAsync<PostgresException>(
                async () => await connection.ExecuteAsync("""
                    UPDATE contract_versions
                    SET canonical_schema = '{"type":"string"}'
                    WHERE id = @VersionId;
                    """, new { VersionId = versionId }));
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                contentMutation.SqlState);

            await connection.ExecuteAsync("""
                UPDATE contract_versions
                SET lifecycle = 'Retired',
                    retired_at = now(),
                    retired_by = 'operator-a',
                    concurrency_token = concurrency_token + 1
                WHERE id = @VersionId;
                """, new { VersionId = versionId });
            var state = await connection.QuerySingleAsync<VersionState>("""
                SELECT lifecycle AS "Lifecycle", schema_hash AS "SchemaHash"
                FROM contract_versions WHERE id = @VersionId;
                """, new { VersionId = versionId });
            Assert.Equal("Retired", state.Lifecycle);
            Assert.Equal(HashA, state.SchemaHash);

            var auditMutation = await Assert.ThrowsAsync<PostgresException>(
                async () => await connection.ExecuteAsync(
                    "DELETE FROM contract_audit WHERE request_id = 'publish-claims-2';"));
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                auditMutation.SqlState);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Profile_link_must_match_published_contract_identity()
    {
        await WithRegistryAsync(async dataSource =>
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var contractId = await InsertContractAsync(
                connection,
                "proposal");
            var contractVersionId = await InsertDraftAsync(
                connection,
                contractId,
                "1",
                "proposal/1");
            await PublishAsync(connection, contractVersionId);
            var profileVersionId = await InsertProfileDraftAsync(connection);

            await connection.ExecuteAsync("""
                INSERT INTO api_destination_profile_contracts (
                    profile_version_id, ordinal, contract_id,
                    contract_version, contract_reference)
                VALUES (
                    @ProfileVersionId, 1, 'proposal', '1', 'proposal/1');
                UPDATE api_destination_profile_versions
                SET validated_at = now(), validated_content_hash = content_hash
                WHERE id = @ProfileVersionId;
                """, new { ProfileVersionId = profileVersionId });
            Assert.Equal(
                contractVersionId,
                await connection.ExecuteScalarAsync<long>("""
                    SELECT contract_version_id
                    FROM api_destination_profile_contracts
                    WHERE profile_version_id = @ProfileVersionId
                      AND ordinal = 1;
                    """, new { ProfileVersionId = profileVersionId }));
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                await connection.ExecuteAsync("""
                    UPDATE api_destination_profile_versions
                    SET lifecycle = 'Active', activated_at = now(),
                        activated_by = 'integration-test'
                    WHERE id = @ProfileVersionId;
                    UPDATE api_destination_profiles
                    SET active_version_id = @ProfileVersionId
                    WHERE id = (
                        SELECT profile_id
                        FROM api_destination_profile_versions
                        WHERE id = @ProfileVersionId);
                    """, new { ProfileVersionId = profileVersionId },
                    transaction);
                await transaction.CommitAsync();
            }
            var mismatch = await Assert.ThrowsAsync<PostgresException>(
                async () => await connection.ExecuteAsync("""
                    UPDATE api_destination_profile_contracts
                    SET contract_version = 'wrong'
                    WHERE profile_version_id = @ProfileVersionId
                      AND ordinal = 1;
                    """, new { ProfileVersionId = profileVersionId, ContractVersionId = contractVersionId }));
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                mismatch.SqlState);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Concurrent_draft_creation_has_one_winner()
    {
        await WithRegistryAsync(async dataSource =>
        {
            await using var setup = await dataSource.OpenConnectionAsync();
            var contractId = await InsertContractAsync(setup, "concurrent");
            await using var first = await dataSource.OpenConnectionAsync();
            await using var second = await dataSource.OpenConnectionAsync();
            await using var firstTransaction =
                await first.BeginTransactionAsync();
            await using var secondTransaction =
                await second.BeginTransactionAsync();

            _ = await first.ExecuteScalarAsync<long>(
                DraftInsertSql,
                DraftParameters(contractId, "1", "concurrent/1"),
                firstTransaction);
            var competing = second.ExecuteScalarAsync<long>(
                DraftInsertSql,
                DraftParameters(contractId, "2", "concurrent/2"),
                secondTransaction);

            await firstTransaction.CommitAsync();
            var conflict = await Assert.ThrowsAsync<PostgresException>(
                async () => await competing);
            Assert.Equal(PostgresErrorCodes.UniqueViolation,
                conflict.SqlState);
            await secondTransaction.RollbackAsync();
        });
    }

    private const string DraftInsertSql = """
        INSERT INTO contract_versions (
            contract_id, version, reference, lifecycle, draft_schema,
            created_by, updated_by)
        VALUES (
            @ContractId, @Version, @Reference, 'Draft', @Schema,
            'integration-test', 'integration-test')
        RETURNING id;
        """;

    private static object DraftParameters(
        long contractId,
        string version,
        string reference) => new
        {
            ContractId = contractId,
            Version = version,
            Reference = reference,
            Schema = "{\"type\":\"object\",\"additionalProperties\":false}"
        };

    private static Task<long> InsertContractAsync(
        NpgsqlConnection connection,
        string key) => connection.ExecuteScalarAsync<long>("""
            INSERT INTO contracts (
                contract_key, display_name, created_by, updated_by)
            VALUES (@Key, @Key, 'integration-test', 'integration-test')
            RETURNING id;
            """, new { Key = key });

    private static Task<long> InsertDraftAsync(
        NpgsqlConnection connection,
        long contractId,
        string version,
        string reference) => connection.ExecuteScalarAsync<long>(
            DraftInsertSql,
            DraftParameters(contractId, version, reference));

    private static Task PublishAsync(
        NpgsqlConnection connection,
        long versionId) => connection.ExecuteAsync("""
            UPDATE contract_versions
            SET lifecycle = 'Published',
                canonical_schema = draft_schema,
                schema_hash = @Hash,
                compatibility = 'Initial',
                published_at = now(),
                published_by = 'operator-a',
                concurrency_token = concurrency_token + 1
            WHERE id = @VersionId;
            """, new { VersionId = versionId, Hash = HashA });

    private static async Task<long> InsertProfileDraftAsync(
        NpgsqlConnection connection)
    {
        var profileId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profiles (
                profile_key, display_name, created_by, updated_by)
            VALUES (
                'contract-link', 'Contract Link', 'integration-test',
                'integration-test')
            RETURNING id;
            """);
        return await connection.ExecuteScalarAsync<long>("""
            INSERT INTO api_destination_profile_versions (
                profile_id, version_no, lifecycle, environment,
                schema_base_url, schema_path_template, delivery_base_url,
                delivery_path_template, method, auth_scheme,
                auth_secret_reference, network_policy_reference,
                timeout_seconds, idempotency_header_name,
                correlation_header_name, allowed_header_names,
                content_hash, created_by, updated_by)
            VALUES (
                @ProfileId, 1, 'Draft', 'Sandbox',
                'https://contracts.example.test',
                '/schemas/{contractReference}',
                'https://delivery.example.test',
                '/deliveries/{contractVersion}', 'POST', 'None', NULL,
                'test-egress', 10, 'Idempotency-Key', 'X-Correlation-Id',
                ARRAY['Idempotency-Key', 'X-Correlation-Id'], @Hash,
                'integration-test', 'integration-test')
            RETURNING id;
            """, new { ProfileId = profileId, Hash = HashA });
    }

    private static async Task WithRegistryAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"contract_registry_{Guid.NewGuid():N}"[..40];
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
                CREATE TABLE mapping_config_versions (
                    id bigint PRIMARY KEY,
                    marker text NOT NULL);
                CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);
                INSERT INTO mapping_config_versions (id, marker)
                VALUES (1, 'keep');
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0014_api_destination_profile_catalog.sql"));
            await setup.ExecuteAsync("""
                CREATE TABLE migration_baseline (
                    row_id bigint PRIMARY KEY,
                    version_token text NOT NULL);
                INSERT INTO migration_baseline (row_id, version_token)
                SELECT id, xmin::text FROM mapping_config_versions;
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0016_versioned_contract_registry.sql"));
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0017_contract_registry_cutover.sql"));
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
            "MappingDemo.Shared.Database.Migrations." + fileName;
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed record VersionState(
        string Lifecycle,
        string SchemaHash);
}
