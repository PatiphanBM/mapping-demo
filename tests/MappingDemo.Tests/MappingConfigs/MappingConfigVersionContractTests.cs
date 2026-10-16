using System.Text.Json;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class MappingConfigVersionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Create_request_round_trips_delimited_input_layout()
    {
        var request = new CreateMappingConfigVersionRequest(
            DelimitedLayout(),
            Program());

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized =
            JsonSerializer.Deserialize<CreateMappingConfigVersionRequest>(
                json,
                JsonOptions);

        Assert.DoesNotContain("fileToSource", json);
        Assert.DoesNotContain("sourceToNormalized", json);
        Assert.Contains("\"inputLayout\":{\"kind\":\"delimited\"", json);
        Assert.Contains(
            "\"transformationProgram\":{\"languageVersion\":1," +
            "\"semanticsProfile\":\"sqlserver-legacy-v1\"",
            json);
        var layout = Assert.IsType<DelimitedInputLayout>(
            deserialized!.InputLayout);
        Assert.Equal(2, layout.PreambleLines);
        Assert.Equal("order_no", layout.Fields[0].SourceColumn);
        Assert.IsType<SourceExpression>(
            deserialized.TransformationProgram.Outputs
                .Single().Fields.Single().Value);
    }

    [Fact]
    public void Version_responses_round_trip_fixed_width_input_layout()
    {
        var version = new MappingConfigVersionResponse(
            Id: 41,
            ConfigId: 7,
            VersionNo: 3,
            FixedWidthLayout(),
            Program());
        var details = new MappingConfigVersionDetailsResponse(
            Id: 41,
            VersionNo: 3,
            IsActive: true,
            FixedWidthLayout(),
            Program());

        var versionJson = JsonSerializer.Serialize(version, JsonOptions);
        var detailsJson = JsonSerializer.Serialize(details, JsonOptions);
        var versionCopy =
            JsonSerializer.Deserialize<MappingConfigVersionResponse>(
                versionJson,
                JsonOptions);
        var detailsCopy =
            JsonSerializer.Deserialize<MappingConfigVersionDetailsResponse>(
                detailsJson,
                JsonOptions);

        Assert.IsType<FixedWidthInputLayout>(versionCopy!.InputLayout);
        var layout = Assert.IsType<FixedWidthInputLayout>(
            detailsCopy!.InputLayout);
        Assert.Equal("detail", layout.RecordRules.Single().Key);
        Assert.DoesNotContain("fileToSource", versionJson);
        Assert.DoesNotContain("fileToSource", detailsJson);
        Assert.DoesNotContain("sourceToNormalized", versionJson);
        Assert.DoesNotContain("sourceToNormalized", detailsJson);
        Assert.Equal(
            "default",
            versionCopy.TransformationProgram.Outputs.Single().OutputKey);
        Assert.Equal(
            "default",
            detailsCopy.TransformationProgram.Outputs.Single().OutputKey);
    }

    [Fact]
    public void Create_request_rejects_legacy_source_to_normalized_payload()
    {
        const string json = """
            {
              "inputLayout": {
                "kind": "delimited",
                "encoding": "utf-8",
                "delimiter": ",",
                "preambleLines": 0,
                "headerPolicy": "SingleRow",
                "fields": [
                  { "inputHeader": "Order No", "sourceColumn": "order_no" }
                ]
              },
              "sourceToNormalized": []
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateMappingConfigVersionRequest>(
                json,
                JsonOptions));
    }

    private static DelimitedInputLayout DelimitedLayout()
    {
        return new DelimitedInputLayout(
            "utf-8",
            ",",
            PreambleLines: 2,
            DelimitedHeaderPolicy.SingleRow,
            [new DelimitedInputField("Order No", "order_no")]);
    }

    private static FixedWidthInputLayout FixedWidthLayout()
    {
        return new FixedWidthInputLayout(
            "utf-8",
            "utf16CodeUnit",
            [
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    ExpectedLength: 10,
                    [new FixedWidthInputField("order_no", 2, 9)])
            ],
            [new InputRecordSequenceItem("detail", 1, null)],
            UnmatchedRecordPolicy.Error);
    }

    private static TransformationProgram Program()
    {
        return new TransformationProgram
        {
            LanguageVersion = 1,
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "order_no",
                            Value = new SourceExpression
                            {
                                Field = "order_no"
                            }
                        }
                    ]
                }
            ]
        };
    }
}
