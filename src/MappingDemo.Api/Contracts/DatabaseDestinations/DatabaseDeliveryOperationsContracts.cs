namespace MappingDemo.Api.Contracts.DatabaseDestinations;

public sealed record DatabaseDeliveryResolutionRequest(
    string Reason,
    string CorrelationId,
    bool AcceptDuplicateRisk = false);

public sealed record DatabaseDeliveryOperationsResponse(
    DatabaseDeliveryMetricResponse Metrics,
    IReadOnlyList<DatabaseDeliveryJobResponse> Jobs,
    IReadOnlyList<DatabaseSchemaCheckResponse> SchemaChecks,
    IReadOnlyList<DatabaseDeliveryAlertResponse> Alerts);

public sealed record DatabaseDeliveryMetricResponse(
    int Total,
    int Pending,
    int Delivering,
    int Delivered,
    int AlreadyDelivered,
    int RetryableFailed,
    int PermanentFailed,
    int DeliveryConflict,
    int BlockedSchemaDrift,
    int UnknownOutcome,
    double OldestQueueAgeSeconds);

public sealed record DatabaseDeliveryJobResponse(
    long Id,
    long FileJobId,
    long ConfigVersionId,
    int SourceRowNumber,
    string OutputKey,
    string DestinationProfileId,
    string Provider,
    string TargetSchema,
    string TargetTable,
    string TargetSchemaHash,
    string MappedValuesHash,
    string Status,
    int Attempts,
    string? CorrelationId,
    string? LastErrorRedacted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasExistingUniqueKey,
    bool CanRetry,
    bool CanMarkDelivered,
    bool CanMarkFailed,
    bool RetryRequiresDuplicateRiskAcceptance);

public sealed record DatabaseSchemaCheckResponse(
    string DestinationProfileId,
    string TargetSchema,
    string TargetTable,
    string ConfiguredSchemaHash,
    string? CurrentSchemaHash,
    string Compatibility,
    string DiagnosticsJson,
    DateTimeOffset CheckedAt);

public sealed record DatabaseDeliveryAlertResponse(
    string Severity,
    string Code,
    string Message);

public enum DatabaseDeliveryResolutionResult
{
    Updated,
    NotFound,
    StateConflict,
    DuplicateRiskAcceptanceRequired
}
