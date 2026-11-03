using System.Text;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Tests.Contracts;

public sealed class ContractSchemaModuleTests
{
    [Fact]
    public void Compile_canonicalizes_schema_and_builds_nested_path_catalog()
    {
        const string first = """
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["proposal"],
              "$defs": {
                "person": {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["fullName"],
                  "properties": {
                    "fullName": { "type": "string", "maxLength": 120 },
                    "birthTime": { "type": ["string", "null"], "format": "time" }
                  }
                }
              },
              "properties": {
                "proposal": {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["insured", "beneficiaries"],
                  "properties": {
                    "beneficiaries": { "type": "array", "items": { "$ref": "#/$defs/person" } },
                    "insured": { "$ref": "#/$defs/person" }
                  }
                }
              }
            }
            """;
        const string reordered = """
            {"properties":{"proposal":{"properties":{"insured":{"$ref":"#/$defs/person"},"beneficiaries":{"items":{"$ref":"#/$defs/person"},"type":"array"}},"required":["insured","beneficiaries"],"additionalProperties":false,"type":"object"}},"$defs":{"person":{"properties":{"birthTime":{"format":"time","type":["string","null"]},"fullName":{"maxLength":120,"type":"string"}},"required":["fullName"],"additionalProperties":false,"type":"object"}},"required":["proposal"],"additionalProperties":false,"type":"object"}
            """;

        var module = new ContractSchemaModule();
        var left = module.Compile(first);
        var right = module.Compile(reordered);

        Assert.True(left.IsSuccess, DiagnosticText(left.Diagnostics));
        Assert.True(right.IsSuccess, DiagnosticText(right.Diagnostics));
        Assert.Equal(left.SchemaHash, right.SchemaHash);
        Assert.Equal(left.CanonicalSchema, right.CanonicalSchema);
        Assert.Contains(left.PathCatalog, path =>
            path.Path == "proposal.insured.fullName" &&
            path.IsAssignable && path.IsRequired &&
            path.ScalarType == ContractScalarType.Text);
        Assert.Contains(left.PathCatalog, path =>
            path.Path == "proposal.insured.birthTime" &&
            path.IsNullable && path.ScalarType == ContractScalarType.Time);
        Assert.Contains(left.PathCatalog, path =>
            path.Path == "proposal.beneficiaries[]" && !path.IsAssignable);
        Assert.Contains(left.PathCatalog, path =>
            path.Path == "proposal.beneficiaries[].fullName" && path.IsAssignable);
    }

    [Theory]
    [InlineData("{\"$ref\":\"https://example.test/schema.json\"}", "ExternalReferenceNotSupported")]
    [InlineData("{\"$defs\":{\"node\":{\"$ref\":\"#/$defs/node\"}},\"$ref\":\"#/$defs/node\"}", "RecursiveReferenceNotSupported")]
    [InlineData("{\"oneOf\":[{\"type\":\"string\"},{\"type\":\"integer\"}]}", "AmbiguousUnionNotSupported")]
    [InlineData("{\"type\":\"object\",\"additionalProperties\":true}", "UnconstrainedAdditionalPropertiesNotSupported")]
    public void Compile_rejects_unsupported_schema_features(
        string schema,
        string expectedCode)
    {
        var result = new ContractSchemaModule().Compile(schema);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == expectedCode);
    }

    [Fact]
    public async Task ResolveAsync_enforces_profile_and_fetches_through_adapter()
    {
        var profile = ContractTestData.Profile("claims-v2", "claims/2");
        var catalog = new ApiDestinationProfileCatalog([profile]);
        var fetcher = new FakeFetcher("""
            {"type":"object","additionalProperties":false,"required":["case"],"properties":{"case":{"type":"object","additionalProperties":false,"properties":{"claimNo":{"type":"string"}}}}}
            """);
        var registry = new ContractRegistryModule(catalog, fetcher);

        var result = await registry.ResolveAsync(
            profile.Id,
            new ContractReference("claims", "2", "claims/2", string.Empty),
            CancellationToken.None);

        Assert.True(result.IsSuccess, DiagnosticText(result.Diagnostics));
        Assert.Equal(1, fetcher.Calls);
        Assert.StartsWith("sha256:", result.SchemaHash);
        Assert.Equal("claims/2", result.Contract!.Reference);
        Assert.NotEmpty(result.CanonicalSchema!);
    }

    [Fact]
    public async Task ResolveAsync_supports_file_profile_without_http_transport()
    {
        var apiProfiles = new ApiDestinationProfileCatalog([]);
        var fileProfile = FileDestinationProfileCatalogTests.Profile(
            "local-json-export",
            "claims/2") with
        {
            PayloadBindings =
            [
                new PayloadBinding(
                    "meta.fileJobId",
                    RuntimeValueSource.FileJobId)
            ]
        };
        var fileProfiles = new FileDestinationProfileCatalog([fileProfile]);
        var fetcher = new FakeFetcher("""
            {
              "type":"object",
              "additionalProperties":false,
              "required":["case","meta"],
              "properties":{
                "case":{"type":"object","additionalProperties":false,"properties":{"claimNo":{"type":"string"}}},
                "meta":{"type":"object","additionalProperties":false,"required":["fileJobId"],"properties":{"fileJobId":{"type":"integer"}}}
              }
            }
            """);
        var registry = new ContractRegistryModule(
            apiProfiles,
            fetcher,
            fileProfiles: fileProfiles);
        var destination = new JsonFileDestination(
            fileProfile.Id,
            new ContractReference("claims", "2", "claims/2", string.Empty),
            "claims",
            "{sourceBaseName}.{fileJobId}.json",
            "compact");

        var result = await registry.ResolveAsync(
            destination,
            CancellationToken.None);

        Assert.True(result.IsSuccess, DiagnosticText(result.Diagnostics));
        Assert.Equal(1, fetcher.Calls);
        Assert.StartsWith("sha256:", result.SchemaHash);
        Assert.Equal("claims/2", result.Contract!.Reference);
        Assert.Equal(
            "meta.fileJobId",
            Assert.Single(result.PayloadBindings).TargetPath);
        Assert.Null(result.Profile);
    }

    private static string DiagnosticText(
        IReadOnlyList<ContractDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));

    private sealed class FakeFetcher(string json) : IContractSchemaFetcher
    {
        public int Calls { get; private set; }

        public ValueTask<ContractSchemaFetchResult> FetchAsync(
            IContractProfile profile,
            ContractReference contract,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(new ContractSchemaFetchResult(
                Encoding.UTF8.GetBytes(json),
                "application/schema+json",
                DateTimeOffset.Parse("2026-10-20T00:00:00Z")));
        }
    }
}
