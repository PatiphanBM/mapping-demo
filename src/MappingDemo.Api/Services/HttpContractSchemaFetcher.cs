using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using System.Net.Http.Headers;

namespace MappingDemo.Api.Services;

public interface IContractAuthenticationProvider
{
    ValueTask ApplyAsync(
        HttpRequestMessage request,
        string secretReference,
        CancellationToken cancellationToken);
}

public sealed class HttpContractSchemaFetcher(
    HttpClient httpClient,
    IContractAuthenticationProvider authentication) : IContractSchemaFetcher
{
    public async ValueTask<ContractSchemaFetchResult> FetchAsync(
        IContractProfile profile,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        if (profile is not ApiDestinationProfile apiProfile)
        {
            throw new ContractFetchException(
                "SchemaEndpointUnavailable",
                "This contract schema fetcher requires an API profile endpoint.");
        }

        var relativePath = apiProfile.SchemaPathTemplate.Replace(
            "{contractReference}",
            string.Join('/', contract.Reference.Split('/').Select(Uri.EscapeDataString)),
            StringComparison.Ordinal);
        var uri = new Uri(new Uri(apiProfile.SchemaBaseUrl), relativePath);
        if (!ApiDestinationProfileNetworkPolicy.TryValidateOrigin(
            apiProfile.SchemaBaseUrl,
            out _,
            out _))
        {
            throw new ContractFetchException(
                "SchemaEndpointUnavailable",
                "Contract schema endpoint is not allowed by network policy.");
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            "application/schema+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            "application/json"));
        await authentication.ApplyAsync(
            request,
            apiProfile.AuthSecretReference,
            cancellationToken);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new ContractFetchException(
                "SchemaRedirectNotAllowed",
                "Contract Registry redirects are not allowed.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new ContractFetchException(
                "ContractFetchFailed",
                $"Contract Registry returned HTTP {(int)response.StatusCode}.");
        }
        if (response.Content.Headers.ContentLength is > ContractRegistryModule.MaxSchemaBytes)
        {
            throw new ContractFetchException(
                "SchemaTooLarge",
                "Contract schema exceeds the size limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        await using var target = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (target.Length + count > ContractRegistryModule.MaxSchemaBytes)
            {
                throw new ContractFetchException(
                    "SchemaTooLarge",
                    "Contract schema exceeds the size limit.");
            }
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        return new ContractSchemaFetchResult(
            target.ToArray(),
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            DateTimeOffset.UtcNow);
    }
}
