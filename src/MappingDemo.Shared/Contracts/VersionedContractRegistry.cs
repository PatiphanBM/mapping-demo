using System.Collections.Concurrent;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Shared.Contracts;

public enum ContractVersionLifecycle
{
    Draft,
    Published,
    Retired
}

public enum ContractCompatibility
{
    Initial,
    NonBreaking,
    Breaking
}

public sealed record ContractVersionSummary(
    long ContractVersionId,
    string ContractKey,
    string DisplayName,
    string Version,
    string Reference,
    ContractVersionLifecycle Lifecycle,
    string? SchemaHash,
    long ConcurrencyToken);

public sealed record ContractVersionDetails(
    long ContractVersionId,
    long ContractId,
    string ContractKey,
    string DisplayName,
    string Version,
    string Reference,
    ContractVersionLifecycle Lifecycle,
    string DraftSchema,
    string? CanonicalSchema,
    string? SchemaHash,
    ContractCompatibility? Compatibility,
    long? CompatibilityBaseVersionId,
    long ConcurrencyToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt);

public sealed record ContractAggregate(
    long ContractId,
    string ContractKey,
    string DisplayName,
    long ConcurrencyToken,
    IReadOnlyList<ContractVersionDetails> Versions);

public sealed record ResolvedContractVersion(
    ContractVersionDetails Version,
    TargetSchema TargetSchema,
    IReadOnlyList<ContractPathDescriptor> PathCatalog);

public sealed record ContractValidationResult(
    bool IsValid,
    string? CanonicalSchema,
    string? SchemaHash,
    TargetSchema? TargetSchema,
    IReadOnlyList<ContractPathDescriptor> PathCatalog,
    ContractCompatibility? Compatibility,
    long? CompatibilityBaseVersionId,
    IReadOnlyList<ContractDiagnostic> Diagnostics);

public sealed record ContractAggregateResult(
    ContractAggregate? Contract,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Contract is not null;
}

public sealed record ContractVersionResult(
    ResolvedContractVersion? ContractVersion,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => ContractVersion is not null;
}

public sealed record ContractMutationResult(
    ContractVersionDetails? Version,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Version is not null;
}

public abstract record ContractRegistryCommand(
    string Actor,
    string Reason,
    string RequestId);

public sealed record CreateContractDraftCommand(
    string ContractKey,
    string DisplayName,
    string Version,
    string Schema,
    string Actor,
    string Reason,
    string RequestId) : ContractRegistryCommand(Actor, Reason, RequestId);

public sealed record UpdateContractDraftCommand(
    long ContractVersionId,
    long ExpectedConcurrencyToken,
    string Schema,
    string Actor,
    string Reason,
    string RequestId) : ContractRegistryCommand(Actor, Reason, RequestId);

public sealed record CloneContractVersionCommand(
    long SourceContractVersionId,
    string NewVersion,
    string Actor,
    string Reason,
    string RequestId) : ContractRegistryCommand(Actor, Reason, RequestId);

public sealed record PublishContractVersionCommand(
    long ContractVersionId,
    long ExpectedConcurrencyToken,
    bool AcknowledgeBreakingChange,
    string Actor,
    string Reason,
    string RequestId) : ContractRegistryCommand(Actor, Reason, RequestId);

public sealed record RetireContractVersionCommand(
    long ContractVersionId,
    long ExpectedConcurrencyToken,
    string Actor,
    string Reason,
    string RequestId) : ContractRegistryCommand(Actor, Reason, RequestId);

public interface IVersionedContractRegistry
{
    ValueTask<IReadOnlyList<ContractVersionSummary>> ListAsync(
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ContractVersionSummary>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ContractAggregateResult> GetAsync(
        string contractKey,
        CancellationToken cancellationToken = default);

    ValueTask<ContractVersionResult> ResolveExactAsync(
        string reference,
        CancellationToken cancellationToken = default);

    ValueTask<ContractValidationResult> ValidateAsync(
        long contractVersionId,
        CancellationToken cancellationToken = default);

    ValueTask<ContractMutationResult> ExecuteAsync(
        ContractRegistryCommand command,
        CancellationToken cancellationToken = default);
}

public interface IVersionedContractRegistrySource : IVersionedContractRegistry;

public sealed record ContractRegistryCachePolicy
{
    public static ContractRegistryCachePolicy Default { get; } = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromSeconds(15));

    public ContractRegistryCachePolicy(
        TimeSpan exactTimeToLive,
        TimeSpan publishedListTimeToLive)
    {
        if (exactTimeToLive <= TimeSpan.Zero ||
            exactTimeToLive > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(exactTimeToLive));
        }
        if (publishedListTimeToLive <= TimeSpan.Zero ||
            publishedListTimeToLive > TimeSpan.FromSeconds(15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishedListTimeToLive));
        }
        ExactTimeToLive = exactTimeToLive;
        PublishedListTimeToLive = publishedListTimeToLive;
    }

    public TimeSpan ExactTimeToLive { get; }
    public TimeSpan PublishedListTimeToLive { get; }
}

public sealed class VersionedContractRegistryModule(
    IVersionedContractRegistrySource source,
    ContractRegistryCachePolicy? cachePolicy = null,
    TimeProvider? timeProvider = null,
    ContractRegistryObservability? observability = null) :
    IVersionedContractRegistry
{
    private const int MaximumExactCacheEntries = 512;
    private readonly ContractRegistryCachePolicy _cachePolicy =
        cachePolicy ?? ContractRegistryCachePolicy.Default;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, ExactCacheEntry> _exact =
        new(StringComparer.Ordinal);
    private PublishedCacheEntry? _published;

    public async ValueTask<IReadOnlyList<ContractVersionSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await source.ListAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async ValueTask<IReadOnlyList<ContractVersionSummary>>
        ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cached = Volatile.Read(ref _published);
        if (cached is not null && cached.ExpiresAt > now)
        {
            observability?.RecordCacheAccess("published_list", hit: true);
            return cached.Versions;
        }
        observability?.RecordCacheAccess("published_list", hit: false);
        try
        {
            var versions = await source.ListPublishedAsync(cancellationToken);
            var snapshot = versions.ToArray();
            Volatile.Write(
                ref _published,
                new PublishedCacheEntry(
                    snapshot,
                    now + _cachePolicy.PublishedListTimeToLive));
            return snapshot;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordResolutionFailure(
                "ContractRegistryUnavailable");
            return [];
        }
    }

    public async ValueTask<ContractAggregateResult> GetAsync(
        string contractKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await source.GetAsync(contractKey, cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return AggregateUnavailable();
        }
    }

    public async ValueTask<ContractVersionResult> ResolveExactAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        if (_exact.TryGetValue(reference, out var cached) &&
            cached.ExpiresAt > now)
        {
            observability?.RecordCacheAccess("exact", hit: true);
            return cached.Result;
        }
        observability?.RecordCacheAccess("exact", hit: false);

        var startedAt = _timeProvider.GetTimestamp();
        try
        {
            var result = await source.ResolveExactAsync(
                reference,
                cancellationToken);
            if (result.IsSuccess)
            {
                if (_exact.Count >= MaximumExactCacheEntries)
                {
                    RemoveOldestExactEntry();
                }
                _exact[reference] = new ExactCacheEntry(
                    result,
                    now + _cachePolicy.ExactTimeToLive,
                    now);
            }
            else
            {
                observability?.RecordResolutionFailure(
                    result.Diagnostics.FirstOrDefault()?.Code ?? "Unknown");
            }
            return result;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordResolutionFailure(
                "ContractRegistryUnavailable");
            return VersionUnavailable();
        }
        finally
        {
            observability?.RecordResolveLatency(
                _timeProvider.GetElapsedTime(startedAt));
        }
    }

    public async ValueTask<ContractValidationResult> ValidateAsync(
        long contractVersionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await source.ValidateAsync(
                contractVersionId,
                cancellationToken);
            observability?.RecordValidation(result.IsValid);
            return result;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordValidation(success: false);
            return ValidationUnavailable();
        }
    }

    public async ValueTask<ContractMutationResult> ExecuteAsync(
        ContractRegistryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await source.ExecuteAsync(command, cancellationToken);
            observability?.RecordMutation(command, result.IsSuccess);
            if (result.IsSuccess)
            {
                Volatile.Write(ref _published, null);
                _exact.Clear();
            }
            return result;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            observability?.RecordMutation(command, success: false);
            return MutationUnavailable();
        }
    }

    private void RemoveOldestExactEntry()
    {
        var oldest = _exact.MinBy(item => item.Value.CachedAt);
        if (!string.IsNullOrEmpty(oldest.Key))
        {
            _exact.TryRemove(oldest.Key, out _);
        }
    }

    internal static ContractDiagnostic Diagnostic(
        string path,
        string code,
        string message) => new(path, code, message);

    internal static ContractAggregateResult AggregateFailure(
        string code,
        string message) => new(
        null,
        [Diagnostic("contract", code, message)]);

    internal static ContractVersionResult VersionFailure(
        string code,
        string message) => new(
        null,
        [Diagnostic("contract", code, message)]);

    internal static ContractMutationResult MutationFailure(
        string code,
        string message) => new(
        null,
        [Diagnostic("contract", code, message)]);

    private static ContractAggregateResult AggregateUnavailable() =>
        AggregateFailure(
            "ContractRegistryUnavailable",
            "The Contract Registry is unavailable.");

    private static ContractVersionResult VersionUnavailable() =>
        VersionFailure(
            "ContractRegistryUnavailable",
            "The Contract Registry is unavailable.");

    private static ContractValidationResult ValidationUnavailable() => new(
        false,
        null,
        null,
        null,
        [],
        null,
        null,
        [Diagnostic(
            "contract",
            "ContractRegistryUnavailable",
            "The Contract Registry is unavailable.")]);

    private static ContractMutationResult MutationUnavailable() =>
        MutationFailure(
            "ContractRegistryUnavailable",
            "The Contract Registry is unavailable.");

    private sealed record PublishedCacheEntry(
        IReadOnlyList<ContractVersionSummary> Versions,
        DateTimeOffset ExpiresAt);

    private sealed record ExactCacheEntry(
        ContractVersionResult Result,
        DateTimeOffset ExpiresAt,
        DateTimeOffset CachedAt);
}
