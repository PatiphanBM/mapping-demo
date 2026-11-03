using System.Globalization;
using Dapper;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Worker.Delivery;

public sealed class DeliveryRetryScheduler(
    NpgsqlDataSource dataSource,
    DeliveryControlReader control,
    ILogger<DeliveryRetryScheduler> logger) : BackgroundService
{
    private const string RecoverStaleSql = """
        UPDATE delivery_jobs
        SET
            status = @RetryableStatus,
            next_attempt_at = now(),
            dispatch_scheduled_at = NULL,
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted = 'Recovered stale delivery lease.',
            updated_at = now()
        WHERE status = @DeliveringStatus
          AND lease_expires_at < now()
          AND attempts < @MaximumAttempts;

        UPDATE delivery_jobs
        SET
            status = @PermanentStatus,
            terminal_at = now(),
            payload_expires_at = now() + interval '30 days',
            audit_expires_at = now() + interval '365 days',
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted = 'Delivery lease expired after maximum attempts.',
            updated_at = now()
        WHERE status = @DeliveringStatus
          AND lease_expires_at < now()
          AND attempts >= @MaximumAttempts;
        """;

    private const string SelectReadySql = """
        SELECT id
        FROM delivery_jobs
        WHERE status IN (@PendingStatus, @RetryableStatus)
          AND attempts < @MaximumAttempts
          AND (next_attempt_at IS NULL OR next_attempt_at <= now())
          AND dispatch_scheduled_at IS NULL
        ORDER BY next_attempt_at NULLS FIRST, id
        LIMIT 100
        FOR UPDATE SKIP LOCKED;
        """;

    private const string MarkScheduledSql = """
        UPDATE delivery_jobs
        SET dispatch_scheduled_at = now(), updated_at = now()
        WHERE id = @DeliveryJobId
          AND dispatch_scheduled_at IS NULL;
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ScheduleReadyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to schedule ready API delivery jobs.");
            }
        }
    }

    internal async Task<int> ScheduleReadyAsync(
        CancellationToken cancellationToken)
    {
        if (await control.IsPausedAsync(cancellationToken))
        {
            return 0;
        }

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            PendingStatus = DeliveryJobStatus.Pending,
            RetryableStatus = DeliveryJobStatus.RetryableFailed,
            DeliveringStatus = DeliveryJobStatus.Delivering,
            PermanentStatus = DeliveryJobStatus.PermanentFailed,
            MaximumAttempts = DeliveryRetryPolicy.MaximumAttempts
        };
        await connection.ExecuteAsync(new CommandDefinition(
            RecoverStaleSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));
        var jobIds = (await connection.QueryAsync<long>(new CommandDefinition(
                SelectReadySql,
                parameters,
                transaction,
                cancellationToken: cancellationToken)))
            .ToArray();
        foreach (var deliveryJobId in jobIds)
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                MarkScheduledSql,
                new { DeliveryJobId = deliveryJobId },
                transaction,
                cancellationToken: cancellationToken));
            if (updated != 1)
            {
                continue;
            }

            await OutboxWriter.AddAsync(
                connection,
                transaction,
                Topics.ApiDelivery,
                deliveryJobId.ToString(CultureInfo.InvariantCulture),
                new DeliveryRequested(deliveryJobId),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return jobIds.Length;
    }
}
