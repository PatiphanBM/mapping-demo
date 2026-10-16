using Dapper;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class RowOutputMigrationIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Migration_adds_nullable_error_context_and_backfills_path()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"migration_test_{Guid.NewGuid():N}"[..32];
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
                CREATE TABLE row_errors (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    field text NOT NULL,
                    reason text NOT NULL
                );
                INSERT INTO row_errors (field, reason)
                VALUES ('amount', 'InvalidDecimal');
                """);
            await connection.ExecuteAsync(
                await ReadMigrationAsync("0006_row_output_reconciliation.sql"));

            var columns = (await connection.QueryAsync<ColumnState>("""
                    SELECT
                        column_name AS "Name",
                        is_nullable AS "IsNullable"
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'row_errors'
                      AND column_name IN (
                          'output_key', 'field', 'expression_path')
                    ORDER BY column_name;
                    """))
                .ToDictionary(column => column.Name, StringComparer.Ordinal);
            Assert.Equal("YES", columns["output_key"].IsNullable);
            Assert.Equal("YES", columns["field"].IsNullable);
            Assert.Equal("NO", columns["expression_path"].IsNullable);
            Assert.Equal(
                "amount",
                await connection.ExecuteScalarAsync<string>(
                    "SELECT expression_path FROM row_errors;"));
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

    private sealed record ColumnState(string Name, string IsNullable);
}
