using MappingDemo.Shared.Database;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class MappingConfigMigrationTests
{
    [Fact]
    public async Task Table_baseline_persists_column_metadata_and_new_types()
    {
        var sql = await ReadMigrationAsync("0001_table_definitions.sql");

        Assert.Contains("'Integer'", sql);
        Assert.Contains("'DateTime'", sql);
        Assert.Contains("'Guid'", sql);
        Assert.Contains("length integer", sql);
        Assert.Contains("precision integer", sql);
        Assert.Contains("scale integer", sql);
    }

    [Fact]
    public async Task Baseline_persists_input_layout_without_legacy_column()
    {
        var sql = await ReadMigrationAsync("0002_mapping_configs.sql");

        Assert.Contains("input_layout jsonb NOT NULL", sql);
        Assert.DoesNotContain("file_to_source", sql);
    }

    [Fact]
    public async Task Baseline_persists_transformation_program_without_legacy_column()
    {
        var sql = await ReadMigrationAsync("0002_mapping_configs.sql");

        Assert.Contains("transformation_program jsonb NOT NULL", sql);
        Assert.DoesNotContain("source_to_normalized", sql);
    }

    [Fact]
    public async Task Row_job_evaluation_instant_is_persisted_by_migration()
    {
        var sql = await ReadMigrationAsync(
            "0005_row_job_evaluation_time.sql");

        Assert.Contains(
            "evaluation_at timestamptz NOT NULL DEFAULT now()",
            sql);
    }

    [Fact]
    public async Task Row_error_context_is_added_by_migration()
    {
        var sql = await ReadMigrationAsync(
            "0006_row_output_reconciliation.sql");

        Assert.Contains("ADD COLUMN output_key text", sql);
        Assert.Contains("ADD COLUMN expression_path text", sql);
        Assert.Contains("ALTER COLUMN field DROP NOT NULL", sql);
        Assert.Contains("ALTER COLUMN expression_path SET NOT NULL", sql);
    }

    [Fact]
    public async Task Destination_migration_is_expand_only_and_backfills_legacy()
    {
        var sql = await ReadMigrationAsync(
            "0007_destination_definition.sql");

        Assert.Contains("destination_definition jsonb", sql);
        Assert.Contains("api_destination_profile_id text", sql);
        Assert.Contains("contract_reference text", sql);
        Assert.Contains("contract_schema_snapshot jsonb", sql);
        Assert.Contains("contract_schema_hash text", sql);
        Assert.Contains("contract_schema_fetched_at timestamptz", sql);
        Assert.Contains("payload_bindings_snapshot jsonb", sql);
        Assert.Contains("jsonb_build_object", sql);
        Assert.Contains("'kind', 'legacyTable'", sql);
        Assert.Contains("'normalizedTableId'", sql);
        Assert.Contains("ALTER COLUMN normalized_table_id DROP NOT NULL", sql);
        Assert.Contains(
            "mapping_config_versions_destination_kind_check",
            sql);
        Assert.Contains("ix_mapping_config_versions_destination_kind", sql);
        Assert.DoesNotContain("DROP COLUMN", sql);
    }

    [Fact]
    public async Task Json_file_destination_migration_only_expands_kind_check()
    {
        var sql = await ReadMigrationAsync(
            "0011_json_file_destination.sql");

        Assert.Contains(
            "DROP CONSTRAINT mapping_config_versions_destination_kind_check",
            sql);
        Assert.Contains("'legacyTable'", sql);
        Assert.Contains("'apiContract'", sql);
        Assert.Contains("'jsonFile'", sql);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
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
}
