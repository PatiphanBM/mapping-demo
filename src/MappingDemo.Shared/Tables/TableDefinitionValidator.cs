namespace MappingDemo.Shared.Tables;

public static class TableDefinitionValidator
{
    public static IReadOnlyList<TableValidationError> Validate(
        TableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<TableValidationError>();

        if (!IdentifierRules.IsValid(definition.Name))
        {
            errors.Add(new TableValidationError(
                "name",
                "Table name must be a valid identifier."));
        }

        if (definition.Columns.Count == 0)
        {
            errors.Add(new TableValidationError(
                "columns",
                "At least one column is required."));
        }

        var columnNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < definition.Columns.Count; index++)
        {
            var column = definition.Columns[index];
            var fieldPrefix = $"columns[{index}]";

            if (!IdentifierRules.IsValid(column.Name))
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.name",
                    "Column name must be a valid identifier."));
            }

            if (!columnNames.Add(column.Name))
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.name",
                    "Column names must be unique."));
            }

            ValidateTypeMetadata(column, fieldPrefix, errors);

            if (definition.Kind != TableKind.Source)
            {
                continue;
            }

            if (column.DataType != ColumnDataType.Text)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.dataType",
                    "Source columns must use the Text data type."));
            }

            if (column.IsRequired)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.isRequired",
                    "Source columns cannot be required."));
            }
        }

        return errors;
    }

    private static void ValidateTypeMetadata(
        ColumnDefinition column,
        string fieldPrefix,
        ICollection<TableValidationError> errors)
    {
        if (column.Length is not null)
        {
            if (column.DataType != ColumnDataType.Text)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.length",
                    "Length is only valid for Text columns."));
            }
            else if (column.Length <= 0)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.length",
                    "Length must be greater than zero."));
            }
        }

        if (column.Precision is not null)
        {
            if (column.DataType != ColumnDataType.Decimal)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.precision",
                    "Precision is only valid for Decimal columns."));
            }
            else if (column.Precision is <= 0 or > 1000)
            {
                errors.Add(new TableValidationError(
                    $"{fieldPrefix}.precision",
                    "Precision must be between 1 and 1000."));
            }
        }

        if (column.Scale is null)
        {
            return;
        }

        if (column.DataType != ColumnDataType.Decimal)
        {
            errors.Add(new TableValidationError(
                $"{fieldPrefix}.scale",
                "Scale is only valid for Decimal columns."));
        }
        else if (column.Precision is null)
        {
            errors.Add(new TableValidationError(
                $"{fieldPrefix}.scale",
                "Scale requires precision."));
        }
        else if (column.Scale < 0 || column.Scale > column.Precision)
        {
            errors.Add(new TableValidationError(
                $"{fieldPrefix}.scale",
                "Scale must be between zero and precision."));
        }
    }
}
