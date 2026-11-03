using Dapper;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportRecoveryScheduler(
    NpgsqlDataSource dataSource,
    JsonFileFinalizer finalizer,
    ILogger<JsonExportRecoveryScheduler> logger,
    JsonExportControlReader? control = null) : BackgroundService
{
    private const string SelectRecoverableJobsSql = """
        SELECT id
        FROM json_export_jobs
        WHERE status IN ('Pending', 'RetryableFailed')
           OR (
               status = 'Finalizing'
               AND (
                   lease_expires_at IS NULL
                   OR lease_expires_at < now()))
        ORDER BY updated_at, id
        LIMIT 100;
        """;

    public async Task<int> RecoverOnceAsync(
        CancellationToken cancellationToken = default)
    {
        if (control is not null &&
            await control.IsFinalizerPausedAsync(cancellationToken))
        {
            return 0;
        }

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var jobIds = (await connection.QueryAsync<long>(new CommandDefinition(
            SelectRecoverableJobsSql,
            cancellationToken: cancellationToken))).ToArray();

        foreach (var jobId in jobIds)
        {
            await finalizer.HandleAsync(jobId, cancellationToken);
        }

        return jobIds.Length;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                await RecoverOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "JSON export recovery scan failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
