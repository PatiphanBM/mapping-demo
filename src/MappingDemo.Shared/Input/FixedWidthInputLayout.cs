using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Input;

public sealed record FixedWidthInputLayout(
    string Encoding,
    string PositionUnit,
    IReadOnlyList<FixedWidthRecordRule> RecordRules,
    IReadOnlyList<InputRecordSequenceItem> RecordSequence,
    UnmatchedRecordPolicy UnmatchedRecordPolicy)
    : InputLayoutDefinition;

public sealed record FixedWidthRecordRule(
    string Key,
    FixedWidthDiscriminator Discriminator,
    InputRecordAction Action,
    int ExpectedLength,
    IReadOnlyList<FixedWidthInputField> Fields);

public sealed record FixedWidthDiscriminator(
    int Start,
    int Length,
    string Value);

public sealed record FixedWidthInputField(
    string SourceColumn,
    int Start,
    int Length);

public sealed record InputRecordSequenceItem(
    string RecordRule,
    int MinOccurs,
    int? MaxOccurs);

[JsonConverter(typeof(JsonStringEnumConverter<InputRecordAction>))]
public enum InputRecordAction
{
    Import,
    Ignore
}

[JsonConverter(typeof(JsonStringEnumConverter<UnmatchedRecordPolicy>))]
public enum UnmatchedRecordPolicy
{
    Error
}
