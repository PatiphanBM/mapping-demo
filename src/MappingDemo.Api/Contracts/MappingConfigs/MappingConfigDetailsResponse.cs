namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigDetailsResponse(
    long Id,
    string Name,
    string InputFolder,
    long SourceTableId,
    long NormalizedTableId,
    long? ActiveVersionId,
    IReadOnlyList<MappingConfigVersionDetailsResponse> Versions);
