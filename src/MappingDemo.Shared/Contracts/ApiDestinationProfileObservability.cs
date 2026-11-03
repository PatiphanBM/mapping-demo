using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MappingDemo.Shared.Contracts;

public enum ApiDestinationProfileCatalogOperation
{
    SafeList,
    ResolveActive,
    ResolvePinned
}

public enum ApiDestinationProfileCacheKind
{
    SafeList,
    Pinned
}

public enum ApiDestinationProfileMutationKind
{
    Activate,
    Disable,
    Rollback
}

public sealed class ApiDestinationProfileObservability
{
    public const string MeterName = "MappingDemo.ApiDestinationProfiles";
    public const int RepeatedValidationFailureThreshold = 3;

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> ValidationCounter =
        Meter.CreateCounter<long>("api_destination_profile.validation");
    private static readonly Counter<long> MutationCounter =
        Meter.CreateCounter<long>("api_destination_profile.mutation");
    private static readonly Counter<long> ResolutionFailureCounter =
        Meter.CreateCounter<long>("api_destination_profile.resolution_failure");
    private static readonly Counter<long> CacheCounter =
        Meter.CreateCounter<long>("api_destination_profile.cache_access");
    private static readonly Counter<long> UnauthorizedActivationCounter =
        Meter.CreateCounter<long>(
            "api_destination_profile.unauthorized_activation");
    private static readonly Histogram<double> ResolutionLatency =
        Meter.CreateHistogram<double>(
            "api_destination_profile.resolve_duration",
            "ms");

    private readonly ConcurrentDictionary<string, long> _validation = [];
    private readonly ConcurrentDictionary<string, long> _mutations = [];
    private readonly ConcurrentDictionary<string, long> _failures = [];
    private readonly ConcurrentDictionary<string, long> _cache = [];
    private readonly ConcurrentDictionary<string, LatencyAccumulator> _latency = [];
    private long _consecutiveValidationFailures;
    private long _unauthorizedActivationCount;

    public void RecordValidation(string environment, bool success)
    {
        var safeEnvironment = NormalizeEnvironment(environment);
        var result = success ? "success" : "failure";
        _validation.AddOrUpdate(
            $"{safeEnvironment}:{result}",
            1,
            static (_, count) => count + 1);
        ValidationCounter.Add(
            1,
            new("environment", safeEnvironment),
            new("result", result));
        if (success)
        {
            Interlocked.Exchange(ref _consecutiveValidationFailures, 0);
        }
        else
        {
            Interlocked.Increment(ref _consecutiveValidationFailures);
        }
    }

    public void RecordMutation(ApiDestinationProfileMutationKind mutation)
    {
        var action = Name(mutation);
        _mutations.AddOrUpdate(action, 1, static (_, count) => count + 1);
        MutationCounter.Add(
            1,
            new KeyValuePair<string, object?>("action", action));
    }

    public void RecordResolutionFailure(string diagnosticCode)
    {
        var failure = diagnosticCode switch
        {
            "UnknownPinnedProfileVersion" => "pinned_version_missing",
            "ProfileCatalogUnavailable" => "catalog_unavailable",
            "ProfileCatalogParityMismatch" => "source_mismatch",
            _ => "other"
        };
        _failures.AddOrUpdate(failure, 1, static (_, count) => count + 1);
        ResolutionFailureCounter.Add(
            1,
            new KeyValuePair<string, object?>("failure", failure));
    }

    public void RecordCacheAccess(
        ApiDestinationProfileCacheKind cache,
        bool hit)
    {
        var cacheName = Name(cache);
        var result = hit ? "hit" : "miss";
        _cache.AddOrUpdate(
            $"{cacheName}:{result}",
            1,
            static (_, count) => count + 1);
        CacheCounter.Add(
            1,
            new("cache", cacheName),
            new("result", result));
    }

    public void RecordResolutionLatency(
        ApiDestinationProfileCatalogOperation operation,
        TimeSpan duration)
    {
        var operationName = Name(operation);
        _latency.GetOrAdd(
                operationName,
                static _ => new LatencyAccumulator())
            .Record(duration);
        ResolutionLatency.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("operation", operationName));
    }

    public void RecordUnauthorizedActivation()
    {
        Interlocked.Increment(ref _unauthorizedActivationCount);
        UnauthorizedActivationCounter.Add(1);
    }

    public ApiDestinationProfileOperationsSnapshot Snapshot()
    {
        var validation = _validation
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                var parts = item.Key.Split(':');
                return new ApiDestinationProfileValidationMetric(
                    parts[0],
                    parts[1],
                    item.Value);
            })
            .ToArray();
        var mutations = _mutations
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ApiDestinationProfileMutationMetric(
                item.Key,
                item.Value))
            .ToArray();
        var failures = _failures
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ApiDestinationProfileFailureMetric(
                item.Key,
                item.Value))
            .ToArray();
        var cache = _cache
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                var parts = item.Key.Split(':');
                return new ApiDestinationProfileCacheMetric(
                    parts[0],
                    parts[1],
                    item.Value);
            })
            .ToArray();
        var latency = _latency
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Value.Snapshot(item.Key))
            .ToArray();
        var consecutiveFailures = Interlocked.Read(
            ref _consecutiveValidationFailures);
        var unauthorized = Interlocked.Read(
            ref _unauthorizedActivationCount);
        return new ApiDestinationProfileOperationsSnapshot(
            validation,
            mutations,
            failures,
            cache,
            latency,
            consecutiveFailures,
            unauthorized,
            BuildAlerts(failures, consecutiveFailures, unauthorized));
    }

    private static IReadOnlyList<ApiDestinationProfileAlert> BuildAlerts(
        IReadOnlyList<ApiDestinationProfileFailureMetric> failures,
        long consecutiveValidationFailures,
        long unauthorizedActivationCount)
    {
        var counts = failures.ToDictionary(
            metric => metric.Failure,
            metric => metric.Count,
            StringComparer.Ordinal);
        var alerts = new List<ApiDestinationProfileAlert>();
        AddFailureAlert(
            alerts,
            counts,
            "pinned_version_missing",
            "Critical",
            "PinnedVersionMissing",
            "Pinned Profile Version resolution failed; pause affected delivery work.");
        AddFailureAlert(
            alerts,
            counts,
            "catalog_unavailable",
            "Critical",
            "ProfileCatalogUnavailable",
            "PostgreSQL profile catalog is unavailable; keep runtime fail-closed.");
        AddFailureAlert(
            alerts,
            counts,
            "source_mismatch",
            "Critical",
            "ProfileCatalogSourceMismatch",
            "Configuration and PostgreSQL profile sources differ; block cutover and mutation.");
        if (unauthorizedActivationCount > 0)
        {
            alerts.Add(new(
                "Warning",
                "UnauthorizedActivationAttempt",
                "Unauthorized profile activation was attempted; review identity and audit logs."));
        }
        if (consecutiveValidationFailures >=
            RepeatedValidationFailureThreshold)
        {
            alerts.Add(new(
                "Warning",
                "RepeatedValidationFailure",
                "Profile validation failed repeatedly; stop activation and review diagnostics."));
        }
        return alerts;
    }

    private static void AddFailureAlert(
        ICollection<ApiDestinationProfileAlert> alerts,
        IReadOnlyDictionary<string, long> counts,
        string failure,
        string severity,
        string code,
        string message)
    {
        if (counts.GetValueOrDefault(failure) > 0)
        {
            alerts.Add(new(severity, code, message));
        }
    }

    private static string NormalizeEnvironment(string environment) =>
        environment is "Stub" or "Sandbox" or "Production"
            ? environment
            : "Unknown";

    private static string Name<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private sealed class LatencyAccumulator
    {
        private long _count;
        private long _totalTicks;
        private long _maximumTicks;

        public void Record(TimeSpan duration)
        {
            var ticks = Math.Max(0, duration.Ticks);
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalTicks, ticks);
            var current = Interlocked.Read(ref _maximumTicks);
            while (ticks > current)
            {
                var observed = Interlocked.CompareExchange(
                    ref _maximumTicks,
                    ticks,
                    current);
                if (observed == current)
                {
                    break;
                }
                current = observed;
            }
        }

        public ApiDestinationProfileLatencyMetric Snapshot(string operation)
        {
            var count = Interlocked.Read(ref _count);
            var totalTicks = Interlocked.Read(ref _totalTicks);
            var maximumTicks = Interlocked.Read(ref _maximumTicks);
            return new(
                operation,
                count,
                count == 0
                    ? 0
                    : TimeSpan.FromTicks(totalTicks).TotalMilliseconds / count,
                TimeSpan.FromTicks(maximumTicks).TotalMilliseconds);
        }
    }
}

public sealed record ApiDestinationProfileOperationsSnapshot(
    IReadOnlyList<ApiDestinationProfileValidationMetric> Validation,
    IReadOnlyList<ApiDestinationProfileMutationMetric> Mutations,
    IReadOnlyList<ApiDestinationProfileFailureMetric> ResolutionFailures,
    IReadOnlyList<ApiDestinationProfileCacheMetric> Cache,
    IReadOnlyList<ApiDestinationProfileLatencyMetric> ResolveLatency,
    long ConsecutiveValidationFailures,
    long UnauthorizedActivationCount,
    IReadOnlyList<ApiDestinationProfileAlert> Alerts);

public sealed record ApiDestinationProfileValidationMetric(
    string Environment,
    string Result,
    long Count);

public sealed record ApiDestinationProfileMutationMetric(
    string Action,
    long Count);

public sealed record ApiDestinationProfileFailureMetric(
    string Failure,
    long Count);

public sealed record ApiDestinationProfileCacheMetric(
    string Cache,
    string Result,
    long Count);

public sealed record ApiDestinationProfileLatencyMetric(
    string Operation,
    long Count,
    double AverageMilliseconds,
    double MaximumMilliseconds);

public sealed record ApiDestinationProfileAlert(
    string Severity,
    string Code,
    string Message);
