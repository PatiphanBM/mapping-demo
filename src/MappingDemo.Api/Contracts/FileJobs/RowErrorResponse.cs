namespace MappingDemo.Api.Contracts.FileJobs;

public sealed record RowErrorResponse(
    int RowNumber,
    string Field,
    string Reason);
