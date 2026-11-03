using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Options;

public sealed class FileDestinationProfilesOptions
{
    public const string SectionName = "FileDestinationProfiles";

    public List<FileDestinationProfileOptions> Profiles { get; init; } = [];

    public FileDestinationProfileCatalog BuildCatalog() =>
        new(Profiles.Select(profile => profile.ToDomain()).ToArray());
}

public sealed class FileDestinationProfileOptions
{
    public string Id { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public string OutputRoot { get; init; } = string.Empty;

    public string EncryptionPolicy { get; init; } = string.Empty;

    public long QuotaBytes { get; init; }

    public long MinimumFreeBytes { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int TimeoutSeconds { get; init; } = 10;

    public List<SupportedContractOptions> SupportedContracts { get; init; } = [];

    public List<PayloadBindingOptions> PayloadBindings { get; init; } = [];

    public FileDestinationProfile ToDomain() =>
        new(
            Id,
            Enabled,
            OutputRoot,
            EncryptionPolicy,
            QuotaBytes,
            MinimumFreeBytes,
            UpdatedAt,
            TimeoutSeconds,
            SupportedContracts.Select(item => new SupportedContract(
                item.Id,
                item.Version,
                item.Reference)).ToArray(),
            PayloadBindings.Select(item => new PayloadBinding(
                item.TargetPath,
                item.Source)).ToArray());
}
