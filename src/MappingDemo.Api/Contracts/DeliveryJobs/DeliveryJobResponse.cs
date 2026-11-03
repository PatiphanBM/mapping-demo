namespace MappingDemo.Api.Contracts.DeliveryJobs;

public sealed record DeliveryJobResponse(
    long Id,
    long FileJobId,
    long RowJobId,
    int SourceRowNumber,
    long ConfigVersionId,
    string OutputKey,
    string OperationKind,
    string DestinationProfileId,
    string ContractReference,
    string SchemaHash,
    string PayloadHash,
    string Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    int? HttpStatus,
    string? CorrelationId,
    string? LastErrorRedacted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeliveredAt,
    bool CanRetry);
