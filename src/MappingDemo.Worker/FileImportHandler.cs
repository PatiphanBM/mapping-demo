using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Csv;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.MappingConfigs;
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
            file_jobs.id AS Id,
            file_jobs.config_id AS ConfigId,
            file_jobs.config_version_id AS ConfigVersionId,
            file_jobs.original_path AS OriginalPath,
            file_jobs.snapshot_path AS SnapshotPath,
            file_jobs.import_status AS ImportStatus,
            mapping_config_versions.file_to_source::text
                AS FileToSourceJson,
            source_table.name AS SourceTableName
        FROM file_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id = file_jobs.config_version_id
           AND mapping_config_versions.config_id = file_jobs.config_id
        INNER JOIN mapping_configs
            ON mapping_configs.id = file_jobs.config_id
        INNER JOIN table_definitions AS source_table
            ON source_table.id = mapping_configs.source_table_id
        WHERE file_jobs.id = @FileJobId;
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

    private const string SetImportFailedSql = """
        UPDATE file_jobs
        SET
            import_status = @ImportStatus,
            last_error = @LastError,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string SetImportedSql = """
        UPDATE file_jobs
        SET
            import_status = @ImportStatus,
            total_rows = @TotalRows,
            updated_at = now()
        WHERE id = @FileJobId;
        """;

    private const string InsertRowJobSql = """
        INSERT INTO row_jobs (
            file_job_id,
            source_row_id,
            row_number,
            config_version_id,
            kind,
            status)
        VALUES (
            @FileJobId,
            @SourceRowId,
            @RowNumber,
            @ConfigVersionId,
            @Kind,
            @Status)
        RETURNING id;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly CsvRecordReader _csvRecordReader;
    private readonly TimeSpan _delay;
    private readonly TimeSpan _importRowDelay;
    private readonly int? _failImportAtRow;
    private readonly string _stagingRootPath;
    private readonly ILogger<FileImportHandler> _logger;

    public FileImportHandler(
        NpgsqlDataSource dataSource,
        CsvRecordReader csvRecordReader,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<FileImportHandler> logger)
    {
        _dataSource = dataSource;
        _csvRecordReader = csvRecordReader;
        _delay = TimeSpan.FromSeconds(
            configuration.GetValue<int>("Import:DelaySeconds"));
        _importRowDelay = TimeSpan.FromMilliseconds(
            configuration.GetValue<int>("Demo:ImportRowDelayMs"));
        _failImportAtRow =
            configuration.GetValue<int?>("Demo:FailImportAtRow");
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

        if (_importRowDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Configuration 'Demo:ImportRowDelayMs' must not be negative.");
        }

        if (_failImportAtRow.HasValue && _failImportAtRow.Value <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Demo:FailImportAtRow' must be greater than zero when set.");
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
            or ImportStatus.Duplicate
            or ImportStatus.ImportFailed)
        {
            _logger.LogInformation(
                "Skipping file job {FileJobId} because import status is {ImportStatus}",
                fileJob.Id,
                fileJob.ImportStatus);
            return true;
        }

        var fileToSourceRules =
            JsonSerializer.Deserialize<FileToSourceRule[]>(
                fileJob.FileToSourceJson,
                JsonOptions) ?? [];
        var rulesLog = string.Join(
            ", ",
            fileToSourceRules.Select(
                rule => $"{rule.CsvHeader}->{rule.SourceColumn}"));

        _logger.LogInformation(
            "File job {FileJobId} uses config version {ConfigVersionId}, source table {SourceTableName}, and file-to-source rules {FileToSourceRules}",
            fileJob.Id,
            fileJob.ConfigVersionId,
            fileJob.SourceTableName,
            rulesLog);

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

        IReadOnlyList<string> headers;
        using (var snapshotReader = File.OpenText(snapshotPath))
        {
            headers = await _csvRecordReader.ReadHeaderAsync(
                snapshotReader,
                cancellationToken);
        }
        var availableHeaders = headers.ToHashSet(StringComparer.Ordinal);
        var missingHeaders = fileToSourceRules
            .Select(rule => rule.CsvHeader)
            .Where(header => !availableHeaders.Contains(header))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (missingHeaders.Length > 0)
        {
            var lastError =
                $"Missing required CSV headers: {string.Join(", ", missingHeaders)}.";
            await SetImportFailedAsync(
                fileJob.Id,
                lastError,
                cancellationToken);
            _logger.LogInformation(
                "File job {FileJobId} failed header validation: {LastError}",
                fileJob.Id,
                lastError);
            return true;
        }

        try
        {
            using var recordsReader = File.OpenText(snapshotPath);
            var totalRows = 0;
            await foreach (var record in _csvRecordReader.ReadAsync(
                               recordsReader,
                               cancellationToken))
            {
                if (_failImportAtRow == record.RowNumber)
                {
                    throw new InvalidOperationException(
                        $"Demo import failure at row {record.RowNumber}.");
                }

                await ImportRecordAsync(
                    fileJob,
                    fileToSourceRules,
                    record,
                    cancellationToken);
                totalRows++;

                if (_importRowDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_importRowDelay, cancellationToken);
                }
            }

            await SetImportedAsync(fileJob.Id, totalRows, cancellationToken);

            _logger.LogInformation(
                "Imported {TotalRows} rows for file job {FileJobId}",
                totalRows,
                fileJob.Id);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await SetImportFailedAsync(
                fileJob.Id,
                exception.Message,
                cancellationToken);
            _logger.LogError(
                exception,
                "File job {FileJobId} failed during import: {LastError}",
                fileJob.Id,
                exception.Message);
        }

        return true;
    }

    private async Task ImportRecordAsync(
        FileJob fileJob,
        IReadOnlyList<FileToSourceRule> rules,
        CsvRecord record,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var sourceRowId = await SourceRowWriter.InsertAsync(
            connection,
            transaction,
            fileJob.SourceTableName,
            rules,
            fileJob.Id,
            record,
            cancellationToken);

        if (sourceRowId is not null)
        {
            var rowJobCommand = new CommandDefinition(
                InsertRowJobSql,
                new
                {
                    FileJobId = fileJob.Id,
                    SourceRowId = sourceRowId.Value,
                    record.RowNumber,
                    ConfigVersionId = fileJob.ConfigVersionId,
                    Kind = "Initial",
                    Status = RowJobStatus.Pending
                },
                transaction,
                cancellationToken: cancellationToken);
            var rowJobId =
                await connection.ExecuteScalarAsync<long>(rowJobCommand);

            await OutboxWriter.AddAsync(
                connection,
                transaction,
                Topics.RowNormalize,
                sourceRowId.Value.ToString(CultureInfo.InvariantCulture),
                new RowNormalizeRequested(rowJobId),
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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

    private async Task SetImportFailedAsync(
        long fileJobId,
        string lastError,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetImportFailedSql,
            new
            {
                FileJobId = fileJobId,
                ImportStatus = ImportStatus.ImportFailed,
                LastError = lastError
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task SetImportedAsync(
        long fileJobId,
        int totalRows,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SetImportedSql,
            new
            {
                FileJobId = fileJobId,
                ImportStatus = ImportStatus.Imported,
                TotalRows = totalRows
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
        long ConfigVersionId,
        string OriginalPath,
        string? SnapshotPath,
        string ImportStatus,
        string FileToSourceJson,
        string SourceTableName);
}
