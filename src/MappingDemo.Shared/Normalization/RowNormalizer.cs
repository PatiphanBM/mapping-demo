using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Normalization;

public static class RowNormalizer
{
    public static RowNormalizationResult Normalize(
        IReadOnlyDictionary<string, string?> sourceRow,
        IReadOnlyList<SourceToNormalizedRule> rules,
        IReadOnlyList<ColumnDefinition> normalizedColumns)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(normalizedColumns);

        var columnsByName = normalizedColumns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var errors = new List<RowNormalizationError>();

        foreach (var rule in rules)
        {
            if (!sourceRow.TryGetValue(rule.SourceColumn, out var sourceValue))
            {
                throw new InvalidOperationException(
                    $"Source column '{rule.SourceColumn}' is missing from the row.");
            }

            if (!columnsByName.TryGetValue(
                    rule.NormalizedColumn,
                    out var normalizedColumn))
            {
                throw new InvalidOperationException(
                    $"Normalized column '{rule.NormalizedColumn}' is missing from the definition.");
            }

            var conversion = ValueConverter.Convert(
                sourceValue,
                normalizedColumn.DataType,
                normalizedColumn.IsRequired,
                rule.Format);

            switch (conversion)
            {
                case ConversionResult.Success success:
                    values.Add(rule.NormalizedColumn, success.Value);
                    break;
                case ConversionResult.Failure failure:
                    errors.Add(new RowNormalizationError(
                        rule.NormalizedColumn,
                        failure.Reason));
                    break;
            }
        }

        return errors.Count == 0
            ? new RowNormalizationResult.Success(values)
            : new RowNormalizationResult.Failure(errors);
    }
}
