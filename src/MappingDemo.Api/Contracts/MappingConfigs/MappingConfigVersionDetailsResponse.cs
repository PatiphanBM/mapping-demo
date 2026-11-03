using System.Text.Json.Serialization;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionDetailsResponse(
    long Id,
    int VersionNo,
    bool IsActive,
    InputLayoutDefinition InputLayout,
    TransformationProgram TransformationProgram,
    DestinationDefinition Destination,
    IReadOnlyList<string>? AcceptedExtensions = null,
    ContractSnapshotResponse? ContractSnapshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FileDestinationProfileSummaryResponse? FileDestinationProfile = null);
