using System.Text;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Api.Services;

public sealed class RegistryContractSchemaFetcher(
    IVersionedContractRegistry registry) : IContractSchemaFetcher
{
    public async ValueTask<ContractSchemaFetchResult> FetchAsync(
        IContractProfile profile,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        var result = await registry.ResolveExactAsync(
            contract.Reference,
            cancellationToken);
        if (!result.IsSuccess)
        {
            var diagnostic = result.Diagnostics.FirstOrDefault();
            throw new ContractFetchException(
                diagnostic?.Code ?? "ContractNotFound",
                diagnostic?.Message ?? "Contract Version was not found.");
        }

        var resolved = result.ContractVersion!;
        if (resolved.Version.Lifecycle != ContractVersionLifecycle.Published)
        {
            throw new ContractFetchException(
                "ContractRetired",
                "Retired Contract Versions cannot be selected for new work.");
        }

        return new ContractSchemaFetchResult(
            Encoding.UTF8.GetBytes(resolved.Version.CanonicalSchema!),
            "application/schema+json",
            resolved.Version.PublishedAt ?? resolved.Version.CreatedAt);
    }
}
