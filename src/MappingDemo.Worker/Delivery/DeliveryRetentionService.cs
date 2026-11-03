using Dapper;
using Npgsql;

namespace MappingDemo.Worker.Delivery;

public sealed class DeliveryRetentionService(
    NpgsqlDataSource dataSource,
    ILogger<DeliveryRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE delivery_jobs
            SET
                payload_ciphertext = NULL,
                payload_nonce = NULL,
                payload_tag = NULL,
                updated_at = now()
            WHERE terminal_at IS NOT NULL
              AND payload_expires_at <= now()
              AND payload_ciphertext IS NOT NULL;

            DELETE FROM delivery_attempts AS attempts
            USING delivery_jobs AS jobs
            WHERE attempts.delivery_job_id = jobs.id
              AND jobs.audit_expires_at <= now();

            DELETE FROM delivery_resend_audit AS audit
            USING delivery_jobs AS jobs
            WHERE audit.original_delivery_job_id = jobs.id
              AND jobs.audit_expires_at <= now();

            DELETE FROM delivery_jobs
            WHERE terminal_at IS NOT NULL
              AND audit_expires_at <= now();
            """;
        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "API delivery retention cleanup failed.");
        }
    }
}
