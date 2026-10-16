using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Transformation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransformationProgram
{
    [JsonPropertyOrder(0)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LanguageVersion { get; init; }

    [JsonPropertyOrder(1)]
    [JsonRequired]
    public required string SemanticsProfile { get; init; }

    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BooleanExpression? SourceWhen { get; init; }

    [JsonPropertyOrder(3)]
    [JsonRequired]
    public required IReadOnlyList<TransformationOutput> Outputs { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransformationOutput
{
    [JsonRequired]
    public required string OutputKey { get; init; }

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BooleanExpression? EmitWhen { get; init; }

    [JsonPropertyOrder(2)]
    [JsonRequired]
    public required IReadOnlyList<FieldAssignment> Fields { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldAssignment
{
    [JsonRequired]
    public required string NormalizedColumn { get; init; }

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFormat { get; init; }

    [JsonPropertyOrder(2)]
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}
