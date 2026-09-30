namespace MappingDemo.Api.Contracts.FileJobs;

public sealed record RowJobHistoryResponse(
    long Id,
    long ConfigVersionId,
    int VersionNo,
    string Kind,
    string Status,
    int Attempts,
    string? LastError,
    DateTime CreatedAt,
    DateTime? FinishedAt,
    IReadOnlyList<RowErrorResponse> Errors);
