namespace MappingDemo.Api.Options;

public sealed class WatcherOptions
{
    public const string SectionName = "Watcher";

    public int ConfigRefreshSeconds { get; init; }

    public int ScanIntervalSeconds { get; init; }
}
