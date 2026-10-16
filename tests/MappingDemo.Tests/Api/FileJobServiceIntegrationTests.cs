using Dapper;
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
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }
}
