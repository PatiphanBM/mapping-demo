using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Options;

public sealed class ApiDestinationProfilesOptions
{
    public const string SectionName = "ApiDestinationProfiles";

    public List<ApiDestinationProfileOptions> Profiles { get; init; } = [];

    public ApiDestinationProfileCatalog BuildCatalog() =>
        new(Profiles.Select(profile => profile.ToDomain()).ToArray());
}

public sealed class ApiDestinationProfileOptions
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

    public int TimeoutSeconds { get; init; } = 10;

    public string AuthScheme { get; init; } = "None";

    public string IdempotencyHeaderName { get; init; } = "Idempotency-Key";

    public string CorrelationHeaderName { get; init; } = "X-Correlation-Id";

    public string Environment { get; init; } = "Production";

    public List<SupportedContractOptions> SupportedContracts { get; init; } = [];

    public List<PayloadBindingOptions> PayloadBindings { get; init; } = [];

    public ApiDestinationProfile ToDomain() =>
        new(
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
            SupportedContracts.Select(item => new SupportedContract(
                item.Id,
                item.Version,
                item.Reference)).ToArray(),
            PayloadBindings.Select(item => new PayloadBinding(
                item.TargetPath,
                item.Source)).ToArray(),
            AuthScheme,
            IdempotencyHeaderName,
            CorrelationHeaderName,
            Environment);
}

public sealed class SupportedContractOptions
{
    public string Id { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;
}

public sealed class PayloadBindingOptions
{
    public string TargetPath { get; init; } = string.Empty;

    public RuntimeValueSource Source { get; init; }
}
