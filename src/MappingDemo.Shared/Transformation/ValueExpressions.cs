using System.Text.Json;
using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Transformation;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SourceExpression), "source")]
[JsonDerivedType(typeof(MetadataExpression), "metadata")]
[JsonDerivedType(typeof(LiteralExpression), "literal")]
[JsonDerivedType(typeof(CoalesceExpression), "coalesce")]
[JsonDerivedType(typeof(DefaultIfBlankExpression), "defaultIfBlank")]
[JsonDerivedType(typeof(TrimExpression), "trim")]
[JsonDerivedType(typeof(CaseExpression), "case")]
[JsonDerivedType(typeof(SubstringExpression), "substring")]
[JsonDerivedType(typeof(ConvertExpression), "convert")]
[JsonDerivedType(typeof(CurrentDateExpression), "currentDate")]
[JsonDerivedType(typeof(CallExpression), "call")]
public abstract record ValueExpression;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceExpression : ValueExpression
{
    [JsonRequired]
    public required string Field { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataExpression : ValueExpression
{
    [JsonRequired]
    public required string Name { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LiteralExpression : ValueExpression
{
    [JsonRequired]
    public required ExpressionValueType Type { get; init; }

    [JsonRequired]
    public required JsonElement Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CoalesceExpression : ValueExpression
{
    [JsonRequired]
    public required IReadOnlyList<ValueExpression> Values { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DefaultIfBlankExpression : ValueExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required ValueExpression Fallback { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TrimExpression : ValueExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaseExpression : ValueExpression
{
    [JsonRequired]
    public required IReadOnlyList<CaseArm> Cases { get; init; }

    [JsonRequired]
    public required ValueExpression Else { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaseArm
{
    [JsonRequired]
    public required BooleanExpression When { get; init; }

    [JsonRequired]
    public required ValueExpression Then { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubstringExpression : ValueExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required int Start { get; init; }

    [JsonRequired]
    public required int Length { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConvertExpression : ValueExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required ExpressionValueType Type { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFormat { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CurrentDateExpression : ValueExpression;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CallExpression : ValueExpression
{
    [JsonRequired]
    public required string Function { get; init; }

    [JsonRequired]
    public required IReadOnlyList<ValueExpression> Arguments { get; init; }
}

[JsonConverter(typeof(ExpressionValueTypeJsonConverter))]
public enum ExpressionValueType
{
    Text,
    Decimal,
    Integer,
    Date,
    DateTime,
    Boolean,
    Guid
}

internal sealed class ExpressionValueTypeJsonConverter
    : JsonConverter<ExpressionValueType>
{
    public override ExpressionValueType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expression value type must be a string.");
        }

        return reader.GetString() switch
        {
            "text" => ExpressionValueType.Text,
            "decimal" => ExpressionValueType.Decimal,
            "integer" => ExpressionValueType.Integer,
            "date" => ExpressionValueType.Date,
            "dateTime" => ExpressionValueType.DateTime,
            "boolean" => ExpressionValueType.Boolean,
            "guid" => ExpressionValueType.Guid,
            _ => throw new JsonException("Unknown expression value type.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ExpressionValueType value,
        JsonSerializerOptions options)
    {
        var identifier = value switch
        {
            ExpressionValueType.Text => "text",
            ExpressionValueType.Decimal => "decimal",
            ExpressionValueType.Integer => "integer",
            ExpressionValueType.Date => "date",
            ExpressionValueType.DateTime => "dateTime",
            ExpressionValueType.Boolean => "boolean",
            ExpressionValueType.Guid => "guid",
            _ => throw new JsonException("Unknown expression value type.")
        };

        writer.WriteStringValue(identifier);
    }
}
