using System.Globalization;
using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class FileIntake
{
    private const string InsertFileJobSql = """
        INSERT INTO file_jobs (
            config_id,
            config_version_id,
            original_path,
            file_name,
            intake_key,
            import_status,
            archive_status)
        SELECT
            mapping_configs.id,
            mapping_configs.active_version_id,
            @OriginalPath,
            @FileName,
            @IntakeKey,
            @ImportStatus,
            @ArchiveStatus
        FROM mapping_configs
        WHERE mapping_configs.id = @ConfigId
          AND mapping_configs.active_version_id IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM file_jobs
              WHERE file_jobs.config_id = mapping_configs.id
                AND file_jobs.original_path = @OriginalPath
                AND file_jobs.import_status IN (
                    @QueuedStatus,
                    @DelayingStatus,
                    @ImportingStatus))
        ON CONFLICT (config_id, intake_key) DO NOTHING
        RETURNING id;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public FileIntake(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public FileIntakeDetails Inspect(string path)
    {
        var file = new FileInfo(path);
        var fullPath = file.FullName;
        var size = file.Length;
        var lastWriteTimeUtc = file.LastWriteTimeUtc;
        var intakeKey =
            $"{fullPath}|{size}|{lastWriteTimeUtc.Ticks}";

        return new FileIntakeDetails(
            fullPath,
            file.Name,
            size,
            lastWriteTimeUtc,
            intakeKey);
    }

    public async Task<long?> CreateJobAsync(
        long configId,
        FileIntakeDetails intake,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            ConfigId = configId,
            OriginalPath = intake.Path,
            intake.FileName,
            intake.IntakeKey,
            ImportStatus = ImportStatus.Queued,
            ArchiveStatus = ArchiveStatus.NotArchived,
            QueuedStatus = ImportStatus.Queued,
            DelayingStatus = ImportStatus.Delaying,
            ImportingStatus = ImportStatus.Importing
        };
        var command = new CommandDefinition(
            InsertFileJobSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        var fileJobId = await connection.ExecuteScalarAsync<long?>(command);

        if (fileJobId.HasValue)
        {
            await OutboxWriter.AddAsync(
                connection,
                transaction,
                Topics.FileImport,
                fileJobId.Value.ToString(CultureInfo.InvariantCulture),
                new FileImportRequested(fileJobId.Value),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return fileJobId;
    }
}

public sealed record FileIntakeDetails(
    string Path,
    string FileName,
    long Size,
    DateTime LastWriteTimeUtc,
    string IntakeKey);
