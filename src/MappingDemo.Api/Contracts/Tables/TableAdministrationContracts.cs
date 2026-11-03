using MappingDemo.Shared.Tables;

namespace MappingDemo.Api.Contracts.Tables;

public sealed record TableAdminCapabilities(
    bool CanRead,
    bool CanCreate,
    bool CanAddColumn,
    bool MutationsEnabled);

public sealed record TableReferenceResponse(
    long ConfigId,
    string ConfigName,
    string Usage,
    long? ConfigVersionId,
    int? VersionNo,
    bool IsActiveVersion,
    long PinnedJobCount);

public sealed record TableAuditContext(
    string OperatorId,
    string Reason,
    string CorrelationId);
