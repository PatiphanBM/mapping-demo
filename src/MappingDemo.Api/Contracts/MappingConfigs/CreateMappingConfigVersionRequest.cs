using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record CreateMappingConfigVersionRequest(
    IReadOnlyList<FileToSourceRule> FileToSource,
    IReadOnlyList<SourceToNormalizedRule> SourceToNormalized);
