using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Contracts.ApiDestinationProfiles;

public sealed record ApiDestinationProfileDraftRequest(
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

public sealed record CreateApiDestinationProfileRequest(
    string ProfileKey,
    string DisplayName,
    ApiDestinationProfileDraftRequest Draft,
    string Reason);

public sealed record UpdateApiDestinationProfileDraftRequest(
    ApiDestinationProfileDraftRequest Draft,
    string Reason);

public sealed record ApiDestinationProfileActionRequest(string Reason);

public sealed record ApiDestinationProfileVersionAdminResponse(
    long ProfileVersionId,
    int VersionNo,
    string Lifecycle,
    string Environment,
    string? SchemaBaseUrl,
    string SchemaPathTemplate,
    string? DeliveryBaseUrl,
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
    IReadOnlyList<PayloadBinding> PayloadBindings,
    string ContentHash,
    long ConcurrencyToken,
    DateTimeOffset? ValidatedAt);

public sealed record ApiDestinationProfileAdminResponse(
    string ProfileKey,
    string DisplayName,
    long ConcurrencyToken,
    int? ActiveVersionNo,
    IReadOnlyList<ApiDestinationProfileVersionAdminResponse> Versions);

public sealed record ApiDestinationProfileValidationResponse(
    bool IsValid,
    string ContentHash,
    DateTimeOffset ValidatedAt,
    string SecretStatus,
    IReadOnlyList<ContractDiagnostic> Diagnostics);

public sealed record ApiDestinationProfileAdminCapabilitiesResponse(
    bool CanReadEndpoints,
    bool CanWriteDraft,
    bool CanActivate,
    bool CanActivateProduction,
    bool CanDisable);
