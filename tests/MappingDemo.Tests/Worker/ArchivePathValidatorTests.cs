using MappingDemo.Worker;

namespace MappingDemo.Tests.Worker;

public sealed class ArchivePathValidatorTests
{
    private static readonly string ContentRoot = Path.Combine(
        Path.GetTempPath(),
        "mapping-demo-path-tests");

    [Fact]
    public void Validate_throws_when_archive_root_equals_input_root()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ArchivePathValidator.Validate(
                "input",
                "input",
                ContentRoot));

        Assert.Contains("Paths:ArchiveRoot", exception.Message);
    }

    [Fact]
    public void Validate_throws_when_archive_root_is_inside_input_root()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ArchivePathValidator.Validate(
                "input",
                Path.Combine("input", "archive"),
                ContentRoot));
    }

    [Fact]
    public void Validate_allows_sibling_with_similar_name()
    {
        var exception = Record.Exception(() =>
            ArchivePathValidator.Validate(
                "input",
                "input-backup",
                ContentRoot));

        Assert.Null(exception);
    }
}
