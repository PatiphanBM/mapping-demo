using MappingDemo.Shared.Database;

namespace MappingDemo.Tests.DatabaseDestinations;

public sealed class DatabaseDeliveryMigrationTests
{
    [Fact]
    public void Phase_29_delivery_migration_is_embedded_after_existing_migrations()
    {
        var resources = new MigrationRunner(null!).GetMigrationResourceNames();

        Assert.EndsWith(
            "0020_database_delivery.sql",
            resources[^1],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_expands_destination_and_persists_safe_lifecycle_state()
    {
        var resource = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(
                "MappingDemo.Shared.Database.Migrations.0020_database_delivery.sql");
        Assert.NotNull(resource);
        using var reader = new StreamReader(resource);
        var sql = await reader.ReadToEndAsync();

        Assert.Contains("'databaseTable'", sql);
        Assert.Contains("draft_destination_definition jsonb", sql);
        Assert.Contains("database_delivery_jobs", sql);
        Assert.Contains("database_delivery_attempts", sql);
        Assert.Contains("database_delivery_resolution_audit", sql);
        Assert.Contains("database_target_schema_checks", sql);
        Assert.Contains("mapped_values_ciphertext bytea NOT NULL", sql);
        Assert.Contains("mapped_columns jsonb NOT NULL", sql);
        Assert.Contains("'UnknownOutcome'", sql);
        Assert.Contains("mapping.database-delivery", sql);
        Assert.Contains("sent_at IS NULL", sql);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
    }
}
