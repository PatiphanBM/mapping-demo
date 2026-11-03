using System.Text.Json;
using System.Text.RegularExpressions;

namespace MappingDemo.Shared.Contracts;

public static partial class ContractSchemaContentPolicy
{
    private static readonly string[] SensitiveValueMarkers =
    [
        "-----BEGIN PRIVATE KEY-----",
        "-----BEGIN RSA PRIVATE KEY-----",
        "Bearer ",
        "Basic "
    ];

    public static ContractDiagnostic? Validate(string schema)
    {
        if (SensitiveValueMarkers.Any(marker =>
            schema.Contains(marker, StringComparison.OrdinalIgnoreCase)) ||
            AwsAccessKey().IsMatch(schema))
        {
            return SensitiveDiagnostic();
        }

        try
        {
            using var document = JsonDocument.Parse(schema);
            return ContainsCredentialValue(document.RootElement)
                ? SensitiveDiagnostic()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ContainsCredentialValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (IsCredentialName(property.Name) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return true;
                }
                if (ContainsCredentialValue(property.Value))
                {
                    return true;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Any(ContainsCredentialValue);
        }
        return false;
    }

    private static bool IsCredentialName(string name) =>
        name.Equals("password", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("clientSecret", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("accessToken", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("privateKey", StringComparison.OrdinalIgnoreCase);

    private static ContractDiagnostic SensitiveDiagnostic() => new(
        "schema",
        "SensitiveSchemaContent",
        "Contract schema must not contain credentials, tokens, or private keys.");

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.CultureInvariant)]
    private static partial Regex AwsAccessKey();
}
