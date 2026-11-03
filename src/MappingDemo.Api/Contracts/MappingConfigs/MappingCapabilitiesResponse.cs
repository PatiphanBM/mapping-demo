using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public sealed record MappingCapabilitiesResponse(
    InputCapabilitiesResponse Input,
    TransformationCapabilitiesResponse Transformation,
    DestinationCapabilitiesResponse Destinations);

public sealed record DestinationCapabilitiesResponse(
    IReadOnlyList<string> Kinds,
    JsonFileCapabilitiesResponse JsonFile);

public sealed record JsonFileCapabilitiesResponse(
    IReadOnlyList<string> Formatting,
    IReadOnlyList<string> FileNameTokens,
    int MaxPreviewBytes,
    int MaxPreviewRecords,
    int MaxObjectBytes,
    int MaxObjectsPerArtifact,
    long MaxArtifactBytes,
    IReadOnlyList<FileDestinationProfileSummaryResponse> Profiles);

public sealed record FileDestinationProfileSummaryResponse(
    string Id,
    bool Enabled,
    IReadOnlyList<SupportedContract> SupportedContracts,
    IReadOnlyList<PayloadBinding> PayloadBindings);

public sealed record InputCapabilitiesResponse(
    IReadOnlyList<string> Formats,
    IReadOnlyList<string> Encodings,
    IReadOnlyList<string> PositionUnits);

public sealed record TransformationCapabilitiesResponse(
    IReadOnlyList<int> LanguageVersions,
    IReadOnlyList<SemanticsProfileCapabilityResponse> SemanticsProfiles,
    IReadOnlyList<ExpressionValueType> TargetConversionTypes,
    InputFormatCapabilitiesResponse InputFormats,
    IReadOnlyList<string> ValueExpressions,
    IReadOnlyList<string> BooleanOperators,
    IReadOnlyList<string> Functions,
    IReadOnlyList<string> Lookups,
    IReadOnlyList<string> MetadataFields,
    TransformationLimitsResponse Limits);

public sealed record SemanticsProfileCapabilityResponse(
    string Id,
    bool Immutable,
    string Summary);

public sealed record InputFormatCapabilitiesResponse(
    IReadOnlyList<string> Date,
    IReadOnlyList<string> Time,
    IReadOnlyList<string> DateTime);

public sealed record TransformationLimitsResponse(
    int MaxOutputs,
    int MaxFieldsPerOutput,
    int MaxCollectionItems,
    int MaxExpressionDepth,
    int MaxExpressionNodes);
