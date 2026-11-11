using Dapper;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryControlReader(NpgsqlDataSource dataSource)
{
    public async Task<bool> IsPausedAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT database_delivery_paused
            FROM delivery_runtime_control
            WHERE singleton = true;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));
    }
}
