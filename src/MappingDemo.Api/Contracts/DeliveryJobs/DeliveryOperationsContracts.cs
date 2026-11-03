namespace MappingDemo.Api.Contracts.DeliveryJobs;

public sealed record UpdateDeliveryControlRequest(
    bool DeliveryPaused,
    bool ApiContractActivationEnabled,
    string Reason,
    bool? JsonFileActivationEnabled = null,
    bool? JsonFileFinalizerPaused = null);

public sealed record UpdateDeliveryRolloutRequest(
    string Stage,
    bool Enabled,
    string Reason);

public sealed record RetryJsonExportRequest(string Reason);

public sealed record DeliveryControlResponse(
    bool DeliveryPaused,
    bool ApiContractActivationEnabled,
    bool JsonFileActivationEnabled,
    bool JsonFileFinalizerPaused,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    string Reason);

public sealed record DeliveryRolloutResponse(
    long ConfigId,
    string ConfigName,
    string Stage,
    bool Enabled,
    string ApprovedBy,
    string Reason,
    DateTimeOffset UpdatedAt);

public sealed record DeliveryMetricGroupResponse(
    long ConfigVersionId,
    string ContractReference,
    int Total,
    int Pending,
    int Delivering,
    int Delivered,
    int RetryableFailed,
    int PermanentFailed);

public sealed record DeliveryAlertResponse(
    string Severity,
    string Code,
    string Message);

public sealed record JsonExportHistoryResponse(
    long Id,
    long FileJobId,
    long ConfigVersionId,
    string DestinationProfileId,
    int ArtifactRevision,
    string Status,
    string RelativeDisplayPath,
    int? ObjectCount,
    long? ByteCount,
    string? Sha256,
    int AttemptCount,
    string? LastErrorRedacted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    bool CanRetry);

public sealed record JsonExportMetricGroupResponse(
    long ConfigId,
    long ConfigVersionId,
    string DestinationProfileId,
    int Total,
    int Pending,
    int Finalizing,
    int Published,
    int Blocked,
    int RetryableFailed,
    long ObjectCount,
    long ByteCount,
    int RetryCount,
    int HashConflictCount,
    int CleanupFailureCount,
    double OldestQueueAgeSeconds,
    double P95FinalizeLatencyMilliseconds);

public sealed record JsonExportOperationsResponse(
    IReadOnlyList<JsonExportMetricGroupResponse> Groups,
    IReadOnlyList<DeliveryAlertResponse> Alerts);

public sealed record DeliveryOperationsResponse(
    DeliveryControlResponse Control,
    IReadOnlyList<DeliveryRolloutResponse> Rollouts,
    IReadOnlyList<DeliveryMetricGroupResponse> Groups,
    double SuccessRate,
    double RetryableFailureRate,
    double PermanentFailureRate,
    double P50LatencyMilliseconds,
    double P95LatencyMilliseconds,
    double P99LatencyMilliseconds,
    double OldestQueueAgeSeconds,
    int RetryCount,
    int DuplicateConflictCount,
    int SchemaValidationFailureCount,
    double? P95SourceToDeliveredSeconds,
    IReadOnlyList<DeliveryAlertResponse> Alerts,
    JsonExportOperationsResponse JsonExports);
