using System.Text.Json;
using MappingDemo.Api;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Tests.Api;

public sealed class ApiJsonOptionsTests
{
    [Fact]
    public void Configure_serializes_enums_as_names()
    {
        var options = new JsonOptions();
        ApiJsonOptions.Configure(options);

        var json = JsonSerializer.Serialize(
            TransformationRowStatus.Filtered,
            options.JsonSerializerOptions);
        var attributedJson = JsonSerializer.Serialize(
            ExpressionValueType.DateTime,
            options.JsonSerializerOptions);

        Assert.Equal("\"Filtered\"", json);
        Assert.Equal("\"dateTime\"", attributedJson);
    }

    [Fact]
    public void CreateSerializerOptions_deserializes_excel_multipart_json()
    {
        var options = ApiJsonOptions.CreateSerializerOptions();

        var layout = PolymorphicJson.Deserialize<InputLayoutDefinition>(
            """
            {
              "kind": "excel",
              "workbookKind": "Xls",
              "sheet": "Sheet1",
              "headerRow": 1,
              "dataStartRow": 2,
              "fields": [
                { "inputHeader": "Order No", "sourceColumn": "order_no" }
              ],
              "blankRowPolicy": "Skip"
            }
            """,
            options);

        var excel = Assert.IsType<ExcelInputLayout>(layout);
        Assert.Equal(ExcelWorkbookKind.Xls, excel.WorkbookKind);
        Assert.Equal(ExcelBlankRowPolicy.Skip, excel.BlankRowPolicy);
    }

    [Fact]
    public void CreateSerializerOptions_deserializes_preview_version_extensions()
    {
        var options = ApiJsonOptions.CreateSerializerOptions();

        var request = JsonSerializer.Deserialize<
            PreviewMappingConfigVersionRequest>(
            """
            {
              "evaluationAt": "2026-10-28T00:00:00Z",
              "sample": { "kind": "sourceRows", "rows": [] },
              "inputLayout": null,
              "transformationProgram": null,
              "destination": null,
              "acceptedExtensions": [".xls"]
            }
            """,
            options);

        Assert.NotNull(request);
        Assert.Equal([".xls"], request.AcceptedExtensions);
    }
}
