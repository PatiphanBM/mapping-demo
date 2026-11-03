using Dapper;
using Npgsql;

namespace MappingDemo.Worker.Delivery;

public sealed class DeliveryControlReader(NpgsqlDataSource dataSource)
{
    public async Task<bool> IsPausedAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT delivery_paused
            FROM delivery_runtime_control
            WHERE singleton = true;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));
    }

    public async Task ReleaseScheduledJobAsync(
        long deliveryJobId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE delivery_jobs
            SET dispatch_scheduled_at = NULL, updated_at = now()
            WHERE id = @DeliveryJobId
              AND status IN ('Pending', 'RetryableFailed');
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { DeliveryJobId = deliveryJobId },
            cancellationToken: cancellationToken));
    }
}
