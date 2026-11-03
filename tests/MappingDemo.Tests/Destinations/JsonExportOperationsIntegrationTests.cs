using System.Text.Json;
using Dapper;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Tables;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonExportOperationsIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task History_and_retry_preserve_safe_artifact_identity()
    {
        await WithDatabaseAsync(async dataSource =>
        {
            var ids = await SeedAsync(dataSource);
            var service = new JsonExportOperationsService(dataSource);

            var history = await service.GetForFileJobAsync(
                ids.FileJobId,
                CancellationToken.None);

            Assert.Equal([2, 1], history.Select(item => item.ArtifactRevision));
            Assert.Equal("exports/customers/retry.json", history[0].RelativeDisplayPath);
            Assert.True(history[0].CanRetry);
            var before = JsonSerializer.Serialize(history[0]);
            Assert.DoesNotContain("OutputRoot", before, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("payload", before, StringComparison.OrdinalIgnoreCase);

            var result = await service.RetryAsync(
                ids.RetryExportId,
                "operator-1",
                "Disk capacity restored",
                CancellationToken.None);

            Assert.Equal(DeliveryActionOutcome.Accepted, result.Outcome);
            var retried = (await service.GetForFileJobAsync(
                ids.FileJobId,
                CancellationToken.None))[0];
            Assert.Equal("Pending", retried.Status);
            Assert.Equal(history[0].RelativeDisplayPath, retried.RelativeDisplayPath);
            Assert.Equal(history[0].Sha256, retried.Sha256);
            await using var verify = await dataSource.OpenConnectionAsync();
            Assert.Equal(1, await verify.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM json_export_retry_audit;"));
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Control_metrics_and_alerts_are_aggregate_and_pii_free()
    {
        await WithDatabaseAsync(async dataSource =>
        {
            var ids = await SeedAsync(dataSource);
            var service = new DeliveryOperationsService(dataSource);

            var control = await service.UpdateControlAsync(
                deliveryPaused: false,
                apiActivationEnabled: true,
                jsonFileActivationEnabled: false,
                jsonFileFinalizerPaused: true,
                "operator-2",
                "Gate E not signed",
                CancellationToken.None);
            var gate = await service.CanActivateJsonFileAsync(
                ids.ConfigId,
                CancellationToken.None);
            var snapshot = await service.GetAsync(CancellationToken.None);

            Assert.True(control.JsonFileFinalizerPaused);
            Assert.False(control.JsonFileActivationEnabled);
            Assert.False(gate.Allowed);
            var group = Assert.Single(snapshot.JsonExports.Groups);
            Assert.Equal("operations-json", group.DestinationProfileId);
            Assert.Equal(2, group.Total);
            Assert.Equal(1, group.Published);
            Assert.Equal(1, group.RetryableFailed);
            Assert.Equal(2, group.ObjectCount);
            Assert.Equal(123, group.ByteCount);
            Assert.Equal(1, group.HashConflictCount);
            Assert.Equal(1, group.CleanupFailureCount);
            Assert.Contains(snapshot.JsonExports.Alerts, alert =>
                alert.Code == "JsonExportHashConflict" &&
                alert.Severity == "Critical");
            Assert.Contains(snapshot.JsonExports.Alerts, alert =>
                alert.Code == "JsonExportCleanupFailure");
            await using var verify = await dataSource.OpenConnectionAsync();
            var identityBeforeResume = await verify.QuerySingleAsync<JsonExportIdentity>("""
                SELECT
                    artifact_revision AS "ArtifactRevision",
                    relative_temp_path AS "RelativeTempPath",
                    relative_final_path AS "RelativeFinalPath",
                    sha256 AS "Sha256"
                FROM json_export_jobs
                WHERE id = @Id;
                """, new { Id = ids.RetryExportId });
            var controlReader = new JsonExportControlReader(dataSource);
            Assert.True(await controlReader.IsFinalizerPausedAsync());

            await service.UpdateControlAsync(
                deliveryPaused: false,
                apiActivationEnabled: true,
                jsonFileActivationEnabled: false,
                jsonFileFinalizerPaused: false,
                "operator-2",
                "Resume existing staged exports",
                CancellationToken.None);

            Assert.False(await controlReader.IsFinalizerPausedAsync());
            var identityAfterResume = await verify.QuerySingleAsync<JsonExportIdentity>("""
                SELECT
                    artifact_revision AS "ArtifactRevision",
                    relative_temp_path AS "RelativeTempPath",
                    relative_final_path AS "RelativeFinalPath",
                    sha256 AS "Sha256"
                FROM json_export_jobs
                WHERE id = @Id;
                """, new { Id = ids.RetryExportId });
            Assert.Equal(identityBeforeResume, identityAfterResume);
            var json = JsonSerializer.Serialize(snapshot.JsonExports);
            Assert.DoesNotContain("Alice", json, StringComparison.Ordinal);
            Assert.DoesNotContain("server-secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\", json, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Cleanup_failure_is_redacted_and_keeps_artifact_identity()
    {
        await WithDatabaseAsync(async dataSource =>
        {
            var ids = await SeedAsync(dataSource);
            await using (var arrange = await dataSource.OpenConnectionAsync())
            {
                await arrange.ExecuteAsync("""
                    UPDATE json_export_jobs
                    SET artifact_expires_at = now() - interval '1 hour'
                    WHERE id = @Id;
                    """, new { Id = ids.RetryExportId });
            }
            var profiles = new FileDestinationProfileCatalog([
                new FileDestinationProfile(
                    "operations-json",
                    true,
                    Path.Combine(
                        Path.GetTempPath(),
                        "server-secret",
                        "exports"),
                    "volume-encrypted",
                    1024 * 1024,
                    0,
                    DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
                    10,
                    [new SupportedContract("proposal", "1", "proposal/1")],
                    [])
            ]);
            var logger = new ListLogger<JsonExportCleanupService>();
            var cleanup = new JsonExportCleanupService(
                dataSource,
                profiles,
                new FailingArtifactCleaner(),
                logger);

            await cleanup.RunOnceAsync(CancellationToken.None);

            await using var verify = await dataSource.OpenConnectionAsync();
            Assert.Null(await verify.ExecuteScalarAsync<DateTime?>(
                "SELECT artifact_deleted_at FROM json_export_jobs WHERE id = @Id;",
                new { Id = ids.RetryExportId }));
            var detail = await verify.ExecuteScalarAsync<string>("""
                SELECT details_redacted
                FROM json_export_operation_events
                WHERE event_type = 'CleanupFailure'
                ORDER BY id DESC LIMIT 1;
                """);
            Assert.Equal("Artifact cleanup failed; inspect storage permissions and health.", detail);
            Assert.DoesNotContain("server-secret", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "server-secret",
                string.Join(Environment.NewLine, logger.Entries),
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task WithDatabaseAsync(
        Func<NpgsqlDataSource, Task> test)
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

        var schema = $"json_ops_{Guid.NewGuid():N}"[..32];
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
            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task<SeedIds> SeedAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<SeedIds>("""
            INSERT INTO table_definitions (name, kind)
            VALUES ('json_ops_source', 'Source');
            INSERT INTO mapping_configs (
                name, input_folder, source_table_id, normalized_table_id)
            SELECT 'JSON operations', 'input/json-ops', id, NULL
            FROM table_definitions WHERE name = 'json_ops_source';
            INSERT INTO mapping_config_versions (
                config_id, version_no, input_layout, transformation_program,
                destination_definition, accepted_extensions)
            SELECT id, 1, '{}'::jsonb, '{}'::jsonb,
                '{"kind":"jsonFile","fileDestinationProfileId":"operations-json","contract":{"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:schema"},"relativeFolder":"exports/customers","fileNameTemplate":"{sourceBaseName}.{artifactRevision}.json","formatting":"compact"}'::jsonb,
                '[".csv"]'::jsonb
            FROM mapping_configs;
            INSERT INTO file_jobs (
                config_id, config_version_id, original_path, file_name,
                intake_key, import_status, archive_status)
            SELECT config_id, id, 'input/json-ops/customer.csv', 'customer.csv',
                'json-ops', 'Imported', 'Archived'
            FROM mapping_config_versions;
            INSERT INTO json_export_jobs (
                file_job_id, config_version_id, destination_profile_id,
                artifact_revision, status, relative_temp_path,
                relative_final_path, object_count, byte_count, sha256,
                attempt_count, terminal_at, published_at)
            SELECT file_jobs.id, file_jobs.config_version_id, 'operations-json',
                1, 'Published', 'exports/customers/one.tmp',
                'exports/customers/one.json', 2, 123, 'sha256:published', 1,
                now(), now()
            FROM file_jobs;
            INSERT INTO json_export_jobs (
                file_job_id, config_version_id, destination_profile_id,
                artifact_revision, status, relative_temp_path,
                relative_final_path, sha256, attempt_count,
                previous_export_job_id, last_error_redacted)
            SELECT file_jobs.id, file_jobs.config_version_id, 'operations-json',
                2, 'RetryableFailed', 'exports/customers/retry.tmp',
                'exports/customers/retry.json', 'sha256:staged', 2,
                published.id, 'Publication failed and will be retried.'
            FROM file_jobs
            CROSS JOIN json_export_jobs AS published
            WHERE published.artifact_revision = 1;
            INSERT INTO json_export_operation_events (
                config_id, config_version_id, destination_profile_id,
                event_type, details_redacted)
            SELECT configs.id, versions.id, 'operations-json', event_type,
                'redacted operational signal'
            FROM mapping_configs AS configs
            INNER JOIN mapping_config_versions AS versions
                ON versions.config_id = configs.id
            CROSS JOIN (VALUES ('HashConflict'), ('CleanupFailure'))
                AS events(event_type);
            SELECT
                configs.id AS "ConfigId",
                file_jobs.id AS "FileJobId",
                retry.id AS "RetryExportId"
            FROM mapping_configs AS configs
            INNER JOIN file_jobs ON file_jobs.config_id = configs.id
            INNER JOIN json_export_jobs AS retry
                ON retry.file_job_id = file_jobs.id
               AND retry.artifact_revision = 2;
            """);
    }

    private sealed record SeedIds(
        long ConfigId,
        long FileJobId,
        long RetryExportId);

    private sealed record JsonExportIdentity(
        int ArtifactRevision,
        string RelativeTempPath,
        string RelativeFinalPath,
        string? Sha256);

    private sealed class FailingArtifactCleaner : IJsonExportArtifactCleaner
    {
        public Task DeleteAsync(
            FileDestinationProfile profile,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException(profile.OutputRoot);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception) + exception?.ToString());
    }
}
