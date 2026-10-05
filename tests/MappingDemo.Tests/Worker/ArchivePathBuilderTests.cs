using MappingDemo.Worker;

namespace MappingDemo.Tests.Worker;

public sealed class ArchivePathBuilderTests
{
    [Fact]
    public void Build_uses_date_and_original_file_name()
    {
        var archiveRoot = Path.Combine("demo", "archive");

        var path = ArchivePathBuilder.Build(
            archiveRoot,
            "orders.csv",
            new DateOnly(2026, 9, 29));

        Assert.Equal(
            Path.Combine(
                archiveRoot,
                "29092026",
                "orders.csv"),
            path);
    }

    [Fact]
    public void Build_strips_directories_from_file_name()
    {
        var path = ArchivePathBuilder.Build(
            "archive",
            Path.Combine("nested", "orders.csv"),
            new DateOnly(2026, 9, 29));

        Assert.EndsWith(
            Path.Combine("29092026", "orders.csv"),
            path);
    }
}
