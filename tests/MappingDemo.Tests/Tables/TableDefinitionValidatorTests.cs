using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Tables;

public sealed class TableDefinitionValidatorTests
{
    [Fact]
    public void Validate_returns_no_errors_for_valid_source_definition()
    {
        var definition = CreateDefinition(
            TableKind.Source,
            new ColumnDefinition(
                "order_no",
                ColumnDataType.Text,
                IsRequired: false,
                Ordinal: 1));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_rejects_invalid_table_name()
    {
        var definition = CreateDefinition(
            TableKind.Source,
            new ColumnDefinition("order_no", ColumnDataType.Text, false, 1))
            with
            {
                Name = "Bad-Name"
            };

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "name",
                "Table name must be a valid identifier."),
            errors);
    }

    [Fact]
    public void Validate_requires_at_least_one_column()
    {
        var definition = new TableDefinition(
            "src_orders",
            TableKind.Source,
            []);

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "columns",
                "At least one column is required."),
            errors);
    }

    [Fact]
    public void Validate_rejects_invalid_column_name()
    {
        var definition = CreateDefinition(
            TableKind.Normalized,
            new ColumnDefinition("Order-No", ColumnDataType.Text, false, 1));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "columns[0].name",
                "Column name must be a valid identifier."),
            errors);
    }

    [Fact]
    public void Validate_rejects_duplicate_column_names()
    {
        var definition = CreateDefinition(
            TableKind.Normalized,
            new ColumnDefinition("order_no", ColumnDataType.Text, false, 1),
            new ColumnDefinition("order_no", ColumnDataType.Text, false, 2));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "columns[1].name",
                "Column names must be unique."),
            errors);
    }

    [Fact]
    public void Validate_requires_source_columns_to_use_text()
    {
        var definition = CreateDefinition(
            TableKind.Source,
            new ColumnDefinition("amount", ColumnDataType.Decimal, false, 1));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "columns[0].dataType",
                "Source columns must use the Text data type."),
            errors);
    }

    [Fact]
    public void Validate_rejects_required_source_columns()
    {
        var definition = CreateDefinition(
            TableKind.Source,
            new ColumnDefinition("order_no", ColumnDataType.Text, true, 1));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Contains(
            new TableValidationError(
                "columns[0].isRequired",
                "Source columns cannot be required."),
            errors);
    }

    [Fact]
    public void Validate_allows_required_typed_normalized_columns()
    {
        var definition = CreateDefinition(
            TableKind.Normalized,
            new ColumnDefinition("amount", ColumnDataType.Decimal, true, 1));

        var errors = TableDefinitionValidator.Validate(definition);

        Assert.Empty(errors);
    }

    private static TableDefinition CreateDefinition(
        TableKind kind,
        params ColumnDefinition[] columns)
    {
        return new TableDefinition("src_orders", kind, columns);
    }
}
