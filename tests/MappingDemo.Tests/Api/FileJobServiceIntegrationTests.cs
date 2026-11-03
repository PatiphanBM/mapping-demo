using Dapper;
using System.Text.Json;
using MappingDemo.Api.Contracts.FileJobs;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Tests.Api;

public sealed class FileJobServiceIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task GetById_returns_filtered_row_count()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"file_job_test_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var testConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                testConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync("""
                CREATE TABLE file_jobs (
                    id bigint PRIMARY KEY,
                    config_id bigint NOT NULL,
                    config_version_id bigint NOT NULL,
                    file_name text NOT NULL,
                    import_status text NOT NULL,
                    archive_status text NOT NULL,
                    total_rows integer,
                    last_error text,
                    created_at timestamptz NOT NULL,
                    updated_at timestamptz NOT NULL
                );
                CREATE TABLE row_jobs (
                    id bigint PRIMARY KEY,
                    file_job_id bigint NOT NULL,
                    source_row_id bigint NOT NULL,
                    status text NOT NULL
                );
                CREATE TABLE delivery_jobs (
                    file_job_id bigint NOT NULL,
                    status text NOT NULL
                );
                CREATE TABLE json_export_jobs (
                    id bigint PRIMARY KEY,
                    file_job_id bigint NOT NULL,
                    artifact_revision integer NOT NULL,
                    status text NOT NULL,
                    relative_final_path text NOT NULL,
                    object_count integer,
                    byte_count bigint,
                    sha256 text,
                    published_at timestamptz,
                    last_error_redacted text
                );
                INSERT INTO file_jobs (
                    id, config_id, config_version_id, file_name,
                    import_status, archive_status, total_rows,
                    created_at, updated_at)
                VALUES (
                    1, 10, 20, 'sample.csv', 'Imported', 'Archived', 2,
                    now(), now());
                INSERT INTO row_jobs (
                    id, file_job_id, source_row_id, status)
                VALUES
                    (1, 1, 101, 'Done'),
                    (2, 1, 102, 'Filtered');
                INSERT INTO delivery_jobs (file_job_id, status)
                VALUES
                    (1, 'Delivered'),
                    (1, 'RetryableFailed');
                """);

            var controller = new FileJobsController(
                new FileJobService(dataSource));
            var actionResult = await controller.GetById(
                1,
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionResult);
            var response = Assert.IsType<FileJobResponse>(ok.Value);
            Assert.Equal(1, response.DoneRows);
            Assert.Equal(1, response.FilteredRows);
            Assert.Equal(NormalizationStatus.Completed, response.NormalizationStatus);
            Assert.Equal("RetryableFailed", response.DeliveryStatus);
            Assert.Equal(1, response.DeliveredDeliveries);
            Assert.Equal(1, response.RetryableFailedDeliveries);
            Assert.Null(response.Export);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task GetAll_returns_deterministic_additive_export_status_matrix()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"file_export_status_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var testConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                testConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync("""
                CREATE TABLE file_jobs (
                    id bigint PRIMARY KEY,
                    config_id bigint NOT NULL,
                    config_version_id bigint NOT NULL,
                    file_name text NOT NULL,
                    import_status text NOT NULL,
                    archive_status text NOT NULL,
                    total_rows integer,
                    last_error text,
                    created_at timestamptz NOT NULL,
                    updated_at timestamptz NOT NULL
                );
                CREATE TABLE row_jobs (
                    id bigint PRIMARY KEY,
                    file_job_id bigint NOT NULL,
                    source_row_id bigint NOT NULL,
                    status text NOT NULL
                );
                CREATE TABLE delivery_jobs (
                    file_job_id bigint NOT NULL,
                    status text NOT NULL
                );
                CREATE TABLE json_export_jobs (
                    id bigint PRIMARY KEY,
                    file_job_id bigint NOT NULL,
                    artifact_revision integer NOT NULL,
                    status text NOT NULL,
                    relative_final_path text NOT NULL,
                    object_count integer,
                    byte_count bigint,
                    sha256 text,
                    published_at timestamptz,
                    last_error_redacted text
                );
                INSERT INTO file_jobs (
                    id, config_id, config_version_id, file_name,
                    import_status, archive_status, total_rows,
                    created_at, updated_at)
                SELECT id, 10, 20, 'sample.csv', 'Imported', 'Archived', 0,
                       now(), now()
                FROM generate_series(1, 5) AS id;
                INSERT INTO json_export_jobs (
                    id, file_job_id, artifact_revision, status,
                    relative_final_path, object_count, byte_count, sha256,
                    published_at, last_error_redacted)
                VALUES
                    (20, 2, 1, 'Published', 'exports/old.json', 1, 10,
                     'sha256:old', now() - interval '1 day', NULL),
                    (21, 2, 2, 'Pending', 'exports/pending.json', NULL, NULL,
                     NULL, NULL, NULL),
                    (30, 3, 1, 'Blocked', 'exports/blocked.json', NULL, NULL,
                     NULL, NULL, 'Latest rows contain invalid outcomes.'),
                    (40, 4, 1, 'RetryableFailed', 'exports/retry.json', NULL,
                     NULL, NULL, NULL, 'internal filesystem detail'),
                    (50, 5, 3, 'Published', 'exports/final.json', 2, 123,
                     'sha256:abc', '2026-01-02T03:04:05Z', NULL);
                """);

            var responses = await new FileJobService(dataSource).GetAllAsync();
            var byId = responses.ToDictionary(response => response.Id);

            Assert.Null(byId[1].Export);
            Assert.Equal("Pending", byId[2].Export!.Status);
            Assert.Equal(2, byId[2].Export!.ArtifactRevision);
            Assert.Equal("exports/pending.json", byId[2].Export!.RelativeDisplayPath);
            Assert.Null(byId[2].Export!.ObjectCount);

            Assert.Equal("Blocked", byId[3].Export!.Status);
            Assert.Equal(
                "Latest rows contain invalid outcomes.",
                byId[3].Export!.BlockReason);

            Assert.Equal("RetryableFailed", byId[4].Export!.Status);
            Assert.Null(byId[4].Export!.BlockReason);

            Assert.Equal("Published", byId[5].Export!.Status);
            Assert.Equal(2, byId[5].Export!.ObjectCount);
            Assert.Equal(123, byId[5].Export!.ByteCount);
            Assert.Equal("sha256:abc", byId[5].Export!.Sha256);
            Assert.Equal(
                new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                byId[5].Export!.PublishedAt);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    public void File_job_response_deserializes_without_additive_export_member()
    {
        const string oldClientPayload = """
            {
              "id": 1,
              "configId": 10,
              "configVersionId": 20,
              "fileName": "sample.csv",
              "importStatus": "Imported",
              "archiveStatus": "Archived",
              "totalRows": 0,
              "normalizationStatus": "Completed",
              "pendingRows": 0,
              "doneRows": 0,
              "filteredRows": 0,
              "invalidRows": 0,
              "failedRows": 0,
              "lastError": null,
              "createdAt": "2026-01-01T00:00:00Z",
              "updatedAt": "2026-01-01T00:00:00Z"
            }
            """;

        var response = JsonSerializer.Deserialize<FileJobResponse>(
            oldClientPayload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(response);
        Assert.Equal(1, response.Id);
        Assert.Null(response.Export);
    }
}
