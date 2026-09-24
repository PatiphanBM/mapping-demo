using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Tables;

public sealed class SqlIdentifierTests
{
    [Theory]
    [InlineData("orders", "\"orders\"")]
    [InlineData("order\"name", "\"order\"\"name\"")]
    public void Quote_wraps_identifier_and_escapes_double_quotes(
        string identifier,
        string expected)
    {
        var quoted = SqlIdentifier.Quote(identifier);

        Assert.Equal(expected, quoted);
    }
}
