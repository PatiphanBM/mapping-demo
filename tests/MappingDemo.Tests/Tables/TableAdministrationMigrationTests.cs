using MappingDemo.Shared.Database;

namespace MappingDemo.Tests.Tables;

public sealed class TableAdministrationMigrationTests
{
    [Fact]
    public async Task Migration_adds_status_history_audit_and_bounded_fields()
    {
        var resourceName =
            "MappingDemo.Shared.Database.Migrations." +
            "0018_table_schema_administration.sql";
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        Assert.Contains("verification_status", sql);
        Assert.Contains("table_schema_verifications", sql);
        Assert.Contains("table_admin_audit", sql);
        Assert.DoesNotContain("connection_string", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_sql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("business_row", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_makes_verification_and_audit_history_append_only()
    {
        var resourceName =
            "MappingDemo.Shared.Database.Migrations." +
            "0019_table_admin_append_only.sql";
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        Assert.Contains("table_schema_verifications_append_only", sql);
        Assert.Contains("table_admin_audit_append_only", sql);
        Assert.Contains("BEFORE UPDATE OR DELETE", sql);
    }
}
