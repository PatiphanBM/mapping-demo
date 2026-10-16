namespace MappingDemo.Shared.Transformation;

public sealed record TransformationBatch(
    IReadOnlyList<TransformationSourceRow> Rows);

public sealed record TransformationSourceRow(
    string RowKey,
    IReadOnlyDictionary<string, string?> SourceValues,
    IReadOnlyDictionary<string, object?> MetadataValues,
    DateTimeOffset EvaluationAt,
    long? SourceRowId = null);

public sealed record TransformationBatchResult(
    IReadOnlyList<TransformationRowResult> Rows);

public sealed record TransformationRowResult(
    string RowKey,
    TransformationRowStatus Status,
    IReadOnlyList<TransformationOutputResult> Outputs,
    IReadOnlyList<TransformationRowError> Errors);

public enum TransformationRowStatus
{
    Done,
    Filtered,
    Invalid
}

public sealed record TransformationOutputResult(
    string OutputKey,
    IReadOnlyDictionary<string, object?> Values);

public sealed record TransformationRowError(
    string? OutputKey,
    string? Field,
    string ExpressionPath,
    string Reason);
