using System.Globalization;
using Dapper;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryScheduler(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    ILogger<DatabaseDeliveryScheduler> logger) : BackgroundService
{
    private const string RecoverSql = """
        UPDATE database_delivery_jobs
        SET
            status = 'UnknownOutcome',
            terminal_at = now(),
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted =
                'Lease expired without a usable unique key.',
            updated_at = now()
        WHERE status = 'Delivering'
          AND lease_expires_at < now()
          AND duplicate_policy ->> 'kind' = 'manualUnknownOutcome';

        UPDATE database_delivery_jobs
        SET
            status = 'RetryableFailed',
            next_attempt_at = now(),
            dispatch_scheduled_at = NULL,
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted =
                'Recovered stale lease for unique-key reconciliation.',
            updated_at = now()
        WHERE status = 'Delivering'
          AND lease_expires_at < now()
          AND duplicate_policy ->> 'kind' = 'existingUniqueKey';
        """;

    private const string IsPausedSql = """
        SELECT database_delivery_paused
        FROM delivery_runtime_control
        WHERE singleton = true;
        """;

    private const string SelectReadySql = """
        SELECT id
        FROM database_delivery_jobs
        WHERE status IN ('Pending', 'RetryableFailed')
          AND (next_attempt_at IS NULL OR next_attempt_at <= now())
          AND dispatch_scheduled_at IS NULL
        ORDER BY next_attempt_at NULLS FIRST, id
        LIMIT 100
        FOR UPDATE SKIP LOCKED;
        """;

    private const string MarkScheduledSql = """
        UPDATE database_delivery_jobs
        SET dispatch_scheduled_at = now(), updated_at = now()
        WHERE id = @JobId
          AND dispatch_scheduled_at IS NULL;
        """;

    private readonly bool _enabled = configuration.GetValue(
        "Features:DatabaseTableDelivery",
        false);

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
                    "Failed to schedule database delivery jobs.");
            }
        }
    }

    internal async Task<int> ScheduleReadyAsync(
        CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return 0;
        }

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                IsPausedSql,
                cancellationToken: cancellationToken)))
        {
            return 0;
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            RecoverSql,
            transaction: transaction,
            cancellationToken: cancellationToken));
        var ids = (await connection.QueryAsync<long>(new CommandDefinition(
                SelectReadySql,
                transaction: transaction,
                cancellationToken: cancellationToken)))
            .ToArray();
        foreach (var id in ids)
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition(
                MarkScheduledSql,
                new { JobId = id },
                transaction,
                cancellationToken: cancellationToken));
            if (changed != 1)
            {
                continue;
            }

            await OutboxWriter.AddOnceAsync(
                connection,
                transaction,
                Topics.DatabaseDelivery,
                id.ToString(CultureInfo.InvariantCulture),
                new DatabaseDeliveryRequested(id),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ids.Length;
    }
}
