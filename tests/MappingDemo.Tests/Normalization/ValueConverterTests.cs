using MappingDemo.Shared.Normalization;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Normalization;

public sealed class ValueConverterTests
{
    [Fact]
    public void Convert_returns_trimmed_text()
    {
        var result = ValueConverter.Convert(
            "  order-001  ",
            ColumnDataType.Text,
            isRequired: true);

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Equal("order-001", success.Value);
    }

    [Fact]
    public void Convert_returns_date_using_the_default_format()
    {
        var result = ValueConverter.Convert(
            " 2026-01-31 ",
            ColumnDataType.Date,
            isRequired: true);

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Equal(new DateOnly(2026, 1, 31), success.Value);
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("31/01/2026")]
    public void Convert_returns_failure_for_invalid_date(string value)
    {
        var result = ValueConverter.Convert(
            value,
            ColumnDataType.Date,
            isRequired: true);

        var failure = Assert.IsType<ConversionResult.Failure>(result);
        Assert.Equal("InvalidDate", failure.Reason);
    }

    [Fact]
    public void Convert_returns_date_using_the_configured_format()
    {
        var result = ValueConverter.Convert(
            "31/01/2026",
            ColumnDataType.Date,
            isRequired: true,
            format: "dd/MM/yyyy");

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Equal(new DateOnly(2026, 1, 31), success.Value);
    }

    public static TheoryData<string, decimal> ValidDecimals => new()
    {
        { "1234.50", 1234.50m },
        { "-3", -3m }
    };

    [Theory]
    [MemberData(nameof(ValidDecimals))]
    public void Convert_returns_decimal(string value, decimal expected)
    {
        var result = ValueConverter.Convert(
            value,
            ColumnDataType.Decimal,
            isRequired: true);

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Equal(expected, success.Value);
    }

    [Theory]
    [InlineData("1,234.50")]
    [InlineData("12a")]
    public void Convert_returns_failure_for_invalid_decimal(string value)
    {
        var result = ValueConverter.Convert(
            value,
            ColumnDataType.Decimal,
            isRequired: true);

        var failure = Assert.IsType<ConversionResult.Failure>(result);
        Assert.Equal("InvalidDecimal", failure.Reason);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Convert_returns_boolean(string value, bool expected)
    {
        var result = ValueConverter.Convert(
            value,
            ColumnDataType.Boolean,
            isRequired: true);

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Equal(expected, success.Value);
    }

    [Fact]
    public void Convert_returns_failure_for_invalid_boolean()
    {
        var result = ValueConverter.Convert(
            "yes",
            ColumnDataType.Boolean,
            isRequired: true);

        var failure = Assert.IsType<ConversionResult.Failure>(result);
        Assert.Equal("InvalidBoolean", failure.Reason);
    }

    [Fact]
    public void Convert_returns_required_failure_for_blank_required_value()
    {
        var result = ValueConverter.Convert(
            "  \t ",
            ColumnDataType.Text,
            isRequired: true);

        var failure = Assert.IsType<ConversionResult.Failure>(result);
        Assert.Equal("Required", failure.Reason);
    }

    [Fact]
    public void Convert_returns_null_for_blank_optional_value()
    {
        var result = ValueConverter.Convert(
            "  \t ",
            ColumnDataType.Text,
            isRequired: false);

        var success = Assert.IsType<ConversionResult.Success>(result);
        Assert.Null(success.Value);
    }
}
