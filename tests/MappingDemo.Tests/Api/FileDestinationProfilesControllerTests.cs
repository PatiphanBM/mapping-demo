using System.Text;
using System.Text.Json;
using MappingDemo.Api.Controllers;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Tests.Api;

public sealed class FileDestinationProfilesControllerTests
{
    [Fact]
    public async Task Resolve_returns_contract_paths_without_server_output_root()
    {
        var contract = new ContractReference(
            "proposal",
            "1",
            "proposal/1",
            SchemaHash: string.Empty);
        var fileProfiles = new FileDestinationProfileCatalog([
            new FileDestinationProfile(
                "operations-json",
                true,
                Path.GetFullPath("server-secret-output"),
                "volume-encrypted",
                1024 * 1024,
                0,
                DateTimeOffset.Parse("2026-10-26T00:00:00Z"),
                10,
                [new SupportedContract("proposal", "1", "proposal/1")],
                [new PayloadBinding(
                    "fileJobId",
                    RuntimeValueSource.FileJobId)])
        ]);
        var registry = new ContractRegistryModule(
            new ApiDestinationProfileCatalog([]),
            new SchemaFetcher(),
            fileProfiles: fileProfiles);
        var controller = new FileDestinationProfilesController(
            fileProfiles,
            registry);

        var result = await controller.Resolve(
            "operations-json",
            contract,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FileContractResolveResponse>(ok.Value);
        Assert.Equal("proposal/1", response.Contract.Reference);
        Assert.Contains(
            response.Paths,
            path => path.Path == "customer.name" && path.IsAssignable);
        Assert.DoesNotContain(
            "server-secret-output",
            JsonSerializer.Serialize(response),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SchemaFetcher : IContractSchemaFetcher
    {
        public ValueTask<ContractSchemaFetchResult> FetchAsync(
            IContractProfile profile,
            ContractReference contract,
            CancellationToken cancellationToken)
        {
            const string schema = """
                {
                  "type":"object",
                  "properties":{
                    "fileJobId":{"type":"integer"},
                    "customer":{
                      "type":"object",
                      "properties":{"name":{"type":"string"}},
                      "required":["name"],
                      "additionalProperties":false
                    }
                  },
                  "required":["fileJobId","customer"],
                  "additionalProperties":false
                }
                """;
            return ValueTask.FromResult(new ContractSchemaFetchResult(
                Encoding.UTF8.GetBytes(schema),
                "application/schema+json",
                DateTimeOffset.Parse("2026-10-26T00:00:00Z")));
        }
    }
}
