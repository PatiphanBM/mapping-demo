using Dapper;
using MappingDemo.Api.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class ActiveConfigProvider
{
    private const string SelectConfigsSql = """
        SELECT
            id AS "ConfigId",
            name AS "Name",
            input_folder AS "InputFolder",
            active_version_id AS "ActiveVersionId"
        FROM mapping_configs
        ORDER BY id;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _inputRootPath;

    public ActiveConfigProvider(
        NpgsqlDataSource dataSource,
        IOptions<PathOptions> pathOptions,
        IHostEnvironment environment)
    {
        _dataSource = dataSource;
        _inputRootPath = Path.GetFullPath(
            pathOptions.Value.InputRoot,
            environment.ContentRootPath);
    }

    public async Task<IReadOnlyList<WatcherConfig>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectConfigsSql,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ConfigRow>(command);

        return rows
            .Select(row => new WatcherConfig(
                row.ConfigId,
                row.Name,
                Path.GetFullPath(row.InputFolder, _inputRootPath),
                row.ActiveVersionId))
            .ToArray();
    }

    private sealed class ConfigRow
    {
        public long ConfigId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string InputFolder { get; init; } = string.Empty;

        public long? ActiveVersionId { get; init; }
    }
}

public sealed record WatcherConfig(
    long ConfigId,
    string Name,
    string InputPath,
    long? ActiveVersionId);
