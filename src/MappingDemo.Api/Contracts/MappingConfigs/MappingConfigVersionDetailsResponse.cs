using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionDetailsResponse(
    long Id,
    int VersionNo,
    bool IsActive,
    InputLayoutDefinition InputLayout,
    TransformationProgram TransformationProgram);
