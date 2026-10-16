using System.Text.Json;
using MappingDemo.Shared.Input;

namespace MappingDemo.Tests.Input;

public sealed class InputLayoutSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Delimited_layout_round_trips_with_an_explicit_discriminator()
    {
        InputLayoutDefinition layout = new DelimitedInputLayout(
            Encoding: "utf-8",
            Delimiter: ",",
            PreambleLines: 2,
            HeaderPolicy: DelimitedHeaderPolicy.SingleRow,
            Fields:
            [
                new DelimitedInputField("Order No", "order_no")
            ]);

        var json = JsonSerializer.Serialize(layout, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<InputLayoutDefinition>(
            json,
            JsonOptions);

        Assert.Equal(
            "{\"kind\":\"delimited\",\"encoding\":\"utf-8\"," +
            "\"delimiter\":\",\",\"preambleLines\":2," +
            "\"headerPolicy\":\"SingleRow\",\"fields\":[{" +
            "\"inputHeader\":\"Order No\"," +
            "\"sourceColumn\":\"order_no\"}]}",
            json);
        var delimited = Assert.IsType<DelimitedInputLayout>(deserialized);
        Assert.Equal(2, delimited.PreambleLines);
        Assert.Equal("order_no", delimited.Fields.Single().SourceColumn);
    }

    [Fact]
    public void Fixed_width_layout_round_trips_with_an_explicit_discriminator()
    {
        InputLayoutDefinition layout = new FixedWidthInputLayout(
            Encoding: "utf-8",
            PositionUnit: "utf16CodeUnit",
            RecordRules:
            [
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    ExpectedLength: 10,
                    Fields:
                    [
                        new FixedWidthInputField("order_no", 2, 9)
                    ])
            ],
            RecordSequence:
            [
                new InputRecordSequenceItem(
                    "detail",
                    MinOccurs: 1,
                    MaxOccurs: null)
            ],
            UnmatchedRecordPolicy: UnmatchedRecordPolicy.Error);

        var json = JsonSerializer.Serialize(layout, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<InputLayoutDefinition>(
            json,
            JsonOptions);

        Assert.Contains("\"kind\":\"fixedWidth\"", json);
        Assert.Contains("\"action\":\"Import\"", json);
        var fixedWidth = Assert.IsType<FixedWidthInputLayout>(deserialized);
        Assert.Equal("utf16CodeUnit", fixedWidth.PositionUnit);
        Assert.Equal(
            "order_no",
            fixedWidth.RecordRules.Single().Fields.Single().SourceColumn);
    }
}
