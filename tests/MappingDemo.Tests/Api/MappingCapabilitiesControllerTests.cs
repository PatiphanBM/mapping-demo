using System.Text.Json.Serialization;
using System.Text.Json;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Tests.Api;

public sealed class MappingCapabilitiesControllerTests
{
    [Fact]
    public void Get_returns_compiler_owned_capabilities_and_immutable_profile()
    {
        var fileProfiles = new FileDestinationProfileCatalog([
            new FileDestinationProfile(
                "operations-json",
                true,
                Path.GetFullPath("server-only-output"),
                "volume-encrypted",
                1024 * 1024,
                0,
                DateTimeOffset.Parse("2026-10-22T00:00:00Z"),
                10,
                [new SupportedContract("proposal", "1", "proposal/1")],
                [])
        ]);
        var action = new MappingCapabilitiesController(fileProfiles).Get();

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
        Assert.Contains(
            TransformationCapabilities.MtiOriginalPolicyExistsV1,
            response.Transformation.Lookups);
        Assert.Contains(
            TransformationCapabilities.MtiOriginalLoanExistsV1,
            response.Transformation.Lookups);
        Assert.Contains("jsonFile", response.Destinations.Kinds);
        Assert.Equal(
            ["compact", "indented"],
            response.Destinations.JsonFile.Formatting);
        Assert.Contains(
            "{artifactRevision}",
            response.Destinations.JsonFile.FileNameTokens);
        var fileProfile = Assert.Single(
            response.Destinations.JsonFile.Profiles);
        Assert.Equal("operations-json", fileProfile.Id);
        Assert.True(fileProfile.Enabled);
        Assert.Equal("proposal/1", Assert.Single(
            fileProfile.SupportedContracts).Reference);
        Assert.DoesNotContain(
            "server-only-output",
            JsonSerializer.Serialize(response),
            StringComparison.OrdinalIgnoreCase);
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
