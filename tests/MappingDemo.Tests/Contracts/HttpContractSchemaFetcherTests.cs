using System.Net;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Tests.Contracts;

public sealed class HttpContractSchemaFetcherTests
{
    [Fact]
    public async Task Fetch_rejects_redirect_response()
    {
        using var handler = new RedirectHandler();
        using var client = new HttpClient(handler);
        var fetcher = new HttpContractSchemaFetcher(
            client,
            new NoOpAuthenticationProvider());
        var profile = ContractTestData.Profile("redirect", "claims/2");

        var exception = await Assert.ThrowsAsync<ContractFetchException>(async () =>
            await fetcher.FetchAsync(
                profile,
                new ContractReference("claims", "2", "claims/2", string.Empty),
                CancellationToken.None));

        Assert.Equal("SchemaRedirectNotAllowed", exception.Code);
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://other.example.test") }
            });
    }

    private sealed class NoOpAuthenticationProvider
        : IContractAuthenticationProvider
    {
        public ValueTask ApplyAsync(
            HttpRequestMessage request,
            string secretReference,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
