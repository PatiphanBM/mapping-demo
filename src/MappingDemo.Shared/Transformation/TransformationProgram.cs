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

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ArrayGroup>? ArrayGroups { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldAssignment
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NormalizedColumn { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetPath { get; init; }

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFormat { get; init; }

    [JsonPropertyOrder(2)]
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArrayGroup
{
    [JsonRequired]
    public required string TargetPath { get; init; }

    [JsonRequired]
    public required string SequencePath { get; init; }

    [JsonRequired]
    public required ArrayEmptyItemPolicy EmptyItemPolicy { get; init; }

    [JsonRequired]
    public required IReadOnlyList<ArraySourceSlot> SourceSlots { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ArrayEmptyItemPolicy>))]
public enum ArrayEmptyItemPolicy
{
    SkipWhenAllBusinessFieldsEmpty,
    Retain
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArraySourceSlot
{
    [JsonRequired]
    public required int SourceSlot { get; init; }

    [JsonRequired]
    public required IReadOnlyList<ArrayItemFieldAssignment> Fields { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ArrayItemFieldAssignment
{
    [JsonRequired]
    public required string TargetPath { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFormat { get; init; }

    [JsonRequired]
    public required ValueExpression Value { get; init; }
}
