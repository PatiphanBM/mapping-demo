namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigResponse(
    long Id,
    string Name,
    string InputFolder,
    long SourceTableId,
    long NormalizedTableId,
    long? ActiveVersionId);
