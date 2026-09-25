namespace MappingDemo.Shared.MappingConfigs;

public sealed record SourceToNormalizedRule(
    string SourceColumn,
    string NormalizedColumn,
    string? Format = null);
