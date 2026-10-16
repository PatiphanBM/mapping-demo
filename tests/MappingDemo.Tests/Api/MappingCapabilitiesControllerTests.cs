using System.Text.Json.Serialization;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Tests.Api;

public sealed class MappingCapabilitiesControllerTests
{
    [Fact]
    public void Get_returns_compiler_owned_capabilities_and_immutable_profile()
    {
        var action = new MappingCapabilitiesController().Get();

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<MappingCapabilitiesResponse>(ok.Value);
        Assert.Equal(InputCapabilities.Formats, response.Input.Formats);
        Assert.Equal(InputCapabilities.Encodings, response.Input.Encodings);
        Assert.Equal(
            InputCapabilities.PositionUnits,
            response.Input.PositionUnits);
        var profile = Assert.Single(
            response.Transformation.SemanticsProfiles);
        Assert.Equal(
            TransformationCapabilities.SqlServerLegacyProfile,
            profile.Id);
        Assert.True(profile.Immutable);
        Assert.NotEmpty(profile.Summary);
        Assert.Equal(
            TransformationCapabilities.TargetConversionTypes,
            response.Transformation.TargetConversionTypes);
        Assert.Equal(
            TransformationCapabilities.DateInputFormats,
            response.Transformation.InputFormats.Date);
        Assert.Equal(
            TransformationCapabilities.DateTimeInputFormats,
            response.Transformation.InputFormats.DateTime);
        Assert.Equal(
            DerivedKinds(typeof(ValueExpression)),
            response.Transformation.ValueExpressions.Order());
        Assert.Equal(
            DerivedKinds(typeof(BooleanExpression)),
            response.Transformation.BooleanOperators.Order());
    }

    private static IEnumerable<string> DerivedKinds(Type type)
    {
        return type.GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
            .Cast<JsonDerivedTypeAttribute>()
            .Select(attribute => Assert.IsType<string>(
                attribute.TypeDiscriminator))
            .Order();
    }
}
