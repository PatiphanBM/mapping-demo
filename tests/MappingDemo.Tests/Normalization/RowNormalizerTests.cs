using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Normalization;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Normalization;

public sealed class RowNormalizerTests
{
    [Fact]
    public void Normalize_returns_all_converted_values_for_a_valid_row()
    {
        var sourceRow = new Dictionary<string, string?>
        {
            ["order_no"] = " A-001 ",
            ["order_date"] = "2026-01-31",
            ["amount"] = "1234.50",
            ["is_paid"] = "1"
        };

        var result = RowNormalizer.Normalize(
            sourceRow,
            Rules(),
            Columns());

        var success = Assert.IsType<RowNormalizationResult.Success>(result);
        Assert.Equal("A-001", success.Values["order_no"]);
        Assert.Equal(
            new DateOnly(2026, 1, 31),
            success.Values["order_date"]);
        Assert.Equal(1234.50m, success.Values["amount"]);
        Assert.Equal(true, success.Values["is_paid"]);
    }

    [Fact]
    public void Normalize_returns_every_field_error_for_an_invalid_row()
    {
        var sourceRow = new Dictionary<string, string?>
        {
            ["order_no"] = "A-001",
            ["order_date"] = "2026-02-30",
            ["amount"] = "1,234.50",
            ["is_paid"] = "false"
        };

        var result = RowNormalizer.Normalize(
            sourceRow,
            Rules(),
            Columns());

        var failure = Assert.IsType<RowNormalizationResult.Failure>(result);
        Assert.Equal(
            [
                new RowNormalizationError("order_date", "InvalidDate"),
                new RowNormalizationError("amount", "InvalidDecimal")
            ],
            failure.Errors);
    }

    private static SourceToNormalizedRule[] Rules()
    {
        return
        [
            new SourceToNormalizedRule("order_no", "order_no"),
            new SourceToNormalizedRule("order_date", "order_date"),
            new SourceToNormalizedRule("amount", "amount"),
            new SourceToNormalizedRule("is_paid", "is_paid")
        ];
    }

    private static ColumnDefinition[] Columns()
    {
        return
        [
            new ColumnDefinition(
                "order_no",
                ColumnDataType.Text,
                IsRequired: true,
                Ordinal: 1),
            new ColumnDefinition(
                "order_date",
                ColumnDataType.Date,
                IsRequired: true,
                Ordinal: 2),
            new ColumnDefinition(
                "amount",
                ColumnDataType.Decimal,
                IsRequired: true,
                Ordinal: 3),
            new ColumnDefinition(
                "is_paid",
                ColumnDataType.Boolean,
                IsRequired: true,
                Ordinal: 4)
        ];
    }
}
