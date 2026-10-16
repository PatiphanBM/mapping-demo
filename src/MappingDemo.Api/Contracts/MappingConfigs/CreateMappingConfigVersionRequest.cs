using System.Text.Json.Serialization;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Contracts.MappingConfigs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateMappingConfigVersionRequest(
    [property: JsonRequired] InputLayoutDefinition InputLayout,
    [property: JsonRequired] TransformationProgram TransformationProgram);
