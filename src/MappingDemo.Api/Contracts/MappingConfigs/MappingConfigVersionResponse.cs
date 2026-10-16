using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionResponse(
    long Id,
    long ConfigId,
    int VersionNo,
    InputLayoutDefinition InputLayout,
    TransformationProgram TransformationProgram);
