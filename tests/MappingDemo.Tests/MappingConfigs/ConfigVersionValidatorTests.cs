using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class ConfigVersionValidatorTests
{
    [Fact]
    public void Validate_rejects_missing_file_to_source_column()
    {
        var fileToSource = ValidFileToSourceRules();
        fileToSource[0] = fileToSource[0] with
        {
            SourceColumn = "missing_column"
        };

        var errors = ConfigVersionValidator.Validate(
            fileToSource,
            ValidSourceToNormalizedRules(),
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field == "fileToSource[0].sourceColumn" &&
                     error.Message == "Source column does not exist.");
    }

    [Fact]
    public void Validate_rejects_missing_source_to_normalized_source_column()
    {
        var sourceToNormalized = ValidSourceToNormalizedRules();
        sourceToNormalized[0] = sourceToNormalized[0] with
        {
            SourceColumn = "missing_column"
        };

        var errors = ConfigVersionValidator.Validate(
            ValidFileToSourceRules(),
            sourceToNormalized,
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field ==
                         "sourceToNormalized[0].sourceColumn" &&
                     error.Message == "Source column does not exist.");
    }

    [Fact]
    public void Validate_rejects_missing_normalized_column()
    {
        var sourceToNormalized = ValidSourceToNormalizedRules();
        sourceToNormalized[0] = sourceToNormalized[0] with
        {
            NormalizedColumn = "missing_column"
        };

        var errors = ConfigVersionValidator.Validate(
            ValidFileToSourceRules(),
            sourceToNormalized,
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field ==
                         "sourceToNormalized[0].normalizedColumn" &&
                     error.Message == "Normalized column does not exist.");
    }

    [Fact]
    public void Validate_rejects_duplicate_csv_headers()
    {
        var fileToSource = ValidFileToSourceRules();
        fileToSource[1] = fileToSource[1] with
        {
            CsvHeader = fileToSource[0].CsvHeader
        };

        var errors = ConfigVersionValidator.Validate(
            fileToSource,
            ValidSourceToNormalizedRules(),
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field == "fileToSource[1].csvHeader" &&
                     error.Message == "CSV headers must be unique.");
    }

    [Fact]
    public void Validate_rejects_duplicate_normalized_column_mappings()
    {
        var sourceToNormalized = ValidSourceToNormalizedRules();
        sourceToNormalized[1] = sourceToNormalized[1] with
        {
            NormalizedColumn = sourceToNormalized[0].NormalizedColumn
        };

        var errors = ConfigVersionValidator.Validate(
            ValidFileToSourceRules(),
            sourceToNormalized,
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field ==
                         "sourceToNormalized[1].normalizedColumn" &&
                     error.Message ==
                         "Normalized columns can only be mapped once.");
    }

    [Fact]
    public void Validate_requires_every_required_normalized_column_to_be_mapped()
    {
        var sourceToNormalized = ValidSourceToNormalizedRules();
        sourceToNormalized.RemoveAt(0);

        var errors = ConfigVersionValidator.Validate(
            ValidFileToSourceRules(),
            sourceToNormalized,
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field == "sourceToNormalized" &&
                     error.Message ==
                         "Required normalized column 'order_no' must be mapped.");
    }

    [Fact]
    public void Validate_rejects_invalid_date_format()
    {
        var sourceToNormalized = ValidSourceToNormalizedRules();
        sourceToNormalized[1] = sourceToNormalized[1] with
        {
            Format = "yyyy-MM-dd'"
        };

        var errors = ConfigVersionValidator.Validate(
            ValidFileToSourceRules(),
            sourceToNormalized,
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field == "sourceToNormalized[1].format" &&
                     error.Message == "Date format is invalid.");
    }

    private static List<FileToSourceRule> ValidFileToSourceRules()
    {
        return
        [
            new FileToSourceRule("Order No", "order_no"),
            new FileToSourceRule("Order Date", "order_date"),
            new FileToSourceRule("Amount", "amount")
        ];
    }

    private static List<SourceToNormalizedRule>
        ValidSourceToNormalizedRules()
    {
        return
        [
            new SourceToNormalizedRule("order_no", "order_no"),
            new SourceToNormalizedRule(
                "order_date",
                "order_date",
                "yyyy-MM-dd"),
            new SourceToNormalizedRule("amount", "amount")
        ];
    }

    private static TableDefinition SourceTable()
    {
        return new TableDefinition(
            "src_orders",
            TableKind.Source,
            [
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 1),
                new ColumnDefinition(
                    "order_date",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 3)
            ]);
    }

    private static TableDefinition NormalizedTable()
    {
        return new TableDefinition(
            "norm_orders",
            TableKind.Normalized,
            [
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: true,
                    Ordinal: 1),
                new ColumnDefinition(
                    "order_date",
                    ColumnDataType.Date,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Decimal,
                    IsRequired: false,
                    Ordinal: 3)
            ]);
    }
}
