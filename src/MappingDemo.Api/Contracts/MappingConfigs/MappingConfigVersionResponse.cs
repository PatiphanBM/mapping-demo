using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionResponse(
    long Id,
    long ConfigId,
    int VersionNo,
    IReadOnlyList<FileToSourceRule> FileToSource,
    IReadOnlyList<SourceToNormalizedRule> SourceToNormalized);
