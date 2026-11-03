using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Contracts.Contracts;

public sealed record CreateContractDraftRequest(
    string ContractKey,
    string DisplayName,
    string Version,
    string Schema,
    string Reason);

public sealed record UpdateContractDraftRequest(
    string Schema,
    string Reason);

public sealed record CloneContractVersionRequest(
    string Version,
    string Reason);

public sealed record PublishContractVersionRequest(
    bool AcknowledgeBreakingChange,
    string Reason);

public sealed record ContractActionRequest(string Reason);

public sealed record ContractAdminCapabilitiesResponse(
    bool CanWriteDraft,
    bool CanPublish);

public sealed record ContractValidationResponse(
    bool IsValid,
    string? CanonicalSchema,
    string? SchemaHash,
    IReadOnlyList<ContractPathDescriptor> Paths,
    ContractCompatibility? Compatibility,
    long? CompatibilityBaseVersionId,
    IReadOnlyList<ContractDiagnostic> Diagnostics);
