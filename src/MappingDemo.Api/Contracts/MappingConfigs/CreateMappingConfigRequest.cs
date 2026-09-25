namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record CreateMappingConfigRequest(
    string Name,
    string InputFolder,
    long SourceTableId,
    long NormalizedTableId);
