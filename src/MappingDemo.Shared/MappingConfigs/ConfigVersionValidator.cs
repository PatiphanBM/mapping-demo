using System.Globalization;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.MappingConfigs;

public static class ConfigVersionValidator
{
    public static IReadOnlyList<ConfigVersionValidationError> Validate(
        IReadOnlyList<FileToSourceRule> fileToSource,
        IReadOnlyList<SourceToNormalizedRule> sourceToNormalized,
        TableDefinition sourceTable,
        TableDefinition normalizedTable)
    {
        ArgumentNullException.ThrowIfNull(fileToSource);
        ArgumentNullException.ThrowIfNull(sourceToNormalized);
        ArgumentNullException.ThrowIfNull(sourceTable);
        ArgumentNullException.ThrowIfNull(normalizedTable);

        var errors = new List<ConfigVersionValidationError>();
        var sourceColumns = sourceTable.Columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.Ordinal);
        var normalizedColumns = normalizedTable.Columns
            .ToDictionary(column => column.Name, StringComparer.Ordinal);

        ValidateFileToSource(fileToSource, sourceColumns, errors);
        ValidateSourceToNormalized(
            sourceToNormalized,
            sourceColumns,
            normalizedColumns,
            errors);

        return errors;
    }

    private static void ValidateFileToSource(
        IReadOnlyList<FileToSourceRule> rules,
        IReadOnlySet<string> sourceColumns,
        ICollection<ConfigVersionValidationError> errors)
    {
        var csvHeaders = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var fieldPrefix = $"fileToSource[{index}]";

            if (!sourceColumns.Contains(rule.SourceColumn))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.sourceColumn",
                    "Source column does not exist."));
            }

            if (!csvHeaders.Add(rule.CsvHeader))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.csvHeader",
                    "CSV headers must be unique."));
            }
        }
    }

    private static void ValidateSourceToNormalized(
        IReadOnlyList<SourceToNormalizedRule> rules,
        IReadOnlySet<string> sourceColumns,
        IReadOnlyDictionary<string, ColumnDefinition> normalizedColumns,
        ICollection<ConfigVersionValidationError> errors)
    {
        var mappedNormalizedColumns =
            new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var fieldPrefix = $"sourceToNormalized[{index}]";

            if (!sourceColumns.Contains(rule.SourceColumn))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.sourceColumn",
                    "Source column does not exist."));
            }

            if (!normalizedColumns.TryGetValue(
                    rule.NormalizedColumn,
                    out var normalizedColumn))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.normalizedColumn",
                    "Normalized column does not exist."));
                continue;
            }

            if (!mappedNormalizedColumns.Add(rule.NormalizedColumn))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.normalizedColumn",
                    "Normalized columns can only be mapped once."));
            }

            if (normalizedColumn.DataType == ColumnDataType.Date &&
                rule.Format is not null &&
                !IsValidDateFormat(rule.Format))
            {
                errors.Add(new ConfigVersionValidationError(
                    $"{fieldPrefix}.format",
                    "Date format is invalid."));
            }
        }

        foreach (var column in normalizedColumns.Values)
        {
            if (!column.IsRequired ||
                mappedNormalizedColumns.Contains(column.Name))
            {
                continue;
            }

            errors.Add(new ConfigVersionValidationError(
                "sourceToNormalized",
                $"Required normalized column '{column.Name}' must be mapped."));
        }
    }

    private static bool IsValidDateFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        try
        {
            var example = new DateOnly(2000, 1, 2);
            var formatted = example.ToString(
                format,
                CultureInfo.InvariantCulture);

            return DateOnly.TryParseExact(
                formatted,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
