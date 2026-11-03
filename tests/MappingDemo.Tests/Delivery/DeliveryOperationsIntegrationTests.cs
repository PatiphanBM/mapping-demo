using Dapper;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using MappingDemo.Worker.Delivery;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryOperationsIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Rollback_pins_existing_api_job_and_routes_new_file_to_legacy_version()
    {
        var rootConnection = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(rootConnection))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        var schema = $"delivery_rollback_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(rootConnection);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var connectionString = new NpgsqlConnectionStringBuilder(rootConnection)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await new MigrationRunner(dataSource).RunAsync();
            await using var connection = await dataSource.OpenConnectionAsync();
            var versions = await connection.QuerySingleAsync<VersionPair>("""
                INSERT INTO table_definitions (name, kind)
                VALUES ('rollback_source', 'Source'), ('rollback_normalized', 'Normalized');
                INSERT INTO mapping_configs (
                    name, input_folder, source_table_id, normalized_table_id)
                SELECT 'Rollback canary', 'input/rollback', source.id, normalized.id
                FROM table_definitions AS source
                CROSS JOIN table_definitions AS normalized
                WHERE source.name = 'rollback_source'
                  AND normalized.name = 'rollback_normalized';
                INSERT INTO mapping_config_versions (
                    config_id, version_no, input_layout, transformation_program,
                    destination_definition, accepted_extensions)
                SELECT id, 1, '{}'::jsonb, '{}'::jsonb,
                    jsonb_build_object(
                        'kind', 'legacyTable',
                        'normalizedTableId', normalized_table_id),
                    '[".csv"]'::jsonb
                FROM mapping_configs;
                INSERT INTO mapping_config_versions (
                    config_id, version_no, input_layout, transformation_program,
                    destination_definition, accepted_extensions)
                SELECT id, 2, '{}'::jsonb, '{}'::jsonb,
                    '{"kind":"apiContract","apiDestinationProfileId":"stub","contract":{"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:schema"}}'::jsonb,
                    '[".csv"]'::jsonb
                FROM mapping_configs;
                UPDATE mapping_configs AS configs
                SET active_version_id = versions.id
                FROM mapping_config_versions AS versions
                WHERE versions.config_id = configs.id AND versions.version_no = 2;
                SELECT
                    MAX(id) FILTER (WHERE version_no = 1) AS "LegacyVersionId",
                    MAX(id) FILTER (WHERE version_no = 2) AS "ApiVersionId"
                FROM mapping_config_versions;
                """);
            var oldJob = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO file_jobs (
                    config_id, config_version_id, original_path, file_name,
                    intake_key, import_status, archive_status)
                SELECT id, active_version_id, 'input/rollback/api.csv', 'api.csv',
                    'rollback-api', 'Imported', 'Archived'
                FROM mapping_configs
                RETURNING id;
                """);

            var started = System.Diagnostics.Stopwatch.StartNew();
            await connection.ExecuteAsync("""
                UPDATE mapping_config_versions SET activated_at = now()
                WHERE id = @LegacyVersionId;
                UPDATE mapping_configs SET active_version_id = @LegacyVersionId;
                """, versions);
            started.Stop();
            var newJob = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO file_jobs (
                    config_id, config_version_id, original_path, file_name,
                    intake_key, import_status, archive_status)
                SELECT id, active_version_id, 'input/rollback/legacy.csv', 'legacy.csv',
                    'rollback-legacy', 'Queued', 'Pending'
                FROM mapping_configs
                RETURNING id;
                """);

            Assert.True(started.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal(versions.ApiVersionId,
                await connection.ExecuteScalarAsync<long>(
                    "SELECT config_version_id FROM file_jobs WHERE id = @Id;",
                    new { Id = oldJob }));
            Assert.Equal(versions.LegacyVersionId,
                await connection.ExecuteScalarAsync<long>(
                    "SELECT config_version_id FROM file_jobs WHERE id = @Id;",
                    new { Id = newJob }));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Control_rollout_metrics_and_alerts_are_persisted_and_enforced()
    {
        var rootConnection = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(rootConnection))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        var schema = $"delivery_ops_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(rootConnection);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var connectionString = new NpgsqlConnectionStringBuilder(rootConnection)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await new MigrationRunner(dataSource).RunAsync();
            var configId = await SeedAsync(dataSource);
            var service = new DeliveryOperationsService(dataSource);

            Assert.True(await service.UpdateRolloutAsync(
                configId,
                "Shadow",
                true,
                "operator-1",
                "Non-mutating stub validation",
                CancellationToken.None));
            Assert.True((await service.CanActivateApiContractAsync(
                configId,
                "Sandbox",
                CancellationToken.None)).Allowed);
            Assert.False((await service.CanActivateApiContractAsync(
                configId,
                "Production",
                CancellationToken.None)).Allowed);

            var snapshot = await service.GetAsync(CancellationToken.None);
            Assert.Equal(0.5, snapshot.SuccessRate);
            Assert.Equal(0.5, snapshot.RetryableFailureRate);
            Assert.Equal(1, snapshot.RetryCount);
            Assert.Equal(1, snapshot.DuplicateConflictCount);
            Assert.Equal(1, snapshot.SchemaValidationFailureCount);
            Assert.Contains(snapshot.Alerts, alert =>
                alert.Code == "DuplicateConflict" &&
                alert.Severity == "Critical");
            Assert.Contains(snapshot.Alerts, alert =>
                alert.Code == "SchemaValidationFailure");
            await using (var read = await dataSource.OpenConnectionAsync())
            {
                var fileJobId = await read.ExecuteScalarAsync<long>(
                    "SELECT id FROM file_jobs LIMIT 1;");
                var deliveryRows = await new DeliveryJobService(dataSource)
                    .GetForFileJobAsync(fileJobId, CancellationToken.None);
                Assert.Equal(2, deliveryRows.Count);
                Assert.Contains(deliveryRows, row =>
                    row.Status == "RetryableFailed" && row.CanRetry);
                Assert.All(deliveryRows, row =>
                    Assert.StartsWith("sha256:", row.PayloadHash));
            }

            var control = await service.UpdateControlAsync(
                deliveryPaused: true,
                activationEnabled: false,
                "operator-2",
                "Rollback rehearsal",
                CancellationToken.None);
            Assert.True(control.DeliveryPaused);
            Assert.False(control.ApiContractActivationEnabled);
            Assert.True(await new DeliveryControlReader(dataSource)
                .IsPausedAsync(CancellationToken.None));
            Assert.False((await service.CanActivateApiContractAsync(
                configId,
                "Sandbox",
                CancellationToken.None)).Allowed);

            await using var verify = await dataSource.OpenConnectionAsync();
            var pausedJobId = await verify.ExecuteScalarAsync<long>("""
                UPDATE delivery_jobs
                SET dispatch_scheduled_at = now()
                WHERE status = 'RetryableFailed'
                RETURNING id;
                """);
            var http = new CountingHttpAdapter();
            await new DeliveryJobHandler(
                dataSource,
                new ApiDestinationProfileCatalog([]),
                null!,
                http,
                new DeliveryControlReader(dataSource),
                NullLogger<DeliveryJobHandler>.Instance)
                .HandleAsync(pausedJobId, CancellationToken.None);
            Assert.Equal(0, http.SendCount);
            Assert.Null(await verify.ExecuteScalarAsync<DateTimeOffset?>(
                "SELECT dispatch_scheduled_at FROM delivery_jobs WHERE id = @Id;",
                new { Id = pausedJobId }));
            Assert.Equal(1, await verify.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM delivery_control_audit;"));
            Assert.Equal(1, await verify.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM api_delivery_rollout_audit;"));

            await verify.ExecuteAsync("""
                UPDATE delivery_jobs
                SET payload_expires_at = now() - interval '1 second'
                WHERE status = 'Delivered';
                """);
            await new DeliveryRetentionService(
                dataSource,
                NullLogger<DeliveryRetentionService>.Instance)
                .RunOnceAsync(CancellationToken.None);
            Assert.Equal(0, await verify.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::integer
                FROM delivery_jobs
                WHERE status = 'Delivered'
                  AND payload_ciphertext IS NOT NULL;
                """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task<long> SeedAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO table_definitions (name, kind)
            VALUES ('source_ops', 'Source');
            INSERT INTO mapping_configs (
                name, input_folder, source_table_id, normalized_table_id)
            SELECT 'Operations canary', 'input/operations', id, NULL
            FROM table_definitions WHERE name = 'source_ops';
            INSERT INTO mapping_config_versions (
                config_id,
                version_no,
                input_layout,
                transformation_program,
                destination_definition,
                accepted_extensions)
            SELECT
                id,
                1,
                '{}'::jsonb,
                '{}'::jsonb,
                '{"kind":"apiContract","apiDestinationProfileId":"stub","contract":{"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:schema"}}'::jsonb,
                '[".csv"]'::jsonb
            FROM mapping_configs;
            INSERT INTO file_jobs (
                config_id,
                config_version_id,
                original_path,
                file_name,
                intake_key,
                import_status,
                archive_status,
                created_at)
            SELECT config_id, id, 'input/operations/a.csv', 'a.csv', 'ops-a',
                'Imported', 'Archived', now() - interval '20 seconds'
            FROM mapping_config_versions;
            INSERT INTO row_jobs (
                file_job_id,
                source_row_id,
                row_number,
                config_version_id,
                kind,
                status)
            SELECT file_jobs.id, 10, 1, file_jobs.config_version_id,
                'Initial', 'Invalid'
            FROM file_jobs;
            INSERT INTO row_errors (
                row_job_id, file_job_id, row_number, field, expression_path, reason)
            SELECT row_jobs.id, row_jobs.file_job_id, 1, 'proposal.name',
                'delivery.proposal.name',
                'Required contract path is missing.'
            FROM row_jobs;
            """);
        var configId = await connection.ExecuteScalarAsync<long>(
            "SELECT id FROM mapping_configs WHERE name = 'Operations canary';");
        await connection.ExecuteAsync("""
            INSERT INTO delivery_jobs (
                file_job_id,
                row_job_id,
                source_row_id,
                source_row_number,
                config_version_id,
                output_key,
                operation_kind,
                destination_profile_id,
                contract_id,
                contract_version,
                contract_reference,
                schema_hash,
                payload_ciphertext,
                payload_nonce,
                payload_tag,
                payload_hash,
                encryption_key_reference,
                idempotency_key,
                status,
                attempts,
                delivered_at,
                terminal_at,
                payload_expires_at,
                audit_expires_at)
            SELECT
                file_jobs.id, row_jobs.id, row_jobs.source_row_id,
                row_jobs.row_number, row_jobs.config_version_id,
                values.output_key, 'Initial', 'stub', 'proposal', '1',
                'proposal/1', 'sha256:schema', '\x01'::bytea,
                decode(repeat('00', 12), 'hex'), decode(repeat('00', 16), 'hex'),
                'sha256:payload-' || values.output_key, 'test-key',
                row_jobs.config_version_id || ':' || file_jobs.id || ':' ||
                    row_jobs.source_row_id || ':' || values.output_key,
                values.status, values.attempts,
                CASE WHEN values.status = 'Delivered' THEN now() ELSE NULL END,
                CASE WHEN values.status = 'Delivered' THEN now() ELSE NULL END,
                now() + interval '30 days', now() + interval '365 days'
            FROM file_jobs
            INNER JOIN row_jobs ON row_jobs.file_job_id = file_jobs.id
            CROSS JOIN (VALUES
                ('delivered', 'Delivered', 1),
                ('retry', 'RetryableFailed', 2))
                AS values(output_key, status, attempts);

            INSERT INTO delivery_attempts (
                delivery_job_id,
                attempt_no,
                started_at,
                finished_at,
                outcome,
                http_status,
                duration_ms,
                correlation_id,
                error_redacted,
                payload_hash,
                idempotency_key)
            SELECT
                id, 1, now() - interval '7 seconds', now(), status,
                CASE WHEN status = 'Delivered' THEN 202 ELSE 409 END,
                CASE WHEN status = 'Delivered' THEN 100 ELSE 6000 END,
                'stub', last_error_redacted, payload_hash, idempotency_key
            FROM delivery_jobs;
            """);
        return configId;
    }

    private sealed record VersionPair(long LegacyVersionId, long ApiVersionId);

    private sealed class CountingHttpAdapter : IDeliveryHttpAdapter
    {
        public int SendCount { get; private set; }

        public ValueTask<DeliverySendResult> SendAsync(
            ApiDestinationProfile profile,
            DeliverySendRequest request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return ValueTask.FromResult(new DeliverySendResult(
                202,
                false,
                null,
                "unexpected",
                null));
        }
    }
}
