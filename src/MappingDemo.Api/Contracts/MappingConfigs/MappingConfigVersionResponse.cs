using System.Text.Json.Serialization;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigVersionResponse(
    long Id,
    long ConfigId,
    int VersionNo,
    InputLayoutDefinition InputLayout,
    TransformationProgram TransformationProgram,
    DestinationDefinition Destination,
    IReadOnlyList<string>? AcceptedExtensions = null,
    ContractSnapshotResponse? ContractSnapshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FileDestinationProfileSummaryResponse? FileDestinationProfile = null);

public sealed record ContractSnapshotResponse(
    string CanonicalSchema,
    string SchemaHash,
    DateTimeOffset FetchedAt,
    IReadOnlyList<ContractPathDescriptor> Paths,
    IReadOnlyList<PayloadBinding> PayloadBindings);
