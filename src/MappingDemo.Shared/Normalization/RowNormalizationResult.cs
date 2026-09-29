namespace MappingDemo.Shared.Normalization;

public abstract record RowNormalizationResult
{
    private RowNormalizationResult()
    {
    }

    public sealed record Success(
        IReadOnlyDictionary<string, object?> Values)
        : RowNormalizationResult;

    public sealed record Failure(
        IReadOnlyList<RowNormalizationError> Errors)
        : RowNormalizationResult;
}
