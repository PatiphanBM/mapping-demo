using System.Text.Json;
using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class DestinationDefinitionValidatorTests
{
    public static TheoryData<string, string, string> InvalidDestinations =>
        new()
        {
            {
                "[]",
                "destination",
                "Destination must be a JSON object."
            },
            {
                "{}",
                "destination.kind",
                "Destination kind is required."
            },
            {
                """{"kind":"queue"}""",
                "destination.kind",
                "Unknown destination kind."
            },
            {
                """{"kind":"legacyTable"}""",
                "destination.normalizedTableId",
                "Normalized table ID must be a positive integer."
            },
            {
                """{"kind":"legacyTable","normalizedTableId":0}""",
                "destination.normalizedTableId",
                "Normalized table ID must be a positive integer."
            },
            {
                """{"kind":"apiContract","contract":{}}""",
                "destination.apiDestinationProfileId",
                "API destination profile ID is required."
            },
            {
                """
                {
                  "kind": "apiContract",
                  "apiDestinationProfileId": "partner-api",
                  "contract": {}
                }
                """,
                "destination.contract.id",
                "Contract ID is required."
            },
            {
                """
                {
                  "kind": "apiContract",
                  "apiDestinationProfileId": "partner-api",
                  "contract": {
                    "id": "proposal",
                    "version": "1",
                    "reference": "proposal/1",
                    "schemaHash": "sha256:test"
                  },
                  "payloadBindingsSnapshot": []
                }
                """,
                "destination.payloadBindingsSnapshot",
                "Property is not allowed for apiContract destination."
            },
            {
                """{"kind":"legacyTable","normalizedTableId":42,"apiDestinationProfileId":"mixed"}""",
                "destination.apiDestinationProfileId",
                "Property is not allowed for legacyTable destination."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": " ",
                  "contract": {},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "export.json",
                  "formatting": "compact"
                }
                """,
                "destination.fileDestinationProfileId",
                "File destination profile ID is required."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1"},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "export.json",
                  "formatting": "compact"
                }
                """,
                "destination.contract.schemaHash",
                "Contract schema hash is required."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "../customers",
                  "fileNameTemplate": "export.json",
                  "formatting": "compact"
                }
                """,
                "destination.relativeFolder",
                "Relative folder must be a safe relative path."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "C:/exports",
                  "fileNameTemplate": "export.json",
                  "formatting": "compact"
                }
                """,
                "destination.relativeFolder",
                "Relative folder must be a safe relative path."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "sub/export.json",
                  "formatting": "compact"
                }
                """,
                "destination.fileNameTemplate",
                "File name template must not contain path separators."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "{tenantId}.json",
                  "formatting": "compact"
                }
                """,
                "destination.fileNameTemplate",
                "File name template contains an unknown token."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "export.json",
                  "formatting": "pretty"
                }
                """,
                "destination.formatting",
                "Formatting must be compact or indented."
            },
            {
                """
                {
                  "kind": "jsonFile",
                  "fileDestinationProfileId": "local-json-export",
                  "contract": {"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"sha256:test"},
                  "relativeFolder": "customers",
                  "fileNameTemplate": "export.json",
                  "formatting": "compact",
                  "normalizedTableId": 42
                }
                """,
                "destination.normalizedTableId",
                "Property is not allowed for jsonFile destination."
            }
        };

    [Theory]
    [MemberData(nameof(InvalidDestinations))]
    public void Validate_rejects_invalid_json_shape_with_exact_path(
        string json,
        string expectedPath,
        string expectedMessage)
    {
        using var document = JsonDocument.Parse(json);

        var error = Assert.Single(
            DestinationDefinitionValidator.Validate(document.RootElement));

        Assert.Equal(expectedPath, error.Field);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData("""{"kind":"legacyTable","normalizedTableId":42}""")]
    [InlineData("""
        {
          "kind": "apiContract",
          "apiDestinationProfileId": "partner-api",
          "contract": {
            "id": "proposal",
            "version": "1",
            "reference": "proposal/1",
            "schemaHash": "sha256:test"
          }
        }
        """)]
    [InlineData("""
        {
          "kind": "jsonFile",
          "fileDestinationProfileId": "local-json-export",
          "contract": {
            "id": "proposal",
            "version": "1",
            "reference": "proposal/1",
            "schemaHash": "sha256:test"
          },
          "relativeFolder": "customers/approved",
          "fileNameTemplate": "{sourceBaseName}.{fileJobId}.r{artifactRevision}.json",
          "formatting": "indented"
        }
        """)]
    public void Validate_accepts_supported_destination_shapes(string json)
    {
        using var document = JsonDocument.Parse(json);

        var errors = DestinationDefinitionValidator.Validate(
            document.RootElement);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("customers/CON", "export.json", "destination.relativeFolder")]
    [InlineData("customers", "CON.json", "destination.fileNameTemplate")]
    [InlineData("customers", "sub\\export.json", "destination.fileNameTemplate")]
    [InlineData("customers", "{fileJobId.json", "destination.fileNameTemplate")]
    public void Validate_rejects_windows_unsafe_paths_with_exact_path(
        string relativeFolder,
        string fileNameTemplate,
        string expectedPath)
    {
        var json = JsonFileJson(relativeFolder, fileNameTemplate);
        using var document = JsonDocument.Parse(json);

        var error = Assert.Single(
            DestinationDefinitionValidator.Validate(document.RootElement));

        Assert.Equal(expectedPath, error.Field);
    }

    [Fact]
    public void Validate_rejects_overlong_filename_template()
    {
        var json = JsonFileJson("customers", new string('a', 181));
        using var document = JsonDocument.Parse(json);

        var error = Assert.Single(
            DestinationDefinitionValidator.Validate(document.RootElement));

        Assert.Equal("destination.fileNameTemplate", error.Field);
    }

    [Fact]
    public void Validate_rejects_unknown_contract_field_with_exact_path()
    {
        const string json = """
            {
              "kind": "jsonFile",
              "fileDestinationProfileId": "local-json-export",
              "contract": {
                "id":"proposal",
                "version":"1",
                "reference":"proposal/1",
                "schemaHash":"sha256:test",
                "endpoint":"https://example.test"
              },
              "relativeFolder": "customers",
              "fileNameTemplate": "export.json",
              "formatting": "compact"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var error = Assert.Single(
            DestinationDefinitionValidator.Validate(document.RootElement));

        Assert.Equal("destination.contract.endpoint", error.Field);
    }

    private static string JsonFileJson(
        string relativeFolder,
        string fileNameTemplate) =>
        JsonSerializer.Serialize(new
        {
            kind = "jsonFile",
            fileDestinationProfileId = "local-json-export",
            contract = new
            {
                id = "proposal",
                version = "1",
                reference = "proposal/1",
                schemaHash = "sha256:test"
            },
            relativeFolder,
            fileNameTemplate,
            formatting = "compact"
        });
}
