using Dapper;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileCatalogMigrationTests
{
    private const string InsertVersionSql = """
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
        """;

    private static readonly string ContentHashA =
        $"sha256:{new string('a', 64)}";

    private static readonly string ContentHashB =
        $"sha256:{new string('b', 64)}";

    [Fact]
    public async Task Migration_defines_expand_only_versioned_catalog()
    {
        var sql = await ReadMigrationAsync();

        Assert.Contains("CREATE TABLE api_destination_profiles", sql);
        Assert.Contains("CREATE TABLE api_destination_profile_versions", sql);
        Assert.Contains("CREATE TABLE api_destination_profile_contracts", sql);
        Assert.Contains("CREATE TABLE api_destination_profile_bindings", sql);
        Assert.Contains("CREATE TABLE api_destination_profile_audit", sql);
        Assert.Contains("WHERE lifecycle = 'Draft'", sql);
        Assert.Contains("WHERE lifecycle = 'Active'", sql);
        Assert.Contains("DEFERRABLE INITIALLY DEFERRED", sql);
        Assert.Contains("audit_append_only", sql);
        Assert.Contains("api_destination_profile_version_id", sql);
        Assert.Contains("destination_profile_version_id", sql);
        Assert.DoesNotContain(
            "UPDATE mapping_config_versions",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "UPDATE delivery_jobs",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Migration_preserves_existing_work_and_old_writers()
    {
        await WithCatalogAsync(async dataSource =>
        {
            await using var connection =
                await dataSource.OpenConnectionAsync();

            var rows = (await connection.QueryAsync<LegacyRow>("""
                SELECT
                    'mapping' AS "Kind",
                    id AS "Id",
                    baseline.version_token AS "BeforeVersionToken",
                    mapping_config_versions.xmin::text AS "VersionToken",
                    marker AS "Marker",
                    api_destination_profile_id AS "ProfileKey",
                    api_destination_profile_version_id AS "ProfileVersionId"
                FROM mapping_config_versions
                INNER JOIN migration_baseline AS baseline
                    ON baseline.kind = 'mapping'
                   AND baseline.row_id = mapping_config_versions.id
                UNION ALL
                SELECT
                    'delivery' AS "Kind",
                    id AS "Id",
                    baseline.version_token AS "BeforeVersionToken",
                    delivery_jobs.xmin::text AS "VersionToken",
                    marker AS "Marker",
                    destination_profile_id AS "ProfileKey",
                    destination_profile_version_id AS "ProfileVersionId"
                FROM delivery_jobs
                INNER JOIN migration_baseline AS baseline
                    ON baseline.kind = 'delivery'
                   AND baseline.row_id = delivery_jobs.id
                ORDER BY "Kind";
                """)).ToArray();

            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.Equal("keep", row.Marker));
            Assert.All(rows, row => Assert.Equal("pa-mtls", row.ProfileKey));
            Assert.All(rows, row => Assert.Null(row.ProfileVersionId));
            Assert.All(rows, row =>
                Assert.Equal(row.BeforeVersionToken, row.VersionToken));

            await connection.ExecuteAsync("""
                INSERT INTO mapping_config_versions (
                    id, api_destination_profile_id, marker)
                VALUES (2, 'pa-mtls-inform', 'old-writer');
                INSERT INTO delivery_jobs (
                    id, destination_profile_id, marker)
                VALUES (2, 'pa-mtls-inform', 'old-writer');
                """);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Catalog_enforces_lifecycle_pointer_immutability_and_audit()
    {
        await WithCatalogAsync(async dataSource =>
        {
            var profileId = await InsertProfileAsync(dataSource, "partner-a");
            var versionId = await InsertVersionAsync(
                dataSource,
                profileId,
                1,
                ContentHashA);

            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await connection.ExecuteAsync("""
                    INSERT INTO api_destination_profile_contracts (
                        profile_version_id,
                        ordinal,
                        contract_id,
                        contract_version,
                        contract_reference)
                    VALUES (@VersionId, 1, 'proposal', '1', 'proposal/1');

                    INSERT INTO api_destination_profile_bindings (
                        profile_version_id,
                        ordinal,
                        target_path,
                        runtime_value_source)
                    VALUES (@VersionId, 1, 'meta.fileJobId', 'FileJobId');

                    UPDATE api_destination_profile_versions
                    SET validated_at = now(),
                        validated_content_hash = content_hash
                    WHERE id = @VersionId;
                    """, new { VersionId = versionId });

                await using var transaction =
                    await connection.BeginTransactionAsync();
                await connection.ExecuteAsync("""
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
            }

            var immutable = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profile_versions
                SET delivery_base_url = 'https://changed.example.test'
                WHERE id = @VersionId;
                """,
                new { VersionId = versionId });
            Assert.Equal(PostgresErrorCodes.CheckViolation, immutable.SqlState);

            var immutableChild = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profile_bindings
                SET target_path = 'meta.changed'
                WHERE profile_version_id = @VersionId;
                """,
                new { VersionId = versionId });
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                immutableChild.SqlState);

            var secondVersionId = await InsertVersionAsync(
                dataSource,
                profileId,
                2,
                ContentHashB);
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await connection.ExecuteAsync("""
                    UPDATE api_destination_profile_versions
                    SET validated_at = now(),
                        validated_content_hash = content_hash
                    WHERE id = @VersionId;
                    """, new { VersionId = secondVersionId });
            }

            var secondActive = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profile_versions
                SET lifecycle = 'Active',
                    activated_at = now(),
                    activated_by = 'integration-test'
                WHERE id = @VersionId;
                """,
                new { VersionId = secondVersionId });
            Assert.Equal(PostgresErrorCodes.UniqueViolation, secondActive.SqlState);

            await using (var connection =
                await dataSource.OpenConnectionAsync())
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

                var disabled = await connection.QuerySingleAsync<VersionState>("""
                    SELECT
                        lifecycle AS "Lifecycle",
                        content_hash AS "ContentHash"
                    FROM api_destination_profile_versions
                    WHERE id = @VersionId;
                    """, new { VersionId = versionId });
                Assert.Equal("Disabled", disabled.Lifecycle);
                Assert.Equal(ContentHashA, disabled.ContentHash);

                await using var rollback =
                    await connection.BeginTransactionAsync();
                await connection.ExecuteAsync("""
                    UPDATE api_destination_profile_versions
                    SET lifecycle = 'Active',
                        activated_at = now(),
                        activated_by = 'integration-test',
                        disabled_at = NULL,
                        disabled_by = NULL
                    WHERE id = @VersionId;

                    UPDATE api_destination_profiles
                    SET active_version_id = @VersionId
                    WHERE id = @ProfileId;
                    """, new { ProfileId = profileId, VersionId = versionId },
                    rollback);
                await rollback.CommitAsync();

                await connection.ExecuteAsync("""
                    INSERT INTO api_destination_profile_audit (
                        profile_id,
                        profile_version_id,
                        action,
                        actor,
                        reason,
                        request_id,
                        before_lifecycle,
                        after_lifecycle,
                        before_content_hash,
                        after_content_hash,
                        concurrency_token)
                    VALUES (
                        @ProfileId,
                        @VersionId,
                        'Rollback',
                        'integration-test',
                        'Restore approved historical version',
                        'request-rollback-1',
                        'Disabled',
                        'Active',
                        @ContentHash,
                        @ContentHash,
                        2);
                    """, new
                {
                    ProfileId = profileId,
                    VersionId = versionId,
                    ContentHash = ContentHashA
                });
            }

            var pointerMismatch = await InsertProfileAsync(
                dataSource,
                "partner-b");
            var draftId = await InsertVersionAsync(
                dataSource,
                pointerMismatch,
                1,
                ContentHashB);
            var invalidPointer = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profiles
                SET active_version_id = @VersionId
                WHERE id = @ProfileId;
                """,
                new { ProfileId = pointerMismatch, VersionId = draftId });
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                invalidPointer.SqlState);

            var rename = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profiles
                SET profile_key = 'renamed'
                WHERE id = @ProfileId;
                """,
                new { ProfileId = profileId });
            Assert.Equal(PostgresErrorCodes.CheckViolation, rename.SqlState);

            var auditUpdate = await ExecuteFailureAsync(
                dataSource,
                """
                UPDATE api_destination_profile_audit
                SET reason = 'Changed audit reason'
                WHERE request_id = 'request-rollback-1';
                """);
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                auditUpdate.SqlState);

            var auditDelete = await ExecuteFailureAsync(
                dataSource,
                """
                DELETE FROM api_destination_profile_audit
                WHERE request_id = 'request-rollback-1';
                """);
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                auditDelete.SqlState);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Concurrent_draft_creation_has_one_winner()
    {
        await WithCatalogAsync(async dataSource =>
        {
            var profileId = await InsertProfileAsync(
                dataSource,
                "concurrent-partner");
            await using var firstConnection =
                await dataSource.OpenConnectionAsync();
            await using var secondConnection =
                await dataSource.OpenConnectionAsync();
            await using var firstTransaction =
                await firstConnection.BeginTransactionAsync();
            await using var secondTransaction =
                await secondConnection.BeginTransactionAsync();

            await firstConnection.ExecuteScalarAsync<long>(
                InsertVersionSql,
                VersionParameters(profileId, 1, ContentHashA),
                firstTransaction);
            var competingInsert = secondConnection.ExecuteScalarAsync<long>(
                InsertVersionSql,
                VersionParameters(profileId, 2, ContentHashB),
                secondTransaction);

            await firstTransaction.CommitAsync();
            var conflict = await Assert.ThrowsAsync<PostgresException>(
                async () => await competingInsert);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, conflict.SqlState);
            await secondTransaction.RollbackAsync();

            await using var verification =
                await dataSource.OpenConnectionAsync();
            Assert.Equal(
                1,
                await verification.ExecuteScalarAsync<int>("""
                    SELECT count(*)
                    FROM api_destination_profile_versions
                    WHERE profile_id = @ProfileId
                      AND lifecycle = 'Draft';
                    """, new { ProfileId = profileId }));
        });
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

        var schema = $"profile_catalog_{Guid.NewGuid():N}"[..40];
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
                    api_destination_profile_id text,
                    marker text NOT NULL);
                CREATE TABLE delivery_jobs (
                    id bigint PRIMARY KEY,
                    destination_profile_id text,
                    marker text NOT NULL);
                CREATE TABLE migration_baseline (
                    kind text NOT NULL,
                    row_id bigint NOT NULL,
                    version_token text NOT NULL,
                    PRIMARY KEY (kind, row_id));

                INSERT INTO mapping_config_versions (
                    id, api_destination_profile_id, marker)
                VALUES (1, 'pa-mtls', 'keep');
                INSERT INTO delivery_jobs (
                    id, destination_profile_id, marker)
                VALUES (1, 'pa-mtls', 'keep');

                INSERT INTO migration_baseline (
                    kind, row_id, version_token)
                SELECT 'mapping', id, xmin::text
                FROM mapping_config_versions
                UNION ALL
                SELECT 'delivery', id, xmin::text
                FROM delivery_jobs;
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

    private static async Task<long> InsertProfileAsync(
        NpgsqlDataSource dataSource,
        string profileKey)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>("""
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
    }

    private static async Task<long> InsertVersionAsync(
        NpgsqlDataSource dataSource,
        long profileId,
        int versionNo,
        string contentHash)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            InsertVersionSql,
            VersionParameters(profileId, versionNo, contentHash));
    }

    private static object VersionParameters(
        long profileId,
        int versionNo,
        string contentHash) => new
        {
            ProfileId = profileId,
            VersionNo = versionNo,
            ContentHash = contentHash
        };

    private static async Task<PostgresException> ExecuteFailureAsync(
        NpgsqlDataSource dataSource,
        string sql,
        object? parameters = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await Assert.ThrowsAsync<PostgresException>(
            async () => await connection.ExecuteAsync(sql, parameters));
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

    private sealed record LegacyRow(
        string Kind,
        long Id,
        string BeforeVersionToken,
        string VersionToken,
        string Marker,
        string ProfileKey,
        long? ProfileVersionId);

    private sealed record VersionState(
        string Lifecycle,
        string ContentHash);
}
