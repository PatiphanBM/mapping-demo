using System.Collections.ObjectModel;

namespace MappingDemo.Shared.Contracts;

public sealed record ApiDestinationProfile(
    string Id,
    string DisplayName,
    bool Enabled,
    string SchemaBaseUrl,
    string SchemaPathTemplate,
    string DeliveryBaseUrl,
    string DeliveryPathTemplate,
    string Method,
    IReadOnlyList<string> AllowedHeaderNames,
    string AuthSecretReference,
    int TimeoutSeconds,
    IReadOnlyList<SupportedContract> SupportedContracts,
    IReadOnlyList<PayloadBinding> PayloadBindings,
    string AuthScheme = "None",
    string IdempotencyHeaderName = "Idempotency-Key",
    string CorrelationHeaderName = "X-Correlation-Id",
    string Environment = "Production",
    long ProfileVersionId = 0,
    int VersionNo = 1,
    string Lifecycle = "Active",
    string NetworkPolicyReference = "partner-egress-default",
    string ContentHash = "") : IContractProfile;

public sealed record SupportedContract(
    string Id,
    string Version,
    string Reference);

public sealed record PayloadBinding(
    string TargetPath,
    RuntimeValueSource Source);

public enum RuntimeValueSource
{
    ContractReference,
    FileJobId,
    FileName,
    SourceRowId,
    SourceRowNumber,
    ConfigVersionReference,
    OutputKey
}

public sealed record ApiDestinationProfileMetadata(
    string Id,
    string DisplayName,
    bool Enabled,
    string Method,
    string DeliveryPathTemplate,
    string Environment,
    IReadOnlyList<string> AllowedHeaderNames,
    IReadOnlyList<SupportedContract> SupportedContracts,
    IReadOnlyList<PayloadBinding> PayloadBindings,
    long ProfileVersionId = 0,
    int VersionNo = 1,
    string Lifecycle = "Active",
    string ContentHash = "");

public interface IApiDestinationProfileCatalog
{
    ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>> GetSafeProfilesAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ProfileResolutionResult> ResolveActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        long profileVersionId,
        CancellationToken cancellationToken = default);
}

public sealed class ApiDestinationProfileCatalog : IApiDestinationProfileCatalog
{
    private readonly IReadOnlyDictionary<string, ApiDestinationProfile> _profiles;

    public ApiDestinationProfileCatalog(IEnumerable<ApiDestinationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var diagnostics = new List<ContractDiagnostic>();
        var values = new Dictionary<string, ApiDestinationProfile>(
            StringComparer.Ordinal);
        var versionIds = new HashSet<long>();
        foreach (var profile in profiles)
        {
            Validate(profile, diagnostics);
            if (!string.IsNullOrWhiteSpace(profile.Id) &&
                !values.TryAdd(profile.Id, profile))
            {
                diagnostics.Add(new ContractDiagnostic(
                    "profiles",
                    "DuplicateProfileId",
                    $"API destination profile '{profile.Id}' is duplicated."));
            }

            if (profile.ProfileVersionId > 0 &&
                !versionIds.Add(profile.ProfileVersionId))
            {
                diagnostics.Add(new ContractDiagnostic(
                    "profiles.profileVersionId",
                    "DuplicateProfileVersionId",
                    $"API destination profile version '{profile.ProfileVersionId}' is duplicated."));
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new ApiDestinationProfileValidationException(diagnostics);
        }

        _profiles = new ReadOnlyDictionary<string, ApiDestinationProfile>(values);
    }

    public IReadOnlyList<ApiDestinationProfileMetadata> GetSafeProfiles() =>
        _profiles.Values
            .Where(profile =>
                profile.Enabled && profile.Lifecycle == "Active")
            .OrderBy(profile => profile.DisplayName, StringComparer.Ordinal)
            .Select(profile => new ApiDestinationProfileMetadata(
                profile.Id,
                profile.DisplayName,
                profile.Enabled,
                profile.Method,
                profile.DeliveryPathTemplate,
                profile.Environment,
                profile.AllowedHeaderNames.ToArray(),
                profile.SupportedContracts.ToArray(),
                profile.PayloadBindings.ToArray(),
                profile.ProfileVersionId,
                profile.VersionNo,
                profile.Lifecycle,
                profile.ContentHash))
            .ToArray();

    public ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>>
        GetSafeProfilesAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(GetSafeProfiles());

    public ValueTask<ProfileResolutionResult> ResolveActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ResolveForCreation(profileId));

    public ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        long profileVersionId,
        CancellationToken cancellationToken = default)
    {
        if (profileVersionId <= 0)
        {
            return ValueTask.FromResult(ProfileResolutionResult.Failure(
                "UnknownPinnedProfileVersion",
                $"API destination profile version '{profileVersionId}' does not exist."));
        }

        var profile = _profiles.Values.SingleOrDefault(value =>
            value.ProfileVersionId == profileVersionId);
        return ValueTask.FromResult(profile is null
            ? ProfileResolutionResult.Failure(
                "UnknownPinnedProfileVersion",
                $"API destination profile version '{profileVersionId}' does not exist.")
            : ProfileResolutionResult.Success(profile));
    }

    public ProfileResolutionResult ResolveForCreation(string profileId)
    {
        if (!_profiles.TryGetValue(profileId, out var profile))
        {
            return ProfileResolutionResult.Failure(
                "UnknownProfile",
                $"API destination profile '{profileId}' does not exist.");
        }

        return profile.Enabled && profile.Lifecycle == "Active"
            ? ProfileResolutionResult.Success(profile)
            : ProfileResolutionResult.Failure(
                "ProfileDisabled",
                $"API destination profile '{profileId}' is disabled.");
    }

    private static void Validate(
        ApiDestinationProfile profile,
        ICollection<ContractDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            diagnostics.Add(new("profiles.id", "ProfileIdRequired", "Profile ID is required."));
        }

        ValidateUrl(profile.SchemaBaseUrl, "Schema", diagnostics);
        ValidateUrl(profile.DeliveryBaseUrl, "Delivery", diagnostics);
        ValidateTemplate(profile.SchemaPathTemplate, "schemaPathTemplate", diagnostics);
        ValidateTemplate(profile.DeliveryPathTemplate, "deliveryPathTemplate", diagnostics);

        if (profile.Method is not ("POST" or "PUT" or "PATCH"))
        {
            diagnostics.Add(new("profiles.method", "InvalidHttpMethod", "Method must be POST, PUT or PATCH."));
        }

        if (profile.TimeoutSeconds is < 1 or > 120)
        {
            diagnostics.Add(new("profiles.timeoutSeconds", "InvalidTimeout", "Timeout must be between 1 and 120 seconds."));
        }

        if (profile.AuthScheme is not ("None" or "Mtls" or "OAuth2ClientCredentials"))
        {
            diagnostics.Add(new("profiles.authScheme", "InvalidAuthScheme", "Auth scheme must be None, Mtls or OAuth2ClientCredentials."));
        }

        if (profile.Environment is not ("Stub" or "Sandbox" or "Production"))
        {
            diagnostics.Add(new(
                "profiles.environment",
                "InvalidEnvironment",
                "Environment must be Stub, Sandbox or Production."));
        }

        var hasValidAuthReference =
            !string.IsNullOrWhiteSpace(profile.AuthSecretReference) &&
            profile.AuthSecretReference.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_' or '.');
        if ((profile.AuthScheme == "None" &&
                !string.IsNullOrWhiteSpace(profile.AuthSecretReference)) ||
            (profile.AuthScheme != "None" && !hasValidAuthReference))
        {
            diagnostics.Add(new(
                "profiles.authSecretReference",
                "InvalidAuthReference",
                "Auth reference must match the selected authentication scheme."));
        }

        var headerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var forbiddenHeaders = new HashSet<string>(
            [
                "Authorization",
                "Proxy-Authorization",
                "Cookie",
                "Set-Cookie",
                "Host",
                "Content-Length",
                "Transfer-Encoding",
                "Connection",
                "Keep-Alive",
                "TE",
                "Trailer",
                "Upgrade"
            ],
            StringComparer.OrdinalIgnoreCase);
        foreach (var header in profile.AllowedHeaderNames)
        {
            if (string.IsNullOrWhiteSpace(header) ||
                header.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-')) ||
                forbiddenHeaders.Contains(header))
            {
                diagnostics.Add(new("profiles.allowedHeaderNames", "InvalidAllowedHeader", $"Header '{header}' is not allowed."));
                continue;
            }
            if (!headerNames.Add(header))
            {
                diagnostics.Add(new(
                    "profiles.allowedHeaderNames",
                    "DuplicateAllowedHeader",
                    $"Header '{header}' is duplicated."));
            }
        }


        if (profile.AuthScheme != "None")
        {
            ValidateRequiredHeader(
                profile.IdempotencyHeaderName,
                "idempotencyHeaderName",
                profile.AllowedHeaderNames,
                diagnostics);
            ValidateRequiredHeader(
                profile.CorrelationHeaderName,
                "correlationHeaderName",
                profile.AllowedHeaderNames,
                diagnostics);
        }
    }

    private static void ValidateRequiredHeader(
        string header,
        string path,
        IReadOnlyList<string> allowedHeaders,
        ICollection<ContractDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(header) ||
            !allowedHeaders.Contains(header, StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(
                $"profiles.{path}",
                "RequiredHeaderNotAllowed",
                $"Header '{header}' must be present in allowedHeaderNames."));
        }
    }

    private static void ValidateUrl(
        string value,
        string label,
        ICollection<ContractDiagnostic> diagnostics)
    {
        if (!ApiDestinationProfileNetworkPolicy.TryValidateOrigin(
            value,
            out var code,
            out var message))
        {
            diagnostics.Add(new(
                $"profiles.{char.ToLowerInvariant(label[0])}{label[1..]}BaseUrl",
                code == "InvalidProfileBaseUrl"
                    ? $"Invalid{label}BaseUrl"
                    : code,
                message));
        }
    }

    private static void ValidateTemplate(
        string value,
        string path,
        ICollection<ContractDiagnostic> diagnostics)
    {
        var allowedToken = path == "schemaPathTemplate"
            ? "{contractReference}"
            : "{contractVersion}";
        var withoutAllowedTokens = value
            .Replace("{contractReference}", string.Empty, StringComparison.Ordinal)
            .Replace(allowedToken, string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('?') ||
            value.Contains('#') ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            value.Any(char.IsControl) ||
            withoutAllowedTokens.Contains('{') ||
            withoutAllowedTokens.Contains('}'))
        {
            diagnostics.Add(new($"profiles.{path}", "InvalidPathTemplate", "Path template must be a safe absolute path template."));
        }
    }
}

public sealed record ProfileResolutionResult(
    ApiDestinationProfile? Profile,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Profile is not null;

    public static ProfileResolutionResult Success(ApiDestinationProfile profile) =>
        new(profile, []);

    public static ProfileResolutionResult Failure(string code, string message) =>
        new(null, [new ContractDiagnostic("profileId", code, message)]);
}

public sealed class ApiDestinationProfileValidationException(
    IReadOnlyList<ContractDiagnostic> diagnostics)
    : Exception("One or more API destination profiles are invalid.")
{
    public IReadOnlyList<ContractDiagnostic> Diagnostics { get; } = diagnostics;
}
