using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MappingDemo.Shared.Contracts;

public sealed class ContractRegistryObservability
{
    public const string MeterName = "MappingDemo.ContractRegistry";
    public const int RepeatedValidationFailureThreshold = 3;

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> ValidationCounter =
        Meter.CreateCounter<long>("contract_registry.validation");
    private static readonly Counter<long> MutationCounter =
        Meter.CreateCounter<long>("contract_registry.mutation");
    private static readonly Counter<long> ResolutionFailureCounter =
        Meter.CreateCounter<long>("contract_registry.resolution_failure");
    private static readonly Counter<long> CacheCounter =
        Meter.CreateCounter<long>("contract_registry.cache_access");
    private static readonly Counter<long> UnauthorizedPublishCounter =
        Meter.CreateCounter<long>("contract_registry.unauthorized_publish");
    private static readonly Histogram<double> ResolveLatency =
        Meter.CreateHistogram<double>(
            "contract_registry.resolve_duration",
            "ms");

    private readonly ConcurrentDictionary<string, long> _validation = [];
    private readonly ConcurrentDictionary<string, long> _mutations = [];
    private readonly ConcurrentDictionary<string, long> _failures = [];
    private readonly ConcurrentDictionary<string, long> _cache = [];
    private long _consecutiveValidationFailures;
    private long _unauthorizedPublishCount;

    public void RecordValidation(bool success)
    {
        var result = success ? "success" : "failure";
        Increment(_validation, result);
        ValidationCounter.Add(
            1,
            new KeyValuePair<string, object?>("result", result));
        if (success)
        {
            Interlocked.Exchange(ref _consecutiveValidationFailures, 0);
        }
        else
        {
            Interlocked.Increment(ref _consecutiveValidationFailures);
        }
    }

    public void RecordMutation(ContractRegistryCommand command, bool success)
    {
        var action = command switch
        {
            CreateContractDraftCommand => "create",
            UpdateContractDraftCommand => "update",
            CloneContractVersionCommand => "clone",
            PublishContractVersionCommand => "publish",
            RetireContractVersionCommand => "retire",
            _ => "unknown"
        };
        var result = success ? "success" : "failure";
        Increment(_mutations, $"{action}:{result}");
        MutationCounter.Add(
            1,
            new("action", action),
            new("result", result));
    }

    public void RecordResolutionFailure(string diagnosticCode)
    {
        var failure = diagnosticCode switch
        {
            "ContractRegistryUnavailable" => "registry_unavailable",
            "ContractRegistryCorrupt" => "hash_drift",
            "ContractNotFound" => "not_found",
            "ContractRetired" => "retired",
            _ => "other"
        };
        Increment(_failures, failure);
        ResolutionFailureCounter.Add(
            1,
            new KeyValuePair<string, object?>("failure", failure));
    }

    public void RecordCacheAccess(string cache, bool hit)
    {
        var result = hit ? "hit" : "miss";
        Increment(_cache, $"{cache}:{result}");
        CacheCounter.Add(1, new("cache", cache), new("result", result));
    }

    public void RecordResolveLatency(TimeSpan duration) =>
        ResolveLatency.Record(duration.TotalMilliseconds);

    public void RecordUnauthorizedPublish()
    {
        Interlocked.Increment(ref _unauthorizedPublishCount);
        UnauthorizedPublishCounter.Add(1);
    }

    public ContractRegistryOperationsSnapshot Snapshot()
    {
        var failures = Metrics(_failures);
        var consecutive = Interlocked.Read(
            ref _consecutiveValidationFailures);
        var unauthorized = Interlocked.Read(ref _unauthorizedPublishCount);
        var alerts = new List<ContractRegistryAlert>();
        if (failures.Any(item =>
            item.Name is "registry_unavailable" or "hash_drift" &&
            item.Count > 0))
        {
            alerts.Add(new(
                "Critical",
                "ContractRegistryIntegrityOrAvailability",
                "Contract Registry resolution is unavailable or failed hash verification; keep runtime fail-closed."));
        }
        if (consecutive >= RepeatedValidationFailureThreshold)
        {
            alerts.Add(new(
                "Warning",
                "RepeatedContractValidationFailure",
                "Contract validation failed repeatedly; pause publication and review safe diagnostics."));
        }
        if (unauthorized > 0)
        {
            alerts.Add(new(
                "Warning",
                "UnauthorizedContractPublishAttempt",
                "Unauthorized Contract publication was attempted; review identity and audit logs."));
        }
        return new ContractRegistryOperationsSnapshot(
            Metrics(_validation),
            Metrics(_mutations),
            failures,
            Metrics(_cache),
            consecutive,
            unauthorized,
            alerts);
    }

    private static void Increment(
        ConcurrentDictionary<string, long> values,
        string key) =>
        values.AddOrUpdate(key, 1, static (_, count) => count + 1);

    private static IReadOnlyList<ContractRegistryMetric> Metrics(
        ConcurrentDictionary<string, long> values) =>
        values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ContractRegistryMetric(item.Key, item.Value))
            .ToArray();
}

public sealed record ContractRegistryOperationsSnapshot(
    IReadOnlyList<ContractRegistryMetric> Validation,
    IReadOnlyList<ContractRegistryMetric> Mutations,
    IReadOnlyList<ContractRegistryMetric> ResolutionFailures,
    IReadOnlyList<ContractRegistryMetric> Cache,
    long ConsecutiveValidationFailures,
    long UnauthorizedPublishCount,
    IReadOnlyList<ContractRegistryAlert> Alerts);

public sealed record ContractRegistryMetric(string Name, long Count);

public sealed record ContractRegistryAlert(
    string Severity,
    string Code,
    string Message);
