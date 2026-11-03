namespace MappingDemo.Shared.Tables;

public enum TableVerificationStatus
{
    Unknown,
    Verifying,
    Healthy,
    Drifted,
    Missing,
    VerificationFailed
}

public sealed record TableSchemaColumn(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsIdentity);

public sealed record TableSchemaConstraint(
    string Kind,
    IReadOnlyList<string> Columns);

public sealed record TableSchemaSnapshot(
    IReadOnlyList<TableSchemaColumn> Columns,
    TableSchemaConstraint PrimaryKey,
    IReadOnlyList<TableSchemaConstraint> UniqueConstraints);

public sealed record TableSchemaDiagnostic(
    string Field,
    string Code,
    string Message,
    string? Expected = null,
    string? Actual = null);

public sealed record TableSchemaVerificationResult(
    TableVerificationStatus Status,
    TableSchemaSnapshot Expected,
    TableSchemaSnapshot? Actual,
    IReadOnlyList<TableSchemaDiagnostic> Diagnostics,
    DateTimeOffset CheckedAt,
    double DurationMilliseconds);

public interface ITableSchemaInspector
{
    Task<TableSchemaVerificationResult> InspectAsync(
        long tableId,
        TableDefinition expectedDefinition,
        CancellationToken cancellationToken = default);
}
