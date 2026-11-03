using System.Text.Json.Serialization;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Contracts.MappingConfigs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewMappingConfigVersionRequest
{
    [JsonRequired]
    public required string EvaluationAt { get; init; }

    [JsonRequired]
    public required MappingPreviewSample Sample { get; init; }

    [JsonRequired]
    public required InputLayoutDefinition InputLayout { get; init; }

    [JsonRequired]
    public required TransformationProgram TransformationProgram { get; init; }

    public DestinationDefinition? Destination { get; init; }

    public IReadOnlyList<string>? AcceptedExtensions { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextFilePreviewSample), "textFile")]
[JsonDerivedType(typeof(SourceRowsPreviewSample), "sourceRows")]
public abstract record MappingPreviewSample;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextFilePreviewSample : MappingPreviewSample
{
    [JsonRequired]
    public required string FileName { get; init; }

    [JsonRequired]
    public required string Content { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceRowsPreviewSample : MappingPreviewSample
{
    [JsonRequired]
    public required IReadOnlyList<PreviewSourceRow> Rows { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewSourceRow
{
    [JsonRequired]
    public required IReadOnlyDictionary<string, string?> Values { get; init; }

    public string? RowKey { get; init; }

    public string? FileName { get; init; }

    public int? RowNumber { get; init; }

    public int? PhysicalLineNumber { get; init; }

    public long? SourceRowId { get; init; }
}

public sealed record ValidateMappingConfigVersionResponse(
    bool IsValid,
    IReadOnlyList<MappingDraftDiagnostic> Diagnostics);

public sealed record MappingDraftDiagnostic(
    string Path,
    string Code,
    string Message,
    MappingDraftDiagnosticSeverity Severity);

public enum MappingDraftDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record PreviewMappingConfigVersionResponse(
    DateTimeOffset EvaluationAt,
    IReadOnlyList<MappingPreviewRecordResponse> Records,
    WorkbookPreviewResponse? Workbook = null,
    ContractDeliveryPreviewResponse? Contract = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonFilePreviewResponse? JsonFile = null);

public sealed record JsonFilePreviewResponse(
    string ProfileId,
    string ContractReference,
    string SchemaHash,
    string Formatting,
    string RelativeDisplayPath,
    string FinalJson,
    int ObjectCount,
    long EstimatedUtf8Bytes,
    IReadOnlyList<JsonFilePreviewDiagnosticResponse> Diagnostics);

public sealed record JsonFilePreviewDiagnosticResponse(
    string RowKey,
    string? OutputKey,
    string Path,
    string Code,
    string Message);

public sealed record ContractDeliveryPreviewResponse(
    string ProfileId,
    string ContractReference,
    string SchemaHash,
    string Method,
    string PathTemplate,
    IReadOnlyList<string> AllowedHeaderNames,
    string IdempotencyKeyExample,
    bool SendsRequest,
    IReadOnlyList<DeliveryPreviewOutputResponse> Outputs);

public sealed record DeliveryPreviewOutputResponse(
    string RowKey,
    string OutputKey,
    string? PayloadJson,
    string? PayloadHash,
    IReadOnlyList<ContractDiagnostic> Diagnostics);

public sealed record WorkbookPreviewResponse(
    IReadOnlyList<string> Sheets,
    string SelectedSheet,
    IReadOnlyList<string> Headers);

public sealed class PreviewMappingConfigFileForm
{
    public required IFormFile File { get; init; }

    public required string EvaluationAt { get; init; }

    public required string InputLayoutJson { get; init; }

    public required string TransformationProgramJson { get; init; }

    public string? DestinationJson { get; init; }
}

public sealed record MappingPreviewRecordResponse(
    string RowKey,
    string? RawRecord,
    int? RowNumber,
    int? PhysicalLineNumber,
    InputRecordAction Action,
    string? RecordRule,
    IReadOnlyDictionary<string, string?> SourceValues,
    TransformationRowResult? Transformation);
