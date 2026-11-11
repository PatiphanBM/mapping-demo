using System.Text.Json.Serialization;
using MappingDemo.Shared.DatabaseDestinations;

namespace MappingDemo.Shared.Destinations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LegacyTableDestination), "legacyTable")]
[JsonDerivedType(typeof(ApiContractDestination), "apiContract")]
[JsonDerivedType(typeof(JsonFileDestination), "jsonFile")]
[JsonDerivedType(typeof(DatabaseTableDestination), "databaseTable")]
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DatabaseTableDestination(
    string ProfileId,
    string Schema,
    string Table,
    string WriteMode,
    DatabaseDuplicatePolicy DuplicatePolicy,
    string SchemaHash,
    TargetSchemaSnapshot SchemaSnapshot)
    : DestinationDefinition;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ExistingUniqueKeyDuplicatePolicy), "existingUniqueKey")]
[JsonDerivedType(typeof(ManualUnknownOutcomeDuplicatePolicy), "manualUnknownOutcome")]
public abstract record DatabaseDuplicatePolicy;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExistingUniqueKeyDuplicatePolicy(
    IReadOnlyList<string> Columns) : DatabaseDuplicatePolicy;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManualUnknownOutcomeDuplicatePolicy
    : DatabaseDuplicatePolicy;
