using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionDetailsResponse(
    long Id,
    int VersionNo,
    bool IsActive,
    IReadOnlyList<FileToSourceRule> FileToSource,
    IReadOnlyList<SourceToNormalizedRule> SourceToNormalized);
