namespace MappingDemo.Shared.DatabaseDestinations;

public static class TargetSchemaCompatibilityAnalyzer
{
    public static SchemaCompatibilityResult Compare(
        TargetSchemaSnapshot configured,
        TargetTableSchema current,
        IReadOnlySet<string> mappedColumns,
        IReadOnlyList<string> selectedUniqueKeyColumns)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(mappedColumns);
        ArgumentNullException.ThrowIfNull(selectedUniqueKeyColumns);

        var changes = new List<TargetSchemaChange>();
        if (!SameTarget(configured, current))
        {
            changes.Add(new TargetSchemaChange(
                "TargetChanged",
                null,
                "The inspected target does not match the configured target.",
                IsBreaking: true));
            return Result(configured.SchemaHash, current.SchemaHash, changes);
        }

        var configuredColumns = configured.Columns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);
        var currentColumns = current.Columns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);

        foreach (var oldColumn in configured.Columns)
        {
            if (!currentColumns.TryGetValue(oldColumn.Name, out var newColumn))
            {
                var mapped = mappedColumns.Contains(oldColumn.Name);
                changes.Add(new TargetSchemaChange(
                    mapped ? "MappedColumnRemoved" : "UnmappedColumnRemoved",
                    oldColumn.Name,
                    mapped
                        ? "A mapped target column was removed."
                        : "An unmapped target column was removed.",
                    mapped));
                continue;
            }

            if (!SameColumnContract(oldColumn, newColumn))
            {
                var mapped = mappedColumns.Contains(oldColumn.Name);
                var becameRequired = !oldColumn.IsRequired && newColumn.IsRequired;
                changes.Add(new TargetSchemaChange(
                    mapped ? "MappedColumnChanged" : "UnmappedColumnChanged",
                    oldColumn.Name,
                    mapped
                        ? "A mapped target column changed."
                        : "An unmapped target column changed.",
                    mapped || becameRequired));
            }
        }

        foreach (var column in current.Columns.Where(column =>
                     !configuredColumns.ContainsKey(column.Name)))
        {
            changes.Add(new TargetSchemaChange(
                column.IsRequired
                    ? "RequiredColumnAdded"
                    : "ColumnAddedCompatible",
                column.Name,
                column.IsRequired
                    ? "A required target column without a default was added."
                    : "A nullable, defaulted or generated target column was added.",
                column.IsRequired));
        }

        if (selectedUniqueKeyColumns.Count > 0 &&
            !current.UniqueConstraints.Any(constraint =>
                constraint.Columns.SequenceEqual(
                    selectedUniqueKeyColumns,
                    StringComparer.Ordinal)))
        {
            changes.Add(new TargetSchemaChange(
                "SelectedUniqueKeyChanged",
                null,
                "The selected unique key no longer exists with the same columns.",
                IsBreaking: true));
        }

        return Result(configured.SchemaHash, current.SchemaHash, changes);
    }

    public static SchemaCompatibilityResult Unavailable(
        TargetSchemaSnapshot configured) =>
        new(
            SchemaCompatibilityStatus.Unavailable,
            [new TargetSchemaChange(
                "SchemaUnavailable",
                null,
                "The target schema is unavailable.",
                IsBreaking: true)],
            configured.SchemaHash,
            CurrentHash: null);

    private static bool SameTarget(
        TargetSchemaSnapshot configured,
        TargetTableSchema current) =>
        configured.Provider == current.Provider &&
        string.Equals(configured.ProfileId, current.ProfileId, StringComparison.Ordinal) &&
        string.Equals(configured.Schema, current.Schema, StringComparison.Ordinal) &&
        string.Equals(configured.Table, current.Table, StringComparison.Ordinal);

    private static bool SameColumnContract(
        TargetColumnSchema oldColumn,
        TargetColumnSchema newColumn) =>
        string.Equals(
            oldColumn.ProviderType,
            newColumn.ProviderType,
            StringComparison.Ordinal) &&
        oldColumn.ScalarType == newColumn.ScalarType &&
        oldColumn.IsNullable == newColumn.IsNullable &&
        oldColumn.HasDefault == newColumn.HasDefault &&
        oldColumn.IsIdentity == newColumn.IsIdentity &&
        oldColumn.IsGenerated == newColumn.IsGenerated &&
        oldColumn.IsComputed == newColumn.IsComputed &&
        oldColumn.IsWritable == newColumn.IsWritable &&
        oldColumn.Length == newColumn.Length &&
        oldColumn.Precision == newColumn.Precision &&
        oldColumn.Scale == newColumn.Scale;

    private static SchemaCompatibilityResult Result(
        string configuredHash,
        string currentHash,
        IReadOnlyList<TargetSchemaChange> changes)
    {
        var status = changes.Any(change => change.IsBreaking)
            ? SchemaCompatibilityStatus.BreakingChange
            : changes.Count > 0
                ? SchemaCompatibilityStatus.CompatibleChange
                : SchemaCompatibilityStatus.Healthy;
        return new SchemaCompatibilityResult(
            status,
            changes,
            configuredHash,
            currentHash);
    }
}
