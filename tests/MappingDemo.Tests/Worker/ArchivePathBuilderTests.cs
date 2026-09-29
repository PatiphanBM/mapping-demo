using MappingDemo.Worker;

namespace MappingDemo.Tests.Worker;

public sealed class ArchivePathBuilderTests
{
    [Fact]
    public void Build_uses_config_date_job_and_original_file_name()
    {
        var archiveRoot = Path.Combine("demo", "archive");

        var path = ArchivePathBuilder.Build(
            archiveRoot,
            12,
            34,
            "orders.csv",
            new DateOnly(2026, 9, 29));

        Assert.Equal(
            Path.Combine(
                archiveRoot,
                "12",
                "20260929",
                "34_orders.csv"),
            path);
    }

    [Fact]
    public void Build_strips_directories_from_file_name()
    {
        var path = ArchivePathBuilder.Build(
            "archive",
            1,
            2,
            Path.Combine("nested", "orders.csv"),
            new DateOnly(2026, 9, 29));

        Assert.EndsWith(
            Path.Combine("1", "20260929", "2_orders.csv"),
            path);
    }
}
