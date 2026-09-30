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
    int InvalidRows,
    int FailedRows,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt);
