using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MappingDemo.Shared.Tables;

public sealed class TableAdministrationObservability
{
    public const string MeterName = "MappingDemo.TableAdministration";
    public const int RepeatedVerificationFailureThreshold = 3;

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> MutationCounter =
        Meter.CreateCounter<long>("table_admin.mutation");
    private static readonly Counter<long> VerificationCounter =
        Meter.CreateCounter<long>("table_admin.verification");
    private static readonly Counter<long> RateLimitCounter =
        Meter.CreateCounter<long>("table_admin.verification_rate_limited");
    private static readonly Counter<long> UnauthorizedMutationCounter =
        Meter.CreateCounter<long>("table_admin.unauthorized_mutation");
    private static readonly Histogram<double> VerificationDuration =
        Meter.CreateHistogram<double>(
            "table_admin.verification_duration",
            "ms");

    private readonly ConcurrentDictionary<string, long> _mutations = [];
    private readonly ConcurrentDictionary<string, long> _verifications = [];
    private long _consecutiveVerificationFailures;
    private long _rateLimited;
    private long _unauthorizedMutations;

    public void RecordMutation(string action, bool success)
    {
        var safeAction = action is "create" or "add_column"
            ? action
            : "unknown";
        var result = success ? "success" : "failure";
        Increment(_mutations, $"{safeAction}:{result}");
        MutationCounter.Add(
            1,
            new("action", safeAction),
            new("result", result));
    }

    public void RecordVerification(TableSchemaVerificationResult result)
    {
        var status = result.Status.ToString();
        Increment(_verifications, status);
        VerificationCounter.Add(
            1,
            new KeyValuePair<string, object?>("status", status));
        VerificationDuration.Record(result.DurationMilliseconds);

        if (result.Status == TableVerificationStatus.VerificationFailed)
        {
            Interlocked.Increment(ref _consecutiveVerificationFailures);
        }
        else
        {
            Interlocked.Exchange(ref _consecutiveVerificationFailures, 0);
        }
    }

    public void RecordRateLimited()
    {
        Interlocked.Increment(ref _rateLimited);
        RateLimitCounter.Add(1);
    }

    public void RecordUnauthorizedMutation()
    {
        Interlocked.Increment(ref _unauthorizedMutations);
        UnauthorizedMutationCounter.Add(1);
    }

    public TableAdministrationOperationsSnapshot Snapshot()
    {
        var consecutive = Interlocked.Read(
            ref _consecutiveVerificationFailures);
        var rateLimited = Interlocked.Read(ref _rateLimited);
        var unauthorized = Interlocked.Read(ref _unauthorizedMutations);
        var alerts = new List<TableAdministrationAlert>();
        if (consecutive >= RepeatedVerificationFailureThreshold)
        {
            alerts.Add(new(
                "Warning",
                "RepeatedTableVerificationFailure",
                "PostgreSQL catalog verification failed repeatedly; keep table mutations paused and inspect catalog access."));
        }
        if (_verifications.Any(item =>
            item.Key is "Drifted" or "Missing" && item.Value > 0))
        {
            alerts.Add(new(
                "Critical",
                "TableSchemaDriftDetected",
                "A managed physical table is missing or differs from authoritative metadata; follow the drift runbook without automatic repair."));
        }
        if (unauthorized > 0)
        {
            alerts.Add(new(
                "Warning",
                "UnauthorizedTableMutationAttempt",
                "An unauthorized table mutation was attempted; review identity and audit records."));
        }

        return new(
            Metrics(_mutations),
            Metrics(_verifications),
            consecutive,
            rateLimited,
            unauthorized,
            alerts);
    }

    private static void Increment(
        ConcurrentDictionary<string, long> values,
        string key) =>
        values.AddOrUpdate(key, 1, static (_, count) => count + 1);

    private static IReadOnlyList<TableAdministrationMetric> Metrics(
        ConcurrentDictionary<string, long> values) =>
        values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new TableAdministrationMetric(
                item.Key,
                item.Value))
            .ToArray();
}

public sealed record TableAdministrationOperationsSnapshot(
    IReadOnlyList<TableAdministrationMetric> Mutations,
    IReadOnlyList<TableAdministrationMetric> Verifications,
    long ConsecutiveVerificationFailures,
    long RateLimitedCount,
    long UnauthorizedMutationCount,
    IReadOnlyList<TableAdministrationAlert> Alerts);

public sealed record TableAdministrationMetric(string Name, long Count);

public sealed record TableAdministrationAlert(
    string Severity,
    string Code,
    string Message);
