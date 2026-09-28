using MappingDemo.Api.Options;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace MappingDemo.Api.Services;

public class FileWatcherService : BackgroundService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly string _inputRootPath;
    private readonly WatcherOptions _watcherOptions;
    private readonly ActiveConfigProvider _activeConfigProvider;
    private readonly WatcherRegistry _watcherRegistry;
    private readonly FileIntake _fileIntake;
    private readonly Channel<FileDetected> _detectedFiles =
        Channel.CreateUnbounded<FileDetected>();
    private readonly Channel<WatcherConfig> _scanRequests =
        Channel.CreateUnbounded<WatcherConfig>();

    public FileWatcherService(
        ILogger<FileWatcherService> logger,
        IOptions<PathOptions> pathOptions,
        IOptions<WatcherOptions> watcherOptions,
        ActiveConfigProvider activeConfigProvider,
        WatcherRegistry watcherRegistry,
        FileIntake fileIntake,
        IHostEnvironment environment)
    {
        _logger = logger;
        _inputRootPath = Path.GetFullPath(
            pathOptions.Value.InputRoot,
            environment.ContentRootPath);
        _watcherOptions = watcherOptions.Value;
        _activeConfigProvider = activeConfigProvider;
        _watcherRegistry = watcherRegistry;
        _fileIntake = fileIntake;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "File watcher configured with input root {InputRoot}, config refresh interval {ConfigRefreshSeconds} seconds, and scan interval {ScanIntervalSeconds} seconds",
            _inputRootPath,
            _watcherOptions.ConfigRefreshSeconds,
            _watcherOptions.ScanIntervalSeconds);

        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var executionToken = executionCancellation.Token;
        var tasks = new[]
        {
            ConsumeDetectedFilesAsync(executionToken),
            RefreshWatchersAsync(executionToken),
            ScanPeriodicallyAsync(executionToken),
            ConsumeScanRequestsAsync(executionToken)
        };

        try
        {
            await Task.WhenAny(tasks);
            await executionCancellation.CancelAsync();
            await Task.WhenAll(tasks);
        }
        finally
        {
            await executionCancellation.CancelAsync();
            _watcherRegistry.Dispose();
        }
    }

    private async Task RefreshWatchersAsync(CancellationToken cancellationToken)
    {
        var configs = await ReloadWatchersAsync(cancellationToken);
        ScanWatchDirectories(configs);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_watcherOptions.ConfigRefreshSeconds));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await ReloadWatchersAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<WatcherConfig>> ReloadWatchersAsync(
        CancellationToken cancellationToken)
    {
        var configs = await _activeConfigProvider.GetAllAsync(cancellationToken);
        foreach (var config in configs)
        {
            _logger.LogInformation(
                "Loaded watcher config {ConfigId} ({ConfigName}) at {InputPath} with active version {ActiveVersionId}",
                config.ConfigId,
                config.Name,
                config.InputPath,
                config.ActiveVersionId);
        }

        _watcherRegistry.Update(
            configs,
            OnFileDetected,
            OnScanRequested);

        return configs;
    }

    private void ScanWatchDirectories(IEnumerable<WatcherConfig> configs)
    {
        foreach (var config in configs.Where(
                     config => config.ActiveVersionId.HasValue))
        {
            ScanWatchDirectory(config);
        }
    }

    private void ScanWatchDirectory(WatcherConfig config)
    {
        using var scope = _logger.BeginScope(
            "ConfigId={ConfigId} Path={Path}",
            config.ConfigId,
            config.InputPath);
        _logger.LogInformation(
            "Scanning watch directory for config {ConfigId} at {Path}",
            config.ConfigId,
            config.InputPath);

        foreach (var path in Directory.EnumerateFiles(
                     config.InputPath,
                     "*.csv",
                     SearchOption.TopDirectoryOnly))
        {
            OnFileDetected(config.ConfigId, path);
        }
    }

    private async Task ScanPeriodicallyAsync(
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_watcherOptions.ScanIntervalSeconds));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var configs = await _activeConfigProvider.GetAllAsync(
                cancellationToken);
            ScanWatchDirectories(configs);
        }
    }

    private async Task ConsumeScanRequestsAsync(
        CancellationToken cancellationToken)
    {
        await foreach (var config in
                       _scanRequests.Reader.ReadAllAsync(cancellationToken))
        {
            ScanWatchDirectory(config);
        }
    }

    private async Task ConsumeDetectedFilesAsync(
        CancellationToken cancellationToken)
    {
        await foreach (var detectedFile in
                       _detectedFiles.Reader.ReadAllAsync(cancellationToken))
        {
            using var fileScope = _logger.BeginScope(
                "ConfigId={ConfigId} Path={Path}",
                detectedFile.ConfigId,
                detectedFile.Path);
            var intake = _fileIntake.Inspect(detectedFile.Path);
            var fileJobId = await _fileIntake.CreateJobAsync(
                detectedFile.ConfigId,
                intake,
                cancellationToken);

            if (fileJobId.HasValue)
            {
                using var jobScope = _logger.BeginScope(
                    "FileJobId={FileJobId}",
                    fileJobId.Value);
                _logger.LogInformation(
                    "Created file job {FileJobId} for config {ConfigId} and file {Path} with intake key {IntakeKey}",
                    fileJobId.Value,
                    detectedFile.ConfigId,
                    intake.Path,
                    intake.IntakeKey);
            }
            else
            {
                _logger.LogInformation(
                    "No file job created for config {ConfigId} and file {Path} with intake key {IntakeKey}",
                    detectedFile.ConfigId,
                    intake.Path,
                    intake.IntakeKey);
            }
        }
    }

    private void OnFileDetected(long configId, string path)
    {
        _detectedFiles.Writer.TryWrite(new FileDetected(configId, path));
    }

    private void OnScanRequested(WatcherConfig config)
    {
        _scanRequests.Writer.TryWrite(config);
    }

    private sealed record FileDetected(long ConfigId, string Path);
}
