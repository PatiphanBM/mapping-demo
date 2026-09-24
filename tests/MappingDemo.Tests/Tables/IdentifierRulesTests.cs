using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Tables;

public sealed class IdentifierRulesTests
{
    [Theory]
    [InlineData("orders")]
    [InlineData("order_no")]
    public void IsValid_returns_true_for_valid_identifier(string identifier)
    {
        Assert.True(IdentifierRules.IsValid(identifier));
    }

    [Theory]
    [InlineData("Orders")]
    [InlineData("1abc")]
    [InlineData("a-b")]
    [InlineData("a;drop")]
    [InlineData("pg_orders")]
    [InlineData("id")]
    [InlineData("file_job_id")]
    [InlineData("row_number")]
    [InlineData("imported_at")]
    [InlineData("source_row_id")]
    [InlineData("row_job_id")]
    [InlineData("config_version_id")]
    [InlineData("normalized_at")]
    public void IsValid_returns_false_for_invalid_identifier(string identifier)
    {
        Assert.False(IdentifierRules.IsValid(identifier));
    }

    [Fact]
    public void IsValid_returns_false_when_identifier_exceeds_63_characters()
    {
        var identifier = new string('a', 64);

        Assert.False(IdentifierRules.IsValid(identifier));
    }

    [Fact]
    public void IsValid_returns_true_when_identifier_has_exactly_63_characters()
    {
        var identifier = new string('a', 63);

        Assert.True(IdentifierRules.IsValid(identifier));
    }
}
