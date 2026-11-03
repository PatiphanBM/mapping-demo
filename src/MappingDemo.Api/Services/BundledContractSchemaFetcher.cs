using System.Text;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Api.Services;

public sealed class BundledContractSchemaFetcher(IHostEnvironment environment)
    : IContractSchemaFetcher
{
    private static readonly IReadOnlyDictionary<string, string> Files =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pa-inform/1"] = "pa-inform-v1.json",
            ["proposal/1"] = "pa-proposal-v1.json",
            ["claims/2"] = "claims-envelope-v2.json"
        };

    public async ValueTask<ContractSchemaFetchResult> FetchAsync(
        IContractProfile profile,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        if (!Files.TryGetValue(contract.Reference, out var file))
        {
            throw new ContractFetchException(
                "ContractNotFound",
                "The configured contract schema was not found.");
        }

        var path = Path.Combine(environment.ContentRootPath, "ContractFixtures", file);
        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new ContractSchemaFetchResult(
            content,
            "application/schema+json",
            new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
    }
}
