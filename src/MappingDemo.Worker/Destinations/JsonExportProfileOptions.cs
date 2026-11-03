using MappingDemo.Shared.Contracts;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportProfileOptions
{
    public List<JsonExportProfileItem> Profiles { get; init; } = [];

    public FileDestinationProfileCatalog BuildCatalog() =>
        new(Profiles.Select(profile => profile.ToDomain()));
}

public sealed class JsonExportProfileItem
{
    public string Id { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string OutputRoot { get; init; } = string.Empty;
    public string EncryptionPolicy { get; init; } = string.Empty;
    public long QuotaBytes { get; init; }
    public long MinimumFreeBytes { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
    public List<JsonExportSupportedContract> SupportedContracts { get; init; } = [];
    public List<JsonExportPayloadBinding> PayloadBindings { get; init; } = [];

    public FileDestinationProfile ToDomain() => new(
        Id,
        Enabled,
        OutputRoot,
        EncryptionPolicy,
        QuotaBytes,
        MinimumFreeBytes,
        UpdatedAt,
        TimeoutSeconds,
        SupportedContracts.Select(contract => new SupportedContract(
            contract.Id,
            contract.Version,
            contract.Reference)).ToArray(),
        PayloadBindings.Select(binding => new PayloadBinding(
            binding.TargetPath,
            binding.Source)).ToArray());
}

public sealed class JsonExportSupportedContract
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

public sealed class JsonExportPayloadBinding
{
    public string TargetPath { get; init; } = string.Empty;
    public RuntimeValueSource Source { get; init; }
}
