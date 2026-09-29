using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Worker;

public sealed class FileImportHandler
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string ContentHashUniqueConstraint =
        "ux_file_jobs_config_content_hash";

    private const string SelectFileJobSql = """
        SELECT
            id,
            config_id AS ConfigId,
            original_path AS OriginalPath,
            snapshot_path AS SnapshotPath,
            import_status AS ImportStatus
        FROM file_jobs
        WHERE id = @FileJobId;
        """;

    private const string SetDelayingSql = """
        UPDATE file_jobs
        SET
            import_status = @ImportStatus,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string SetImportingSql = """
        UPDATE file_jobs
        SET
            import_status = @ImportStatus,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string SetSnapshotPathSql = """
        UPDATE file_jobs
        SET
            snapshot_path = @SnapshotPath,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string SetContentHashSql = """
        UPDATE file_jobs
        SET
            content_hash = @ContentHash,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string HasDuplicateSql = """
        SELECT EXISTS (
            SELECT 1
            FROM file_jobs
            WHERE config_id = @ConfigId
              AND id <> @FileJobId
              AND content_hash = @ContentHash
              AND import_status <> @DuplicateStatus);
        """;

    private const string SetDuplicateSql = """
        UPDATE file_jobs
        SET
            import_status = @DuplicateStatus,
            content_hash = @ContentHash,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _delay;
    private readonly string _stagingRootPath;
    private readonly ILogger<FileImportHandler> _logger;

    public FileImportHandler(
        NpgsqlDataSource dataSource,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<FileImportHandler> logger)
    {
        _dataSource = dataSource;
        _delay = TimeSpan.FromSeconds(
            configuration.GetValue<int>("Import:DelaySeconds"));
        var stagingRootPath = configuration["Import:StagingRoot"]
            ?? throw new InvalidOperationException(
                "Configuration 'Import:StagingRoot' is not set.");
        _stagingRootPath = Path.GetFullPath(
            stagingRootPath,
            hostEnvironment.ContentRootPath);
        _logger = logger;

        if (_delay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Configuration 'Import:DelaySeconds' must be greater than zero.");
        }
    }

    public async Task<bool> HandleAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<FileImportRequested>(
                payload,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "File import message payload is empty.");

        var fileJob = await LoadFileJobAsync(
                request.FileJobId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"File job {request.FileJobId} was not found.");

        if (fileJob.ImportStatus is ImportStatus.Imported
            or ImportStatus.Duplicate)
        {
            _logger.LogInformation(
                "Skipping file job {FileJobId} because import status is {ImportStatus}",
                fileJob.Id,
                fileJob.ImportStatus);
            return true;
        }

        await SetDelayingAsync(fileJob.Id, cancellationToken);

        _logger.LogInformation(
            "File job {FileJobId} is delaying for {DelaySeconds} seconds",
            fileJob.Id,
            _delay.TotalSeconds);
        await Task.Delay(_delay, cancellationToken);
        _logger.LogInformation(
            "File job {FileJobId} completed its delay",
            fileJob.Id);

        await SetImportingAsync(fileJob.Id, cancellationToken);

        Directory.CreateDirectory(_stagingRootPath);
        var snapshotPath = fileJob.SnapshotPath
            ?? Path.Combine(_stagingRootPath, $"{fileJob.Id}.csv");

        if (File.Exists(snapshotPath))
        {
            _logger.LogInformation(
                "File job {FileJobId} is reusing snapshot {SnapshotPath}",
                fileJob.Id,
                snapshotPath);
        }
        else
        {
            File.Copy(fileJob.OriginalPath, snapshotPath);
            _logger.LogInformation(
                "Copied file job {FileJobId} to snapshot {SnapshotPath}",
                fileJob.Id,
                snapshotPath);
        }

        await SetSnapshotPathAsync(
            fileJob.Id,
            snapshotPath,
            cancellationToken);

        await using var snapshotStream = File.OpenRead(snapshotPath);
        var hash = await SHA256.HashDataAsync(
            snapshotStream,
            cancellationToken);
        var contentHash = Convert.ToHexString(hash);

        if (await HasDuplicateAsync(
                fileJob.Id,
                fileJob.ConfigId,
                contentHash,
                cancellationToken))
        {
            await SetDuplicateAsync(
                fileJob.Id,
                contentHash,
                cancellationToken);
            LogDuplicate(fileJob.Id, contentHash);
            return true;
        }

        try
        {
            await SetContentHashAsync(
                fileJob.Id,
                contentHash,
                cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                  && exception.ConstraintName == ContentHashUniqueConstraint)
        {
            await SetDuplicateAsync(
                fileJob.Id,
                contentHash,
                cancellationToken);
            LogDuplicate(fileJob.Id, contentHash);
            return true;
        }

        _logger.LogInformation(
            "Calculated content hash {ContentHash} for file job {FileJobId}",
            contentHash,
            fileJob.Id);

        return false;
    }

    private async Task<FileJob?> LoadFileJobAsync(
        long fileJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectFileJobSql,
            new { FileJobId = fileJobId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<FileJob>(command);
    }

    private async Task SetDelayingAsync(
        long fileJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetDelayingSql,
            new
            {
                FileJobId = fileJobId,
                ImportStatus = ImportStatus.Delaying
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task SetImportingAsync(
        long fileJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetImportingSql,
            new
            {
                FileJobId = fileJobId,
                ImportStatus = ImportStatus.Importing
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task SetSnapshotPathAsync(
        long fileJobId,
        string snapshotPath,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetSnapshotPathSql,
            new
            {
                FileJobId = fileJobId,
                SnapshotPath = snapshotPath
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task SetContentHashAsync(
        long fileJobId,
        string contentHash,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetContentHashSql,
            new
            {
                FileJobId = fileJobId,
                ContentHash = contentHash
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task<bool> HasDuplicateAsync(
        long fileJobId,
        long configId,
        string contentHash,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            HasDuplicateSql,
            new
            {
                FileJobId = fileJobId,
                ConfigId = configId,
                ContentHash = contentHash,
                DuplicateStatus = ImportStatus.Duplicate
            },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    private async Task SetDuplicateAsync(
        long fileJobId,
        string contentHash,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetDuplicateSql,
            new
            {
                FileJobId = fileJobId,
                ContentHash = contentHash,
                DuplicateStatus = ImportStatus.Duplicate
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private void LogDuplicate(long fileJobId, string contentHash)
    {
        _logger.LogInformation(
            "File job {FileJobId} is a duplicate with content hash {ContentHash}",
            fileJobId,
            contentHash);
    }

    private sealed record FileJob(
        long Id,
        long ConfigId,
        string OriginalPath,
        string? SnapshotPath,
        string ImportStatus);
}
