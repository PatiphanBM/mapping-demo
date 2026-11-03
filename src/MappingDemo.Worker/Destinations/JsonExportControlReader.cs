using Dapper;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportControlReader(NpgsqlDataSource dataSource)
{
    public async Task<bool> IsFinalizerPausedAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT json_file_finalizer_paused
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
