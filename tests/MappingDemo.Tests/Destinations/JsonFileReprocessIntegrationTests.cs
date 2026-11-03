using Dapper;
using MappingDemo.Api.Contracts.FileJobs;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonFileReprocessIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task New_config_version_allocates_new_revision_and_audit_link()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        var schema = $"json_reprocess_{Guid.NewGuid():N}"[..36];
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
            await new MigrationRunner(dataSource).RunAsync();
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedAsync(connection);

            var result = await new FileJobService(dataSource).ReprocessAsync(
                fileJobId: 1,
                versionId: 2);

            Assert.Equal(FileReprocessOutcome.Accepted, result.Outcome);
            Assert.Equal(1, result.RowJobsCreated);
            var revisions = (await connection.QueryAsync<ExportRevision>("""
                SELECT
                    id AS "Id",
                    config_version_id AS "ConfigVersionId",
                    artifact_revision AS "ArtifactRevision",
                    relative_final_path AS "RelativeFinalPath",
                    previous_export_job_id AS "PreviousExportJobId",
                    status AS "Status",
                    artifact_expires_at AS "ArtifactExpiresAt",
                    audit_expires_at AS "AuditExpiresAt"
                FROM json_export_jobs
                ORDER BY artifact_revision;
                """)).ToArray();
            Assert.Collection(
                revisions,
                revision =>
                {
                    Assert.Equal(1, revision.ConfigVersionId);
                    Assert.Equal(1, revision.ArtifactRevision);
                    Assert.Equal("Published", revision.Status);
                    Assert.Equal(
                        "exports/source.v1.r1.json",
                        revision.RelativeFinalPath);
                    Assert.NotNull(revision.ArtifactExpiresAt);
                    Assert.NotNull(revision.AuditExpiresAt);
                },
                revision =>
                {
                    Assert.Equal(2, revision.ConfigVersionId);
                    Assert.Equal(2, revision.ArtifactRevision);
                    Assert.Equal(revisions[0].Id, revision.PreviousExportJobId);
                    Assert.Equal("Pending", revision.Status);
                    Assert.Equal(
                        "exports/source.v2.r2.json",
                        revision.RelativeFinalPath);
                });
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM row_jobs
                    WHERE config_version_id = 2
                      AND kind = 'Reprocess'
                      AND status = 'Pending';
                    """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static Task SeedAsync(NpgsqlConnection connection) =>
        connection.ExecuteAsync("""
            INSERT INTO table_definitions (id, name, kind)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'source_rows', 'Source');
            CREATE TABLE source_rows (
                id bigint PRIMARY KEY,
                file_job_id bigint NOT NULL,
                row_number integer NOT NULL);
            INSERT INTO mapping_configs (
                id, name, input_folder, source_table_id,
                normalized_table_id)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'json', 'json-in', 1, NULL);
            INSERT INTO mapping_config_versions (
                id, config_id, version_no, input_layout,
                transformation_program, destination_definition)
            OVERRIDING SYSTEM VALUE
            VALUES
                (1, 1, 1, '{}'::jsonb, CAST(@Program AS jsonb),
                 CAST(@Destination AS jsonb)),
                (2, 1, 2, '{}'::jsonb, CAST(@Program AS jsonb),
                 CAST(@Destination AS jsonb));
            INSERT INTO file_jobs (
                id, config_id, config_version_id, original_path,
                file_name, intake_key, import_status, archive_status,
                total_rows)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, 'input/source.csv', 'source.csv',
                'intake-1', 'Imported', 'Archived', 1);
            INSERT INTO source_rows (id, file_job_id, row_number)
            VALUES (100, 1, 1);
            INSERT INTO json_export_jobs (
                file_job_id, config_version_id,
                destination_profile_id, artifact_revision, status,
                relative_temp_path, relative_final_path,
                terminal_at, published_at, payload_expires_at,
                artifact_expires_at, audit_expires_at)
            VALUES (
                1, 1, 'local-json-export', 1, 'Published',
                'exports/source.v1.r1.json.tmp',
                'exports/source.v1.r1.json',
                now(), now(), now() + interval '30 days',
                now() + interval '30 days', now() + interval '365 days');
            """, new
        {
            Program = "{\"semanticsProfile\":\"legacy-v1\",\"outputs\":[]}",
            Destination = """
                {
                  "kind":"jsonFile",
                  "fileDestinationProfileId":"local-json-export",
                  "contract":{
                    "id":"proposal",
                    "version":"1",
                    "reference":"proposal/1",
                    "schemaHash":"sha256:test"
                  },
                  "relativeFolder":"exports",
                  "fileNameTemplate":"{sourceBaseName}.v{configVersion}.r{artifactRevision}.json",
                  "formatting":"compact"
                }
                """
        });

    private sealed record ExportRevision(
        long Id,
        long ConfigVersionId,
        int ArtifactRevision,
        string RelativeFinalPath,
        long? PreviousExportJobId,
        string Status,
        DateTime? ArtifactExpiresAt,
        DateTime? AuditExpiresAt);
}
