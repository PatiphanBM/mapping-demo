using MappingDemo.Shared.Contracts;

namespace MappingDemo.Worker.Delivery;

public sealed class ApiDeliveryProfileOptions
{
    public List<ApiDeliveryProfileItem> Profiles { get; init; } = [];

    public ApiDestinationProfileCatalog BuildCatalog() =>
        new(Profiles.Select(profile => profile.ToDomain()));
}

public sealed class ApiDeliveryProfileItem
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string SchemaBaseUrl { get; init; } = string.Empty;
    public string SchemaPathTemplate { get; init; } = string.Empty;
    public string DeliveryBaseUrl { get; init; } = string.Empty;
    public string DeliveryPathTemplate { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public List<string> AllowedHeaderNames { get; init; } = [];
    public string AuthSecretReference { get; init; } = string.Empty;
    public string AuthScheme { get; init; } = "None";
    public string IdempotencyHeaderName { get; init; } = "Idempotency-Key";
    public string CorrelationHeaderName { get; init; } = "X-Correlation-Id";

    public string Environment { get; init; } = "Production";
    public int TimeoutSeconds { get; init; } = 10;
    public List<ApiDeliverySupportedContract> SupportedContracts { get; init; } = [];
    public List<ApiDeliveryPayloadBinding> PayloadBindings { get; init; } = [];

    public ApiDestinationProfile ToDomain() => new(
        Id,
        DisplayName,
        Enabled,
        SchemaBaseUrl,
        SchemaPathTemplate,
        DeliveryBaseUrl,
        DeliveryPathTemplate,
        Method,
        AllowedHeaderNames,
        AuthSecretReference,
        TimeoutSeconds,
        SupportedContracts.Select(contract => new SupportedContract(
            contract.Id,
            contract.Version,
            contract.Reference)).ToArray(),
        PayloadBindings.Select(binding => new PayloadBinding(
            binding.TargetPath,
            binding.Source)).ToArray(),
        AuthScheme,
        IdempotencyHeaderName,
            CorrelationHeaderName,
            Environment);
}

public sealed class ApiDeliverySupportedContract
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}

public sealed class ApiDeliveryPayloadBinding
{
    public string TargetPath { get; init; } = string.Empty;
    public RuntimeValueSource Source { get; init; }
}
