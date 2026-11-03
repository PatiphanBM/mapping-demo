using System.Net;
using System.Text;
using MappingDemo.Shared.Contracts;
using MappingDemo.Worker.Delivery;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryHttpAdapterTests
{
    [Fact]
    public async Task Sends_exact_bytes_and_approved_headers()
    {
        var transport = new RecordingTransport(HttpStatusCode.Accepted);
        var adapter = new DeliveryHttpAdapter(transport);
        var payload = Encoding.UTF8.GetBytes("{\"amount\":10}");

        var result = await adapter.SendAsync(
            Profile(timeoutSeconds: 10),
            new DeliverySendRequest(
                42,
                "1",
                "proposal/1",
                "9:8:7:pmx",
                payload),
            CancellationToken.None);

        Assert.Equal(202, result.StatusCode);
        Assert.Equal(payload, transport.Payload);
        Assert.Equal("9:8:7:pmx", transport.IdempotencyKey);
        Assert.Equal("delivery-42", transport.CorrelationId);
        Assert.Equal("https://delivery.example.test/proposals/1", transport.Uri);
    }

    [Fact]
    public async Task Commit_before_timeout_preserves_the_idempotency_key()
    {
        var transport = new TimeoutAfterCommitTransport();
        var adapter = new DeliveryHttpAdapter(transport);

        var result = await adapter.SendAsync(
            Profile(timeoutSeconds: 1),
            new DeliverySendRequest(
                42,
                "1",
                "proposal/1",
                "9:8:7:pmx",
                Encoding.UTF8.GetBytes("{}")),
            CancellationToken.None);

        Assert.True(result.TransportFailure);
        Assert.Equal("9:8:7:pmx", transport.CommittedKey);
    }

    [Theory]
    [InlineData("https://localhost/token")]
    [InlineData("https://10.0.0.1/token")]
    [InlineData("https://auth.example.test:8443/token")]
    [InlineData("https://auth.example.test/token?secret=value")]
    public async Task OAuth_token_endpoint_rejects_unsafe_origin(
        string tokenEndpoint)
    {
        using var transport = new AuthenticatedDeliveryTransport(
            new OAuthSecretProvider(tokenEndpoint));
        var profile = Profile(timeoutSeconds: 10) with
        {
            AuthScheme = "OAuth2ClientCredentials",
            AuthSecretReference = "oauth-secret"
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://delivery.example.test/proposals/1");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await transport.SendAsync(
                profile,
                request,
                CancellationToken.None));

        Assert.Contains("approved HTTPS endpoint", exception.Message);
    }

    private static ApiDestinationProfile Profile(int timeoutSeconds) => new(
        "stub",
        "Stub",
        true,
        "https://contracts.example.test",
        "/schemas/{contractReference}",
        "https://delivery.example.test",
        "/proposals/{contractVersion}",
        "POST",
        ["X-Correlation-Id", "Idempotency-Key"],
        "stub-secret",
        timeoutSeconds,
        [new SupportedContract("proposal", "1", "proposal/1")],
        [],
        "None",
        "Idempotency-Key",
        "X-Correlation-Id");

    private sealed class RecordingTransport(HttpStatusCode status)
        : IAuthenticatedDeliveryTransport
    {
        public byte[]? Payload { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? Uri { get; private set; }

        public async ValueTask<HttpResponseMessage> SendAsync(
            ApiDestinationProfile profile,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Payload = await request.Content!.ReadAsByteArrayAsync(
                cancellationToken);
            IdempotencyKey = request.Headers.GetValues(
                "Idempotency-Key").Single();
            CorrelationId = request.Headers.GetValues(
                "X-Correlation-Id").Single();
            Uri = request.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(status);
        }
    }

    private sealed class TimeoutAfterCommitTransport
        : IAuthenticatedDeliveryTransport
    {
        public string? CommittedKey { get; private set; }

        public async ValueTask<HttpResponseMessage> SendAsync(
            ApiDestinationProfile profile,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CommittedKey = request.Headers.GetValues(
                "Idempotency-Key").Single();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after cancellation.");
        }
    }

    private sealed class OAuthSecretProvider(string tokenEndpoint)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    tokenEndpoint,
                    clientId = "client",
                    clientSecret = "secret"
                }));
    }
}
