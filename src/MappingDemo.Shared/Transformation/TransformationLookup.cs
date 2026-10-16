namespace MappingDemo.Shared.Transformation;

public interface ITransformationLookupResolver
{
    ValueTask<TransformationLookupValidationResult> ValidateAsync(
        string capability,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TransformationLookupMatch>> ResolveAsync(
        IReadOnlyList<TransformationLookupRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed record TransformationLookupRequest(
    string Capability,
    string SystemId,
    string ProductName,
    string LoanNumber);

public sealed record TransformationLookupMatch(
    TransformationLookupRequest Request,
    long SourceRowId);

public sealed record TransformationLookupValidationResult(
    bool IsValid,
    string? Error);
