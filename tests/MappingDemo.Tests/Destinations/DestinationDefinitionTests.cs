using System.Text.Json;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Serialization;

namespace MappingDemo.Tests.Destinations;

public sealed class DestinationDefinitionTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Legacy_table_definition_round_trips_with_its_discriminator()
    {
        const string json = """
            {
              "kind": "legacyTable",
              "normalizedTableId": 42
            }
            """;

        var definition = PolymorphicJson.Deserialize<DestinationDefinition>(
            json,
            JsonOptions);
        var legacy = Assert.IsType<LegacyTableDestination>(definition);

        Assert.Equal(42, legacy.NormalizedTableId);
        Assert.Equal(
            "legacyTable",
            JsonDocument.Parse(JsonSerializer.Serialize(definition, JsonOptions))
                .RootElement
                .GetProperty("kind")
                .GetString());
    }

    [Fact]
    public void Api_contract_definition_round_trips_with_its_discriminator()
    {
        const string json = """
            {
              "kind": "apiContract",
              "apiDestinationProfileId": "pa-proposals",
              "contract": {
                "id": "proposal",
                "version": "1",
                "reference": "proposal/1",
                "schemaHash": "sha256:abc"
              }
            }
            """;

        var definition = PolymorphicJson.Deserialize<DestinationDefinition>(
            json,
            JsonOptions);
        var api = Assert.IsType<ApiContractDestination>(definition);

        Assert.Equal("pa-proposals", api.ApiDestinationProfileId);
        Assert.Equal("proposal/1", api.Contract.Reference);
        Assert.Equal(
            "apiContract",
            JsonDocument.Parse(JsonSerializer.Serialize(definition, JsonOptions))
                .RootElement
                .GetProperty("kind")
                .GetString());
    }

    [Fact]
    public void Json_file_definition_round_trips_with_its_discriminator()
    {
        const string json = """
            {
              "kind": "jsonFile",
              "fileDestinationProfileId": "local-json-export",
              "contract": {
                "id": "proposal",
                "version": "1",
                "reference": "proposal/1",
                "schemaHash": "sha256:abc"
              },
              "relativeFolder": "customers/approved",
              "fileNameTemplate": "{sourceBaseName}.{fileJobId}.r{artifactRevision}.json",
              "formatting": "compact"
            }
            """;

        var definition = PolymorphicJson.Deserialize<DestinationDefinition>(
            json,
            JsonOptions);
        var jsonFile = Assert.IsType<JsonFileDestination>(definition);

        Assert.Equal("local-json-export", jsonFile.FileDestinationProfileId);
        Assert.Equal("proposal/1", jsonFile.Contract.Reference);
        Assert.Equal("customers/approved", jsonFile.RelativeFolder);
        Assert.Equal(
            "{sourceBaseName}.{fileJobId}.r{artifactRevision}.json",
            jsonFile.FileNameTemplate);
        Assert.Equal("compact", jsonFile.Formatting);
        Assert.Equal(
            "jsonFile",
            JsonDocument.Parse(JsonSerializer.Serialize(definition, JsonOptions))
                .RootElement
                .GetProperty("kind")
                .GetString());
    }

    [Theory]
    [InlineData("normalizedTableId")]
    [InlineData("apiDestinationProfileId")]
    [InlineData("unexpected")]
    public void Json_file_definition_rejects_mixed_or_unknown_fields(
        string propertyName)
    {
        var json = $$"""
            {
              "kind": "jsonFile",
              "fileDestinationProfileId": "local-json-export",
              "contract": {
                "id": "proposal",
                "version": "1",
                "reference": "proposal/1",
                "schemaHash": "sha256:abc"
              },
              "relativeFolder": "customers",
              "fileNameTemplate": "{sourceBaseName}.{fileJobId}.json",
              "formatting": "compact",
              "{{propertyName}}": "mixed"
            }
            """;

        var exception = Assert.Throws<JsonException>(() =>
            PolymorphicJson.Deserialize<DestinationDefinition>(
                json,
                JsonOptions));

        Assert.Equal($"$.{propertyName}", exception.Path);
    }
}
