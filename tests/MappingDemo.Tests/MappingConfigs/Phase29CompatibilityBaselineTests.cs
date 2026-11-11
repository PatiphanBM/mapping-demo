using System.Text.Json;
using System.Text.Json.Nodes;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Serialization;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class Phase29CompatibilityBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Existing_destination_wire_contracts_match_phase_29_baseline()
    {
        using var fixture = LoadFixture();
        var root = fixture.RootElement;

        AssertRoundTrip<LegacyTableDestination>(
            root.GetProperty("legacyTable"));
        AssertRoundTrip<ApiContractDestination>(
            root.GetProperty("apiContract"));
        AssertRoundTrip<JsonFileDestination>(
            root.GetProperty("jsonFile"));
    }

    private static void AssertRoundTrip<TDestination>(JsonElement expected)
        where TDestination : DestinationDefinition
    {
        var destination = PolymorphicJson.Deserialize<DestinationDefinition>(
            expected.GetRawText(),
            JsonOptions);

        Assert.IsType<TDestination>(destination);
        var expectedNode = JsonNode.Parse(expected.GetRawText());
        var actualNode = JsonSerializer.SerializeToNode(
            destination,
            JsonOptions);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"Expected: {expectedNode}{Environment.NewLine}Actual: {actualNode}");
    }

    private static JsonDocument LoadFixture()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Phase29",
            "compatibility-baseline.golden.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
