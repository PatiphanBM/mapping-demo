using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Shared.MappingConfigs;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class MappingConfigService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string SelectMappingConfigByIdSql = """
        SELECT
            id AS "Id",
            name AS "Name",
            input_folder AS "InputFolder",
            source_table_id AS "SourceTableId",
            normalized_table_id AS "NormalizedTableId",
            active_version_id AS "ActiveVersionId"
        FROM mapping_configs
        WHERE id = @Id;
        """;

    private const string SelectMappingConfigsWithVersionsSql = """
        SELECT
            mapping_configs.id AS "ConfigId",
            mapping_configs.name AS "Name",
            mapping_configs.input_folder AS "InputFolder",
            mapping_configs.source_table_id AS "SourceTableId",
            mapping_configs.normalized_table_id AS "NormalizedTableId",
            mapping_configs.active_version_id AS "ActiveVersionId",
            mapping_config_versions.id AS "VersionId",
            mapping_config_versions.version_no AS "VersionNo",
            mapping_config_versions.file_to_source::text
                AS "FileToSourceJson",
            mapping_config_versions.source_to_normalized::text
                AS "SourceToNormalizedJson"
        FROM mapping_configs
        LEFT JOIN mapping_config_versions
            ON mapping_config_versions.config_id = mapping_configs.id
        ORDER BY mapping_configs.id, mapping_config_versions.version_no;
        """;

    private const string SelectMappingConfigWithVersionsByIdSql = """
        SELECT
            mapping_configs.id AS "ConfigId",
            mapping_configs.name AS "Name",
            mapping_configs.input_folder AS "InputFolder",
            mapping_configs.source_table_id AS "SourceTableId",
            mapping_configs.normalized_table_id AS "NormalizedTableId",
            mapping_configs.active_version_id AS "ActiveVersionId",
            mapping_config_versions.id AS "VersionId",
            mapping_config_versions.version_no AS "VersionNo",
            mapping_config_versions.file_to_source::text
                AS "FileToSourceJson",
            mapping_config_versions.source_to_normalized::text
                AS "SourceToNormalizedJson"
        FROM mapping_configs
        LEFT JOIN mapping_config_versions
            ON mapping_config_versions.config_id = mapping_configs.id
        WHERE mapping_configs.id = @Id
        ORDER BY mapping_config_versions.version_no;
        """;

    private const string InsertMappingConfigSql = """
        INSERT INTO mapping_configs (
            name,
            input_folder,
            source_table_id,
            normalized_table_id)
        VALUES (
            @Name,
            @InputFolder,
            @SourceTableId,
            @NormalizedTableId)
        RETURNING id;
        """;

    private const string SelectNextVersionNumberSql = """
        SELECT COALESCE(MAX(version_no), 0) + 1
        FROM mapping_config_versions
        WHERE config_id = @ConfigId;
        """;

    private const string InsertMappingConfigVersionSql = """
        INSERT INTO mapping_config_versions (
            config_id,
            version_no,
            file_to_source,
            source_to_normalized)
        VALUES (
            @ConfigId,
            @VersionNo,
            CAST(@FileToSourceJson AS jsonb),
            CAST(@SourceToNormalizedJson AS jsonb))
        RETURNING id;
        """;

    private const string SelectMappingConfigVersionSql = """
        SELECT
            id AS "Id",
            config_id AS "ConfigId",
            version_no AS "VersionNo",
            file_to_source::text AS "FileToSourceJson",
            source_to_normalized::text AS "SourceToNormalizedJson"
        FROM mapping_config_versions
        WHERE config_id = @ConfigId
          AND version_no = @VersionNo;
        """;

    private const string ActivateMappingConfigVersionSql = """
        UPDATE mapping_config_versions
        SET activated_at = now()
        WHERE id = @VersionId
          AND config_id = @ConfigId;
        """;

    private const string SetActiveMappingConfigVersionSql = """
        UPDATE mapping_configs
        SET active_version_id = @VersionId
        WHERE id = @ConfigId;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _inputRootPath;

    public MappingConfigService(
        NpgsqlDataSource dataSource,
        IOptions<PathOptions> pathOptions,
        IHostEnvironment environment)
    {
        _dataSource = dataSource;
        _inputRootPath = Path.GetFullPath(
            pathOptions.Value.InputRoot,
            environment.ContentRootPath);
    }

    public async Task<long> CreateAsync(
        CreateMappingConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var command = new CommandDefinition(
            InsertMappingConfigSql,
            request,
            transaction,
            cancellationToken: cancellationToken);
        var configId = await connection.ExecuteScalarAsync<long>(command);

        var inputFolderPath = Path.Combine(
            _inputRootPath,
            request.InputFolder);
        Directory.CreateDirectory(inputFolderPath);

        await transaction.CommitAsync(cancellationToken);

        return configId;
    }

    public async Task<MappingConfigResponse?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigByIdSql,
            new { Id = id },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<MappingConfigResponse>(command);
    }

    public async Task<IReadOnlyList<MappingConfigDetailsResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigsWithVersionsSql,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<MappingConfigDetailsRow>(
            command);

        return MapConfigDetails(rows);
    }

    public async Task<MappingConfigDetailsResponse?> GetDetailsByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigWithVersionsByIdSql,
            new { Id = id },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<MappingConfigDetailsRow>(
            command);

        return MapConfigDetails(rows).SingleOrDefault();
    }

    public async Task<MappingConfigVersionResponse> CreateVersionAsync(
        long configId,
        CreateMappingConfigVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var nextVersionCommand = new CommandDefinition(
            SelectNextVersionNumberSql,
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken);
        var versionNo = await connection.ExecuteScalarAsync<int>(
            nextVersionCommand);
        var parameters = new
        {
            ConfigId = configId,
            VersionNo = versionNo,
            FileToSourceJson = JsonSerializer.Serialize(
                request.FileToSource,
                JsonOptions),
            SourceToNormalizedJson = JsonSerializer.Serialize(
                request.SourceToNormalized,
                JsonOptions)
        };
        var insertCommand = new CommandDefinition(
            InsertMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        var versionId = await connection.ExecuteScalarAsync<long>(
            insertCommand);

        await transaction.CommitAsync(cancellationToken);

        return new MappingConfigVersionResponse(
            versionId,
            configId,
            versionNo,
            request.FileToSource,
            request.SourceToNormalized);
    }

    public async Task<MappingConfigVersionResponse?> GetVersionAsync(
        long configId,
        int versionNo,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigVersionSql,
            new
            {
                ConfigId = configId,
                VersionNo = versionNo
            },
            cancellationToken: cancellationToken);
        var row = await connection
            .QuerySingleOrDefaultAsync<MappingConfigVersionRow>(command);

        if (row is null)
        {
            return null;
        }

        var fileToSource = JsonSerializer.Deserialize<FileToSourceRule[]>(
            row.FileToSourceJson,
            JsonOptions) ?? [];
        var sourceToNormalized =
            JsonSerializer.Deserialize<SourceToNormalizedRule[]>(
                row.SourceToNormalizedJson,
                JsonOptions) ?? [];

        return new MappingConfigVersionResponse(
            row.Id,
            row.ConfigId,
            row.VersionNo,
            fileToSource,
            sourceToNormalized);
    }

    public async Task ActivateVersionAsync(
        long configId,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            ConfigId = configId,
            VersionId = versionId
        };
        var activateVersionCommand = new CommandDefinition(
            ActivateMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(activateVersionCommand);

        var setActiveVersionCommand = new CommandDefinition(
            SetActiveMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(setActiveVersionCommand);

        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<MappingConfigDetailsResponse>
        MapConfigDetails(IEnumerable<MappingConfigDetailsRow> rows)
    {
        return rows
            .GroupBy(row => row.ConfigId)
            .Select(group =>
            {
                var config = group.First();
                var versions = group
                    .Where(row => row.VersionId.HasValue)
                    .OrderBy(row => row.VersionNo)
                    .Select(row => new MappingConfigVersionDetailsResponse(
                        row.VersionId!.Value,
                        row.VersionNo!.Value,
                        row.VersionId == config.ActiveVersionId,
                        JsonSerializer.Deserialize<FileToSourceRule[]>(
                            row.FileToSourceJson!,
                            JsonOptions) ?? [],
                        JsonSerializer.Deserialize<SourceToNormalizedRule[]>(
                            row.SourceToNormalizedJson!,
                            JsonOptions) ?? []))
                    .ToArray();

                return new MappingConfigDetailsResponse(
                    config.ConfigId,
                    config.Name,
                    config.InputFolder,
                    config.SourceTableId,
                    config.NormalizedTableId,
                    config.ActiveVersionId,
                    versions);
            })
            .ToArray();
    }

    private sealed class MappingConfigDetailsRow
    {
        public long ConfigId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string InputFolder { get; init; } = string.Empty;

        public long SourceTableId { get; init; }

        public long NormalizedTableId { get; init; }

        public long? ActiveVersionId { get; init; }

        public long? VersionId { get; init; }

        public int? VersionNo { get; init; }

        public string? FileToSourceJson { get; init; }

        public string? SourceToNormalizedJson { get; init; }
    }

    private sealed class MappingConfigVersionRow
    {
        public long Id { get; init; }

        public long ConfigId { get; init; }

        public int VersionNo { get; init; }

        public string FileToSourceJson { get; init; } = string.Empty;

        public string SourceToNormalizedJson { get; init; } = string.Empty;
    }
}
