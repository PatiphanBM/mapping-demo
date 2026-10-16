using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Transformation;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EqualExpression), "eq")]
[JsonDerivedType(typeof(NotEqualExpression), "ne")]
[JsonDerivedType(typeof(GreaterThanExpression), "gt")]
[JsonDerivedType(typeof(GreaterThanOrEqualExpression), "gte")]
[JsonDerivedType(typeof(LessThanExpression), "lt")]
[JsonDerivedType(typeof(LessThanOrEqualExpression), "lte")]
[JsonDerivedType(typeof(IsNullExpression), "isNull")]
[JsonDerivedType(typeof(IsBlankExpression), "isBlank")]
[JsonDerivedType(typeof(NotBlankExpression), "notBlank")]
[JsonDerivedType(typeof(LikeExpression), "like")]
[JsonDerivedType(typeof(InExpression), "in")]
[JsonDerivedType(typeof(NotInExpression), "notIn")]
[JsonDerivedType(typeof(AndExpression), "and")]
[JsonDerivedType(typeof(OrExpression), "or")]
[JsonDerivedType(typeof(NotExpression), "not")]
[JsonDerivedType(typeof(ExistsExpression), "exists")]
[JsonDerivedType(typeof(NotExistsExpression), "notExists")]
public abstract record BooleanExpression;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EqualExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotEqualExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GreaterThanExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GreaterThanOrEqualExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessThanExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LessThanOrEqualExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Left { get; init; }

    [JsonRequired]
    public required ValueExpression Right { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IsNullExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IsBlankExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotBlankExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LikeExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required ValueExpression Pattern { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required IReadOnlyList<ValueExpression> Values { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotInExpression : BooleanExpression
{
    [JsonRequired]
    public required ValueExpression Value { get; init; }

    [JsonRequired]
    public required IReadOnlyList<ValueExpression> Values { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AndExpression : BooleanExpression
{
    [JsonRequired]
    public required IReadOnlyList<BooleanExpression> Operands { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrExpression : BooleanExpression
{
    [JsonRequired]
    public required IReadOnlyList<BooleanExpression> Operands { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotExpression : BooleanExpression
{
    [JsonRequired]
    public required BooleanExpression Value { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExistsExpression : BooleanExpression
{
    [JsonRequired]
    public required string Lookup { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, ValueExpression> Arguments
    {
        get;
        init;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotExistsExpression : BooleanExpression
{
    [JsonRequired]
    public required string Lookup { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, ValueExpression> Arguments
    {
        get;
        init;
    }
}
