using System.Text.Json.Serialization;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingConfigResponse(
    long Id,
    string Name,
    string InputFolder,
    long SourceTableId,
    long? NormalizedTableId,
    long? ActiveVersionId,
    DestinationDefinition? Destination = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FileDestinationProfileSummaryResponse? FileDestinationProfile = null);
