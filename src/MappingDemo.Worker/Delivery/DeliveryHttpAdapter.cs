using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Worker.Delivery;

public sealed record DeliverySendRequest(
    long DeliveryJobId,
    string ContractVersion,
    string ContractReference,
    string IdempotencyKey,
    byte[] Payload);

public sealed record DeliverySendResult(
    int? StatusCode,
    bool TransportFailure,
    TimeSpan? RetryAfter,
    string? CorrelationId,
    string? ErrorRedacted);

public interface IDeliveryHttpAdapter
{
    ValueTask<DeliverySendResult> SendAsync(
        ApiDestinationProfile profile,
        DeliverySendRequest request,
        CancellationToken cancellationToken);
}

public interface IAuthenticatedDeliveryTransport
{
    ValueTask<HttpResponseMessage> SendAsync(
        ApiDestinationProfile profile,
        HttpRequestMessage request,
        CancellationToken cancellationToken);
}

public sealed class DeliveryHttpAdapter(
    IAuthenticatedDeliveryTransport transport) : IDeliveryHttpAdapter
{
    public async ValueTask<DeliverySendResult> SendAsync(
        ApiDestinationProfile profile,
        DeliverySendRequest request,
        CancellationToken cancellationToken)
    {
        var path = profile.DeliveryPathTemplate
            .Replace(
                "{contractVersion}",
                Uri.EscapeDataString(request.ContractVersion),
                StringComparison.Ordinal)
            .Replace(
                "{contractReference}",
                Uri.EscapeDataString(request.ContractReference),
                StringComparison.Ordinal);
        var uri = new Uri(new Uri(profile.DeliveryBaseUrl), path);
        using var message = new HttpRequestMessage(
            new HttpMethod(profile.Method),
            uri)
        {
            Content = new ByteArrayContent(request.Payload)
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/json");
        message.Headers.TryAddWithoutValidation(
            profile.IdempotencyHeaderName,
            request.IdempotencyKey);
        var requestCorrelation = $"delivery-{request.DeliveryJobId}";
        message.Headers.TryAddWithoutValidation(
            profile.CorrelationHeaderName,
            requestCorrelation);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
        try
        {
            using var response = await transport.SendAsync(
                profile,
                message,
                timeout.Token);
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var correlation = response.Headers.TryGetValues(
                    profile.CorrelationHeaderName,
                    out var values)
                ? values.FirstOrDefault()
                : requestCorrelation;
            return new DeliverySendResult(
                (int)response.StatusCode,
                false,
                retryAfter,
                correlation,
                response.IsSuccessStatusCode
                    ? null
                    : $"Partner returned HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new DeliverySendResult(
                null,
                true,
                null,
                requestCorrelation,
                "Partner request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return new DeliverySendResult(
                null,
                true,
                null,
                requestCorrelation,
                $"Partner transport failed ({exception.HttpRequestError}).");
        }
    }
}

public sealed class AuthenticatedDeliveryTransport(
    IDeliverySecretProvider secrets) : IAuthenticatedDeliveryTransport,
    IDisposable
{
    private static readonly JsonSerializerOptions SecretJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly HttpClient _plainClient = CreateClient();
    private readonly ConcurrentDictionary<string, Task<HttpClient>>
        _mtlsClients = new(StringComparer.Ordinal);

    public async ValueTask<HttpResponseMessage> SendAsync(
        ApiDestinationProfile profile,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var client = profile.AuthScheme switch
        {
            "Mtls" => await _mtlsClients.GetOrAdd(
                profile.Id,
                _ => CreateMtlsClientAsync(profile)),
            "OAuth2ClientCredentials" => _plainClient,
            "None" => _plainClient,
            _ => throw new InvalidOperationException(
                $"Unsupported delivery auth scheme '{profile.AuthScheme}'.")
        };

        if (profile.AuthScheme == "OAuth2ClientCredentials")
        {
            var token = await RequestAccessTokenAsync(
                profile,
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token);
        }

        return await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    public void Dispose()
    {
        _plainClient.Dispose();
        foreach (var task in _mtlsClients.Values)
        {
            if (task.IsCompletedSuccessfully)
            {
                task.Result.Dispose();
            }
        }
    }

    private async Task<HttpClient> CreateMtlsClientAsync(
        ApiDestinationProfile profile)
    {
        var secretJson = await secrets.GetSecretAsync(
            profile.AuthSecretReference);
        var secret = JsonSerializer.Deserialize<MtlsSecret>(
                secretJson,
                SecretJsonOptions)
            ?? throw new InvalidOperationException(
                "The mTLS secret is invalid.");
        var certificate = new X509Certificate2(
            Convert.FromBase64String(secret.PfxBase64),
            secret.Password,
            X509KeyStorageFlags.EphemeralKeySet);
        var handler =
            ApiDestinationProfileNetworkPolicy.CreateSocketsHttpHandler();
        handler.SslOptions.ClientCertificates = [certificate];
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private async Task<string> RequestAccessTokenAsync(
        ApiDestinationProfile profile,
        CancellationToken cancellationToken)
    {
        var secretJson = await secrets.GetSecretAsync(
            profile.AuthSecretReference,
            cancellationToken);
        var secret = JsonSerializer.Deserialize<OAuthSecret>(
                secretJson,
                SecretJsonOptions)
            ?? throw new InvalidOperationException(
                "The OAuth secret is invalid.");
        if (!Uri.TryCreate(secret.TokenEndpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.IsLoopback ||
            uri.HostNameType != UriHostNameType.Dns ||
            !uri.IsDefaultPort ||
            uri.Port != 443 ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "The OAuth token endpoint must be an approved HTTPS endpoint on port 443.");
        }

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = secret.ClientId,
                ["client_secret"] = secret.ClientSecret,
                ["scope"] = secret.Scope ?? string.Empty
            })
        };
        using var response = await _plainClient.SendAsync(
            tokenRequest,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = JsonSerializer.Deserialize<OAuthTokenResponse>(json);
        return string.IsNullOrWhiteSpace(token?.AccessToken)
            ? throw new InvalidOperationException(
                "The OAuth token response did not contain an access token.")
            : token.AccessToken;
    }

    private static HttpClient CreateClient() => new(
        ApiDestinationProfileNetworkPolicy.CreateSocketsHttpHandler(),
        true)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private sealed record MtlsSecret(string PfxBase64, string? Password);
    private sealed record OAuthSecret(
        string TokenEndpoint,
        string ClientId,
        string ClientSecret,
        string? Scope);
    private sealed record OAuthTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")]
        string AccessToken);
}
