using Dapper;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Tables;

public sealed class TableSchemaInspectorIntegrationTests
{
    [Fact]
    public Task Healthy_source_and_normalized_tables_match_metadata() =>
        WithDatabaseAsync(async dataSource =>
        {
            var service = new TableService(dataSource);
            var inspector = new PostgreSqlTableSchemaInspector(dataSource);
            var source = Source("src_inspector_healthy");
            var normalized = Normalized("norm_inspector_healthy");
            var sourceId = await service.CreateAsync(source);
            var normalizedId = await service.CreateAsync(normalized);

            var sourceResult = await inspector.InspectAsync(sourceId, source);
            var normalizedResult = await inspector.InspectAsync(
                normalizedId,
                normalized);

            Assert.Equal(TableVerificationStatus.Healthy, sourceResult.Status);
            Assert.Empty(sourceResult.Diagnostics);
            Assert.Equal(
                TableVerificationStatus.Healthy,
                normalizedResult.Status);
            Assert.Empty(normalizedResult.Diagnostics);
        });

    [Fact]
    public Task Inspector_reports_extra_missing_type_and_constraint_drift() =>
        WithDatabaseAsync(async dataSource =>
        {
            var service = new TableService(dataSource);
            var inspector = new PostgreSqlTableSchemaInspector(dataSource);
            await using var connection = await dataSource.OpenConnectionAsync();

            var extra = Source("src_inspector_extra");
            var extraId = await service.CreateAsync(extra);
            await connection.ExecuteAsync(
                "ALTER TABLE src_inspector_extra ADD COLUMN unexpected text;");
            var extraResult = await inspector.InspectAsync(extraId, extra);
            Assert.Contains(extraResult.Diagnostics, item =>
                item.Code == "UnexpectedColumn");

            var missing = Source("src_inspector_missing_column");
            var missingId = await service.CreateAsync(missing);
            await connection.ExecuteAsync(
                "ALTER TABLE src_inspector_missing_column DROP COLUMN raw_value;");
            var missingResult = await inspector.InspectAsync(
                missingId,
                missing);
            Assert.Contains(missingResult.Diagnostics, item =>
                item.Code == "ColumnMissing");

            var changed = Source("src_inspector_changed");
            var changedId = await service.CreateAsync(changed);
            await connection.ExecuteAsync("""
                ALTER TABLE src_inspector_changed
                ALTER COLUMN raw_value TYPE integer USING NULL;
                """);
            var changedResult = await inspector.InspectAsync(
                changedId,
                changed);
            Assert.Contains(changedResult.Diagnostics, item =>
                item.Code == "ColumnTypeMismatch");

            var constraint = Source("src_inspector_constraint");
            var constraintId = await service.CreateAsync(constraint);
            await connection.ExecuteAsync("""
                ALTER TABLE src_inspector_constraint
                DROP CONSTRAINT src_inspector_constraint_file_job_id_row_number_key;
                """);
            var constraintResult = await inspector.InspectAsync(
                constraintId,
                constraint);
            Assert.Contains(constraintResult.Diagnostics, item =>
                item.Code == "UniqueConstraintMissing");
        });

    [Fact]
    public Task Missing_table_and_parameterized_name_are_safe() =>
        WithDatabaseAsync(async dataSource =>
        {
            var service = new TableService(dataSource);
            var inspector = new PostgreSqlTableSchemaInspector(dataSource);
            var definition = Source("src_inspector_dropped");
            var id = await service.CreateAsync(definition);
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync("DROP TABLE src_inspector_dropped;");

            var missing = await inspector.InspectAsync(id, definition);
            var injection = await inspector.InspectAsync(
                id,
                definition with
                {
                    Name = "x'; DROP TABLE table_definitions; --"
                });
            var metadataExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT to_regclass('table_definitions') IS NOT NULL;");

            Assert.Equal(TableVerificationStatus.Missing, missing.Status);
            Assert.Equal(TableVerificationStatus.Missing, injection.Status);
            Assert.True(metadataExists);
        });

    [Fact]
    public Task Verification_persists_latest_status_and_add_column_resets_it() =>
        WithDatabaseAsync(async dataSource =>
        {
            var service = new TableService(dataSource);
            var inspector = new PostgreSqlTableSchemaInspector(dataSource);
            var definition = Normalized("norm_inspector_persist");
            var audit = new TableAuditContext(
                "operator-a",
                "Create table for verification test",
                "test-correlation");
            var id = await service.CreateAsync(definition, audit);
            var result = await inspector.InspectAsync(id, definition);
            await service.SaveVerificationAsync(
                id,
                result,
                "operator-a",
                "verify-correlation");

            var verified = await service.GetByIdAsync(id);
            var latest = await service.GetLatestVerificationAsync(id);
            Assert.Equal(TableVerificationStatus.Healthy, verified!.VerificationStatus);
            Assert.Equal(TableVerificationStatus.Healthy, latest!.Status);

            await service.AddColumnAsync(
                id,
                new ColumnRequest(
                    "new_value",
                    ColumnDataType.Text,
                    IsRequired: false),
                new TableAuditContext(
                    "operator-a",
                    "Add approved additive column",
                    "add-correlation"));
            var reset = await service.GetByIdAsync(id);
            Assert.Equal(TableVerificationStatus.Unknown, reset!.VerificationStatus);

            await using var connection = await dataSource.OpenConnectionAsync();
            var auditCount = await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)
                FROM table_admin_audit
                WHERE table_id = @Id
                  AND outcome = 'Success';
                """, new { Id = id });
            Assert.Equal(2, auditCount);
        });

    [Fact]
    public Task Concurrent_additive_columns_are_serialized() =>
        WithDatabaseAsync(async dataSource =>
        {
            var service = new TableService(dataSource);
            var id = await service.CreateAsync(
                Normalized("norm_inspector_concurrent"));
            var audit = new TableAuditContext(
                "operator-a",
                "Approved concurrent additive change",
                "concurrent-correlation");

            await Task.WhenAll(
                service.AddColumnAsync(
                    id,
                    new ColumnRequest("value_a", ColumnDataType.Text, false),
                    audit),
                service.AddColumnAsync(
                    id,
                    new ColumnRequest("value_b", ColumnDataType.Integer, false),
                    audit));

            await using var connection = await dataSource.OpenConnectionAsync();
            var ordinals = (await connection.QueryAsync<int>("""
                SELECT ordinal
                FROM table_columns
                WHERE table_id = @Id
                ORDER BY ordinal;
                """, new { Id = id })).ToArray();
            Assert.Equal([1, 2, 3], ordinals);
        });

    private static TableDefinition Source(string name) =>
        new(
            name,
            TableKind.Source,
            [new("raw_value", ColumnDataType.Text, false, 1, Length: 50)]);

    private static TableDefinition Normalized(string name) =>
        new(
            name,
            TableKind.Normalized,
            [
                new(
                    "amount",
                    ColumnDataType.Decimal,
                    true,
                    1,
                    Precision: 18,
                    Scale: 2)
            ]);

    private static async Task WithDatabaseAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable(
                        "MAPPING_REQUIRE_POSTGRES_INTEGRATION"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "MAPPING_POSTGRES_INTEGRATION is required.");
            }
            return;
        }

        var schema = $"table_admin_{Guid.NewGuid():N}"[..40];
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
            await new MigrationRunner(dataSource).RunAsync();
            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }
}
