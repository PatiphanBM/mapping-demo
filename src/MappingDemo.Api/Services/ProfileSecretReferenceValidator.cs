using System.Text.Json;

namespace MappingDemo.Api.Services;

public sealed record ProfileSecretReferenceValidation(
    string Status,
    string? DiagnosticCode = null,
    string? DiagnosticMessage = null)
{
    public bool IsValid => Status is "Unchecked" or "Present";
}

public interface IProfileSecretReferenceValidator
{
    ValueTask<ProfileSecretReferenceValidation> ValidateAsync(
        string authScheme,
        string? reference,
        CancellationToken cancellationToken = default);
}

public sealed class EnvironmentProfileSecretReferenceValidator :
    IProfileSecretReferenceValidator
{
    public ValueTask<ProfileSecretReferenceValidation> ValidateAsync(
        string authScheme,
        string? reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authScheme == "None")
        {
            return ValueTask.FromResult(
                new ProfileSecretReferenceValidation("Unchecked"));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return ValueTask.FromResult(Missing());
        }

        var value = Environment.GetEnvironmentVariable(
            $"MAPPING_SECRET__{Normalize(reference)}");
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValueTask.FromResult(Missing());
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            var valid = authScheme switch
            {
                "Mtls" => HasText(root, "pfxBase64"),
                "OAuth2ClientCredentials" =>
                    HasText(root, "clientId") &&
                    HasText(root, "clientSecret") &&
                    HasSafeTokenEndpoint(root),
                _ => false
            };
            return ValueTask.FromResult(valid
                ? new ProfileSecretReferenceValidation("Present")
                : new ProfileSecretReferenceValidation(
                    "InvalidShape",
                    "InvalidAuthSecretShape",
                    "The referenced authentication secret has an invalid shape."));
        }
        catch (JsonException)
        {
            return ValueTask.FromResult(new ProfileSecretReferenceValidation(
                "InvalidShape",
                "InvalidAuthSecretShape",
                "The referenced authentication secret has an invalid shape."));
        }
    }

    private static ProfileSecretReferenceValidation Missing() => new(
        "Missing",
        "MissingAuthSecretReference",
        "The referenced authentication secret does not exist.");

    private static string Normalize(string reference) =>
        string.Concat(reference.Select(character =>
            char.IsAsciiLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_'));

    private static bool HasText(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(property.GetString());

    private static bool HasSafeTokenEndpoint(JsonElement root)
    {
        if (!HasText(root, "tokenEndpoint") ||
            !Uri.TryCreate(
                root.GetProperty("tokenEndpoint").GetString(),
                UriKind.Absolute,
                out var endpoint))
        {
            return false;
        }

        return endpoint.Scheme == Uri.UriSchemeHttps &&
            !endpoint.IsLoopback &&
            !System.Net.IPAddress.TryParse(endpoint.Host, out _);
    }
}
