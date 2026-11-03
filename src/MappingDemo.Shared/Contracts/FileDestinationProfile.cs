using System.Collections.ObjectModel;

namespace MappingDemo.Shared.Contracts;

public sealed record FileDestinationProfile(
    string Id,
    bool Enabled,
    string OutputRoot,
    string EncryptionPolicy,
    long QuotaBytes,
    long MinimumFreeBytes,
    DateTimeOffset UpdatedAt,
    int TimeoutSeconds,
    IReadOnlyList<SupportedContract> SupportedContracts,
    IReadOnlyList<PayloadBinding> PayloadBindings) : IContractProfile;

public sealed record FileDestinationProfileMetadata(
    string Id,
    bool Enabled,
    IReadOnlyList<SupportedContract> SupportedContracts,
    IReadOnlyList<PayloadBinding> PayloadBindings);

public sealed class FileDestinationProfileCatalog
{
    private readonly IReadOnlyDictionary<string, FileDestinationProfile> _profiles;

    public FileDestinationProfileCatalog(
        IEnumerable<FileDestinationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var diagnostics = new List<ContractDiagnostic>();
        var values = new Dictionary<string, FileDestinationProfile>(
            StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            Validate(profile, diagnostics);
            if (!string.IsNullOrWhiteSpace(profile.Id) &&
                !values.TryAdd(profile.Id, profile))
            {
                diagnostics.Add(new ContractDiagnostic(
                    "profiles",
                    "DuplicateProfileId",
                    $"File destination profile '{profile.Id}' is duplicated."));
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new FileDestinationProfileValidationException(diagnostics);
        }

        _profiles = new ReadOnlyDictionary<string, FileDestinationProfile>(
            values);
    }

    public IReadOnlyList<FileDestinationProfileMetadata> GetSafeProfiles() =>
        _profiles.Values
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .Select(profile => new FileDestinationProfileMetadata(
                profile.Id,
                profile.Enabled,
                profile.SupportedContracts.ToArray(),
                profile.PayloadBindings.ToArray()))
            .ToArray();

    public FileProfileResolutionResult ResolveForCreation(string profileId)
    {
        if (!_profiles.TryGetValue(profileId, out var profile))
        {
            return FileProfileResolutionResult.Failure(
                "UnknownProfile",
                $"File destination profile '{profileId}' does not exist.");
        }

        return profile.Enabled
            ? FileProfileResolutionResult.Success(profile)
            : FileProfileResolutionResult.Failure(
                "ProfileDisabled",
                $"File destination profile '{profileId}' is disabled.");
    }

    public FileDestinationProfile? ResolvePinned(string profileId) =>
        _profiles.GetValueOrDefault(profileId);

    private static void Validate(
        FileDestinationProfile profile,
        ICollection<ContractDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) ||
            profile.Id.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_' or '.')))
        {
            diagnostics.Add(new(
                "profiles.id",
                "InvalidProfileId",
                "Profile ID must be a logical identifier."));
        }
        if (string.IsNullOrWhiteSpace(profile.OutputRoot) ||
            !Path.IsPathFullyQualified(profile.OutputRoot))
        {
            diagnostics.Add(new(
                "profiles.outputRoot",
                "InvalidOutputRoot",
                "Output Root must be an absolute server-owned path."));
        }
        if (profile.EncryptionPolicy != "volume-encrypted")
        {
            diagnostics.Add(new(
                "profiles.encryptionPolicy",
                "InvalidEncryptionPolicy",
                "Encryption policy must be volume-encrypted."));
        }
        if (profile.QuotaBytes <= 0 ||
            profile.MinimumFreeBytes < 0 ||
            profile.MinimumFreeBytes > profile.QuotaBytes)
        {
            diagnostics.Add(new(
                "profiles.quotaBytes",
                "InvalidQuota",
                "Quota and minimum free bytes are invalid."));
        }
        if (profile.UpdatedAt == default)
        {
            diagnostics.Add(new(
                "profiles.updatedAt",
                "UpdatedAtRequired",
                "Profile update time is required."));
        }
        if (profile.TimeoutSeconds is < 1 or > 120)
        {
            diagnostics.Add(new(
                "profiles.timeoutSeconds",
                "InvalidTimeout",
                "Timeout must be between 1 and 120 seconds."));
        }
        if (profile.SupportedContracts.Count == 0)
        {
            diagnostics.Add(new(
                "profiles.supportedContracts",
                "SupportedContractRequired",
                "At least one supported contract is required."));
        }
    }
}

public sealed record FileProfileResolutionResult(
    FileDestinationProfile? Profile,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Profile is not null;

    public static FileProfileResolutionResult Success(
        FileDestinationProfile profile) => new(profile, []);

    public static FileProfileResolutionResult Failure(
        string code,
        string message) =>
        new(null, [new ContractDiagnostic("profileId", code, message)]);
}

public sealed class FileDestinationProfileValidationException(
    IReadOnlyList<ContractDiagnostic> diagnostics)
    : Exception("One or more File destination profiles are invalid.")
{
    public IReadOnlyList<ContractDiagnostic> Diagnostics { get; } = diagnostics;
}
