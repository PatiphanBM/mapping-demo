namespace MappingDemo.Api.Contracts.FileJobs;

public sealed record RowErrorResponse(
    int RowNumber,
    string? OutputKey,
    string? Field,
    string ExpressionPath,
    string Reason);
