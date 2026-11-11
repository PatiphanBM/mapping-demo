using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryObservability : IDisposable
{
    private readonly Meter _meter = new("MappingDemo.DatabaseDelivery", "1.0");
    private readonly Counter<long> _deliveryResults;
    private readonly Counter<long> _schemaChecks;
    private readonly Histogram<double> _schemaInspectionMilliseconds;

    public DatabaseDeliveryObservability()
    {
        _deliveryResults = _meter.CreateCounter<long>(
            "mapping_database_delivery_results_total");
        _schemaChecks = _meter.CreateCounter<long>(
            "mapping_database_schema_checks_total");
        _schemaInspectionMilliseconds = _meter.CreateHistogram<double>(
            "mapping_database_schema_inspection_duration_ms");
    }

    public void RecordDelivery(string provider, string result) =>
        _deliveryResults.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", result));

    public void RecordSchemaCheck(
        string provider,
        string compatibility,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "compatibility", compatibility }
        };
        _schemaChecks.Add(1, tags);
        _schemaInspectionMilliseconds.Record(duration.TotalMilliseconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
