using System.Text.Json;
using MappingDemo.Api;
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
}
