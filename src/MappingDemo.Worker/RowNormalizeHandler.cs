using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Normalization;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Worker;

public sealed class RowNormalizeHandler
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ];

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string SelectRowJobSql = """
        SELECT
            row_jobs.id AS Id,
            row_jobs.file_job_id AS FileJobId,
            row_jobs.source_row_id AS SourceRowId,
            row_jobs.row_number AS RowNumber,
            row_jobs.config_version_id AS ConfigVersionId,
            row_jobs.status AS Status,
            mapping_config_versions.source_to_normalized::text
                AS SourceToNormalizedJson,
            mapping_configs.normalized_table_id AS NormalizedTableId,
            source_table.name AS SourceTableName,
            normalized_table.name AS NormalizedTableName
        FROM row_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id = row_jobs.config_version_id
        INNER JOIN mapping_configs
            ON mapping_configs.id = mapping_config_versions.config_id
        INNER JOIN table_definitions AS source_table
            ON source_table.id = mapping_configs.source_table_id
        INNER JOIN table_definitions AS normalized_table
            ON normalized_table.id = mapping_configs.normalized_table_id
        WHERE row_jobs.id = @RowJobId
        FOR UPDATE OF row_jobs;
        """;

    private const string SelectNormalizedColumnsSql = """
        SELECT
            name AS Name,
            data_type AS DataType,
            is_required AS IsRequired,
            ordinal AS Ordinal
        FROM table_columns
        WHERE table_id = @NormalizedTableId
        ORDER BY ordinal;
        """;

    private const string InsertRowErrorSql = """
        INSERT INTO row_errors (
            row_job_id,
            file_job_id,
            row_number,
            field,
            reason)
        VALUES (
            @RowJobId,
            @FileJobId,
            @RowNumber,
            @Field,
            @Reason);
        """;

    private const string CompleteRowJobSql = """
        UPDATE row_jobs
        SET
            status = @Status,
            attempts = @Attempts,
            last_error = NULL,
            finished_at = now()
        WHERE id = @RowJobId;
        """;

    private const string FailRowJobSql = """
        UPDATE row_jobs
        SET
            status = @Status,
            attempts = @Attempts,
            last_error = @LastError,
            finished_at = now()
        WHERE id = @RowJobId;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlySet<int> _failNormalizeRowNumbers;
    private readonly ILogger<RowNormalizeHandler> _logger;

    public RowNormalizeHandler(
        NpgsqlDataSource dataSource,
        IConfiguration configuration,
        ILogger<RowNormalizeHandler> logger)
    {
        _dataSource = dataSource;
        var failNormalizeRowNumbers = configuration
            .GetSection("Demo:FailNormalizeRowNumbers")
            .Get<int[]>() ?? [];
        _failNormalizeRowNumbers = failNormalizeRowNumbers.ToHashSet();
        _logger = logger;

        if (_failNormalizeRowNumbers.Any(rowNumber => rowNumber <= 0))
        {
            throw new InvalidOperationException(
                "Configuration 'Demo:FailNormalizeRowNumbers' must only contain positive row numbers.");
        }
    }

    public async Task HandleAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<RowNormalizeRequested>(
                payload,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Row normalize message payload is empty.");

        Exception? lastException = null;

        for (var attempt = 1; attempt <= RetryDelays.Length + 1; attempt++)
        {
            try
            {
                await ProcessOnceAsync(request, attempt, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;

                if (attempt > RetryDelays.Length)
                {
                    break;
                }

                var retryDelay = RetryDelays[attempt - 1];
                _logger.LogWarning(
                    exception,
                    "Row job {RowJobId} failed on attempt {Attempt}; retrying in {RetryDelaySeconds} seconds",
                    request.RowJobId,
                    attempt,
                    retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        var totalAttempts = RetryDelays.Length + 1;
        await SetFailedAsync(
            request.RowJobId,
            totalAttempts,
            lastException!.Message,
            cancellationToken);
        _logger.LogError(
            lastException,
            "Row job {RowJobId} failed after {Attempts} attempts",
            request.RowJobId,
            totalAttempts);
    }

    private async Task ProcessOnceAsync(
        RowNormalizeRequested request,
        int attempt,
        CancellationToken cancellationToken)
    {

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectRowJobSql,
            new { request.RowJobId },
            transaction,
            cancellationToken: cancellationToken);
        var rowJob = await connection.QuerySingleOrDefaultAsync<RowJob>(command)
            ?? throw new InvalidOperationException(
                $"Row job {request.RowJobId} was not found.");

        if (rowJob.Status is RowJobStatus.Done
            or RowJobStatus.Invalid
            or RowJobStatus.Failed)
        {
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Skipping row job {RowJobId} because status is {RowJobStatus}",
                rowJob.Id,
                rowJob.Status);
            return;
        }

        if (_failNormalizeRowNumbers.Contains(rowJob.RowNumber))
        {
            throw new InvalidOperationException(
                $"Demo normalize failure at row {rowJob.RowNumber}.");
        }

        var rules = JsonSerializer.Deserialize<SourceToNormalizedRule[]>(
                rowJob.SourceToNormalizedJson,
                JsonOptions) ?? [];
        var normalizedColumns = await LoadNormalizedColumnsAsync(
            connection,
            transaction,
            rowJob.NormalizedTableId,
            cancellationToken);
        var sourceRow = await LoadSourceRowAsync(
            connection,
            transaction,
            rowJob.SourceTableName,
            rowJob.SourceRowId,
            rules,
            cancellationToken);

        _logger.LogInformation(
            "Loaded {RuleCount} normalization rules and source row {SourceRowId} from {SourceTableName} for row job {RowJobId} using config version {ConfigVersionId}",
            rules.Length,
            rowJob.SourceRowId,
            rowJob.SourceTableName,
            rowJob.Id,
            rowJob.ConfigVersionId);

        var result = RowNormalizer.Normalize(
            sourceRow,
            rules,
            normalizedColumns);

        string completedStatus;
        switch (result)
        {
            case RowNormalizationResult.Success success:
                var normalizedValues = normalizedColumns.ToDictionary(
                    column => column.Name,
                    column => success.Values.TryGetValue(
                        column.Name,
                        out var value)
                            ? value
                            : null,
                    StringComparer.Ordinal);
                await NormalizedRowWriter.UpsertAsync(
                    connection,
                    transaction,
                    rowJob.NormalizedTableName,
                    rowJob.SourceRowId,
                    rowJob.FileJobId,
                    rowJob.RowNumber,
                    rowJob.Id,
                    rowJob.ConfigVersionId,
                    normalizedValues,
                    cancellationToken);
                completedStatus = RowJobStatus.Done;
                break;

            case RowNormalizationResult.Failure failure:
                await InsertErrorsAsync(
                    connection,
                    transaction,
                    rowJob,
                    failure.Errors,
                    cancellationToken);
                completedStatus = RowJobStatus.Invalid;
                break;

            default:
                throw new InvalidOperationException(
                    "Row normalizer returned an unsupported result.");
        }

        await CompleteRowJobAsync(
            connection,
            transaction,
            rowJob.Id,
            completedStatus,
            attempt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Completed row job {RowJobId} with status {RowJobStatus}",
            rowJob.Id,
            completedStatus);
    }

    private static async Task<IReadOnlyList<ColumnDefinition>>
        LoadNormalizedColumnsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long normalizedTableId,
            CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            SelectNormalizedColumnsSql,
            new { NormalizedTableId = normalizedTableId },
            transaction,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<NormalizedColumnRow>(command);

        return rows
            .Select(row => new ColumnDefinition(
                row.Name,
                Enum.Parse<ColumnDataType>(row.DataType),
                row.IsRequired,
                row.Ordinal))
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, string?>>
        LoadSourceRowAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sourceTableName,
            long sourceRowId,
            IReadOnlyList<SourceToNormalizedRule> rules,
            CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT *
            FROM {SqlIdentifier.Quote(sourceTableName)}
            WHERE id = @SourceRowId;
            """;
        var command = new CommandDefinition(
            sql,
            new { SourceRowId = sourceRowId },
            transaction,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync(command)
            ?? throw new InvalidOperationException(
                $"Source row {sourceRowId} was not found in table '{sourceTableName}'.");
        var rowValues = (IDictionary<string, object?>)row;
        var sourceRow = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var sourceColumn in rules
                     .Select(rule => rule.SourceColumn)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!rowValues.TryGetValue(sourceColumn, out var value))
            {
                throw new InvalidOperationException(
                    $"Source column '{sourceColumn}' was not found in table '{sourceTableName}'.");
            }

            sourceRow.Add(sourceColumn, (string?)value);
        }

        return sourceRow;
    }

    private static async Task InsertErrorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RowJob rowJob,
        IReadOnlyList<RowNormalizationError> errors,
        CancellationToken cancellationToken)
    {
        foreach (var error in errors)
        {
            var command = new CommandDefinition(
                InsertRowErrorSql,
                new
                {
                    RowJobId = rowJob.Id,
                    rowJob.FileJobId,
                    rowJob.RowNumber,
                    error.Field,
                    error.Reason
                },
                transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command);
        }
    }

    private static async Task CompleteRowJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rowJobId,
        string status,
        int attempts,
        CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            CompleteRowJobSql,
            new
            {
                RowJobId = rowJobId,
                Status = status,
                Attempts = attempts
            },
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private async Task SetFailedAsync(
        long rowJobId,
        int attempts,
        string lastError,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            FailRowJobSql,
            new
            {
                RowJobId = rowJobId,
                Status = RowJobStatus.Failed,
                Attempts = attempts,
                LastError = lastError
            },
            transaction,
            cancellationToken: cancellationToken);
        var updatedRows = await connection.ExecuteAsync(command);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Row job {rowJobId} was not found while recording failure.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record RowJob(
        long Id,
        long FileJobId,
        long SourceRowId,
        int RowNumber,
        long ConfigVersionId,
        string Status,
        string SourceToNormalizedJson,
        long NormalizedTableId,
        string SourceTableName,
        string NormalizedTableName);

    private sealed record NormalizedColumnRow(
        string Name,
        string DataType,
        bool IsRequired,
        int Ordinal);
}
