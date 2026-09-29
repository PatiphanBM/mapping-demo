using MappingDemo.Shared.Normalization;

namespace MappingDemo.Tests.Normalization;

public sealed class ConversionResultTests
{
    [Fact]
    public void Success_carries_the_converted_value()
    {
        ConversionResult result = new ConversionResult.Success(123.45m);

        var success = Assert.IsType<ConversionResult.Success>(result);

        Assert.Equal(123.45m, success.Value);
    }

    [Fact]
    public void Failure_carries_the_reason()
    {
        ConversionResult result = new ConversionResult.Failure("InvalidDecimal");

        var failure = Assert.IsType<ConversionResult.Failure>(result);

        Assert.Equal("InvalidDecimal", failure.Reason);
    }
}
