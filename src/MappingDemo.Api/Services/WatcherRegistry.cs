namespace MappingDemo.Api.Services;

public sealed class WatcherRegistry : IDisposable
{
    private readonly ILogger<WatcherRegistry> _logger;
    private readonly Dictionary<long, FileSystemWatcher> _watchers = [];

    public WatcherRegistry(ILogger<WatcherRegistry> logger)
    {
        _logger = logger;
    }

    public void Update(
        IReadOnlyCollection<WatcherConfig> configs,
        Action<long, string> onFileDetected,
        Action<WatcherConfig> onScanRequested)
    {
        foreach (var config in configs.Where(
                     config => !config.ActiveVersionId.HasValue))
        {
            _logger.LogWarning(
                "Watcher config {ConfigId} ({ConfigName}) has no active version",
                config.ConfigId,
                config.Name);
        }

        var activeConfigs = configs
            .Where(config => config.ActiveVersionId.HasValue)
            .ToDictionary(config => config.ConfigId);

        foreach (var configId in _watchers.Keys
                     .Where(configId => !activeConfigs.ContainsKey(configId))
                     .ToArray())
        {
            Remove(configId);
        }

        foreach (var config in activeConfigs.Values)
        {
            Add(config, onFileDetected, onScanRequested);
        }
    }

    private void Add(
        WatcherConfig config,
        Action<long, string> onFileDetected,
        Action<WatcherConfig> onScanRequested)
    {
        if (_watchers.ContainsKey(config.ConfigId))
        {
            return;
        }

        var watcher = new FileSystemWatcher(config.InputPath)
        {
            Filter = "*.csv",
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size
        };

        watcher.Created += (_, eventArgs) =>
            onFileDetected(config.ConfigId, eventArgs.FullPath);
        watcher.Renamed += (_, eventArgs) =>
        {
            if (eventArgs.FullPath.EndsWith(
                    ".csv",
                    StringComparison.OrdinalIgnoreCase))
            {
                onFileDetected(config.ConfigId, eventArgs.FullPath);
            }
        };
        watcher.Error += (_, eventArgs) =>
            OnWatcherError(config, eventArgs, onScanRequested);

        _watchers.Add(config.ConfigId, watcher);
        watcher.EnableRaisingEvents = true;

        _logger.LogInformation(
            "Watching config {ConfigId} at {InputPath}",
            config.ConfigId,
            config.InputPath);
    }

    private void Remove(long configId)
    {
        if (!_watchers.Remove(configId, out var watcher))
        {
            return;
        }

        var inputPath = watcher.Path;
        watcher.Dispose();

        _logger.LogInformation(
            "Stopped watching config {ConfigId} at {InputPath}",
            configId,
            inputPath);
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private void OnWatcherError(
        WatcherConfig config,
        ErrorEventArgs eventArgs,
        Action<WatcherConfig> onScanRequested)
    {
        using var scope = _logger.BeginScope(
            "ConfigId={ConfigId} Path={Path}",
            config.ConfigId,
            config.InputPath);

        onScanRequested(config);
        _logger.LogError(
            eventArgs.GetException(),
            "File system watcher error; requesting an immediate scan");
    }
}
