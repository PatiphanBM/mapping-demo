using System.Text.Json.Serialization;

namespace MappingDemo.Shared.Input;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DelimitedInputLayout), "delimited")]
[JsonDerivedType(typeof(FixedWidthInputLayout), "fixedWidth")]
public abstract record InputLayoutDefinition;

public sealed record DelimitedInputLayout(
    string Encoding,
    string Delimiter,
    int PreambleLines,
    DelimitedHeaderPolicy HeaderPolicy,
    IReadOnlyList<DelimitedInputField> Fields)
    : InputLayoutDefinition;

[JsonConverter(typeof(JsonStringEnumConverter<DelimitedHeaderPolicy>))]
public enum DelimitedHeaderPolicy
{
    None,
    SingleRow
}

public sealed record DelimitedInputField(
    string InputHeader,
    string SourceColumn);
