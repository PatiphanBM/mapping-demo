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
