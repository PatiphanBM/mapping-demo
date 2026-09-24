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
}
