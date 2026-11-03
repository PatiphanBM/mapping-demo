using System.Text.Json.Serialization;

namespace MappingDemo.Api.Contracts.FileJobs;

public sealed record FileJobResponse(
    long Id,
    long ConfigId,
    long ConfigVersionId,
    string FileName,
    string ImportStatus,
    string ArchiveStatus,
    int? TotalRows,
    string NormalizationStatus,
    int PendingRows,
    int DoneRows,
    int FilteredRows,
    int InvalidRows,
    int FailedRows,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? DeliveryStatus = null,
    int PendingDeliveries = 0,
    int DeliveringDeliveries = 0,
    int DeliveredDeliveries = 0,
    int RetryableFailedDeliveries = 0,
    int PermanentFailedDeliveries = 0,
    string? EndToEndStatus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FileExportStatusResponse? Export = null);

public sealed record FileExportStatusResponse(
    string Status,
    int ArtifactRevision,
    string RelativeDisplayPath,
    int? ObjectCount,
    long? ByteCount,
    string? Sha256,
    DateTime? PublishedAt,
    string? BlockReason);
