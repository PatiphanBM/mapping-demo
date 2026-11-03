using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Destinations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LegacyTableDestination), "legacyTable")]
[JsonDerivedType(typeof(ApiContractDestination), "apiContract")]
[JsonDerivedType(typeof(JsonFileDestination), "jsonFile")]
public abstract record DestinationDefinition;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegacyTableDestination(long NormalizedTableId)
    : DestinationDefinition;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApiContractDestination(
    string ApiDestinationProfileId,
    ContractReference Contract)
    : DestinationDefinition;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JsonFileDestination(
    string FileDestinationProfileId,
    ContractReference Contract,
    string RelativeFolder,
    string FileNameTemplate,
    string Formatting)
    : DestinationDefinition;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContractReference(
    string Id,
    string Version,
    string Reference,
    string SchemaHash);
