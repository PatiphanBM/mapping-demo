using MappingDemo.Shared.Transformation;

namespace MappingDemo.Shared.Contracts;

public static class ContractCompatibilityAnalyzer
{
    public static ContractCompatibility Compare(
        IReadOnlyList<ContractPathDescriptor>? baseline,
        IReadOnlyList<ContractPathDescriptor> candidate)
    {
        if (baseline is null)
        {
            return ContractCompatibility.Initial;
        }

        var candidateByPath = candidate.ToDictionary(
            path => path.Path,
            StringComparer.Ordinal);
        foreach (var previous in baseline)
        {
            if (!candidateByPath.TryGetValue(previous.Path, out var current))
            {
                return ContractCompatibility.Breaking;
            }
            if (previous.Kind != current.Kind ||
                previous.ScalarType != current.ScalarType ||
                (!previous.IsRequired && current.IsRequired) ||
                (previous.IsNullable && !current.IsNullable) ||
                HasStricterConstraints(previous.Constraints, current.Constraints))
            {
                return ContractCompatibility.Breaking;
            }
        }
        return ContractCompatibility.NonBreaking;
    }

    private static bool HasStricterConstraints(
        TargetConstraints? previous,
        TargetConstraints? current)
    {
        if (previous is null)
        {
            return current is not null &&
                (current.Length is not null ||
                 current.Precision is not null ||
                 current.Scale is not null ||
                 current.Minimum is not null ||
                 current.Maximum is not null ||
                 current.Pattern is not null);
        }
        if (current is null)
        {
            return false;
        }
        return IsLower(current.Length, previous.Length) ||
            IsLower(current.Precision, previous.Precision) ||
            IsLower(current.Scale, previous.Scale) ||
            IsHigher(current.Minimum, previous.Minimum) ||
            IsLower(current.Maximum, previous.Maximum) ||
            (current.Pattern is not null &&
             !string.Equals(
                 current.Pattern,
                 previous.Pattern,
                 StringComparison.Ordinal));
    }

    private static bool IsLower<T>(T? current, T? previous)
        where T : struct, IComparable<T> =>
        current is not null &&
        (previous is null || current.Value.CompareTo(previous.Value) < 0);

    private static bool IsHigher<T>(T? current, T? previous)
        where T : struct, IComparable<T> =>
        current is not null &&
        (previous is null || current.Value.CompareTo(previous.Value) > 0);
}
