using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MappingDemo.Shared.Contracts;

public static class ApiDestinationProfileCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string ComputeContentHash(ApiDestinationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var content = new ProfileContent(
            profile.Environment,
            profile.SchemaBaseUrl,
            profile.SchemaPathTemplate,
            profile.DeliveryBaseUrl,
            profile.DeliveryPathTemplate,
            profile.Method,
            profile.AuthScheme,
            string.IsNullOrWhiteSpace(profile.AuthSecretReference)
                ? null
                : profile.AuthSecretReference,
            profile.NetworkPolicyReference,
            profile.TimeoutSeconds,
            profile.IdempotencyHeaderName,
            profile.CorrelationHeaderName,
            profile.AllowedHeaderNames,
            profile.SupportedContracts,
            profile.PayloadBindings);
        var canonical = JsonSerializer.Serialize(content, JsonOptions);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()}";
    }

    private sealed record ProfileContent(
        string Environment,
        string SchemaBaseUrl,
        string SchemaPathTemplate,
        string DeliveryBaseUrl,
        string DeliveryPathTemplate,
        string Method,
        string AuthScheme,
        string? AuthSecretReference,
        string NetworkPolicyReference,
        int TimeoutSeconds,
        string IdempotencyHeaderName,
        string CorrelationHeaderName,
        IReadOnlyList<string> AllowedHeaderNames,
        IReadOnlyList<SupportedContract> SupportedContracts,
        IReadOnlyList<PayloadBinding> PayloadBindings);
}
