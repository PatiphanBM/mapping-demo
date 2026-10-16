using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
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
            row_jobs.evaluation_at AS EvaluationAt,
            mapping_config_versions.transformation_program::text
                AS TransformationProgramJson,
            mapping_configs.source_table_id AS SourceTableId,
            mapping_configs.normalized_table_id AS NormalizedTableId,
            source_table.name AS SourceTableName,
            normalized_table.name AS NormalizedTableName,
            file_jobs.file_name AS FileName
        FROM row_jobs
        INNER JOIN file_jobs
            ON file_jobs.id = row_jobs.file_job_id
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

    private const string SelectTableColumnsSql = """
        SELECT
            name AS Name,
            data_type AS DataType,
            is_required AS IsRequired,
            ordinal AS Ordinal,
            length AS Length,
            precision AS Precision,
            scale AS Scale
        FROM table_columns
        WHERE table_id = @TableId
        ORDER BY ordinal;
        """;

    private const string InsertRowErrorSql = """
        INSERT INTO row_errors (
            row_job_id,
            file_job_id,
            row_number,
            output_key,
            field,
            expression_path,
            reason)
        VALUES (
            @RowJobId,
            @FileJobId,
            @RowNumber,
            @OutputKey,
            @Field,
            @ExpressionPath,
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
    private readonly TransformationModule _transformationModule;
    private readonly IReadOnlySet<int> _failNormalizeRowNumbers;
    private readonly TimeSpan _normalizeDelay;
    private readonly ILogger<RowNormalizeHandler> _logger;
    private readonly ConcurrentDictionary<long, CompiledRowConfig>
        _compiledConfigs = new();

    public RowNormalizeHandler(
        NpgsqlDataSource dataSource,
        TransformationModule transformationModule,
        IConfiguration configuration,
        ILogger<RowNormalizeHandler> logger)
    {
        _dataSource = dataSource;
        _transformationModule = transformationModule;
        var failNormalizeRowNumbers = configuration
            .GetSection("Demo:FailNormalizeRowNumbers")
            .Get<int[]>() ?? [];
        _failNormalizeRowNumbers = failNormalizeRowNumbers.ToHashSet();
        _normalizeDelay = TimeSpan.FromMilliseconds(
            configuration.GetValue<int>("Demo:NormalizeDelayMs"));
        _logger = logger;

        if (_failNormalizeRowNumbers.Any(rowNumber => rowNumber <= 0))
        {
            throw new InvalidOperationException(
                "Configuration 'Demo:FailNormalizeRowNumbers' must only contain positive row numbers.");
        }

        if (_normalizeDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Configuration 'Demo:NormalizeDelayMs' must not be negative.");
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
        var rowJob = await connection.QuerySingleOrDefaultAsync<RowJob>(
                new CommandDefinition(
                    SelectRowJobSql,
                    new { request.RowJobId },
                    transaction,
                    cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException(
                $"Row job {request.RowJobId} was not found.");

        if (rowJob.Status is RowJobStatus.Done
            or RowJobStatus.Filtered
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

        if (_normalizeDelay > TimeSpan.Zero)
        {
            await Task.Delay(_normalizeDelay, cancellationToken);
        }

        var compiledConfig = await GetCompiledConfigAsync(
            connection,
            transaction,
            rowJob,
            cancellationToken);
        var sourceRow = await LoadSourceRowAsync(
            connection,
            transaction,
            rowJob.SourceTableName,
            rowJob.SourceRowId,
            compiledConfig.SourceDependencies,
            cancellationToken);
        var evaluationAt = ToDateTimeOffset(rowJob.EvaluationAt);
        var transformationRow = new TransformationSourceRow(
            rowJob.SourceRowId.ToString(CultureInfo.InvariantCulture),
            sourceRow.Values,
            new Dictionary<string, object?>
            {
                ["fileName"] = rowJob.FileName,
                ["rowNumber"] = rowJob.RowNumber,
                ["physicalLineNumber"] = sourceRow.PhysicalLineNumber,
                ["evaluationTime"] = evaluationAt
            },
            evaluationAt,
            rowJob.SourceRowId);
        var batchResult = await _transformationModule.EvaluateAsync(
            compiledConfig.Program,
            new TransformationBatch([transformationRow]),
            cancellationToken);
        var result = batchResult.Rows.Single();

        string completedStatus;
        switch (result.Status)
        {
            case TransformationRowStatus.Done:
            case TransformationRowStatus.Filtered:
                completedStatus = await NormalizedRowWriter
                    .ReconcileAndCompleteAsync(
                        connection,
                        transaction,
                        rowJob.NormalizedTableName,
                        rowJob.SourceRowId,
                        rowJob.FileJobId,
                        rowJob.RowNumber,
                        rowJob.Id,
                        rowJob.ConfigVersionId,
                        compiledConfig.NormalizedColumnNames,
                        result.Outputs,
                        attempt,
                        cancellationToken);
                break;

            case TransformationRowStatus.Invalid:
                await InsertErrorsAsync(
                    connection,
                    transaction,
                    rowJob,
                    result.Errors,
                    cancellationToken);
                completedStatus = RowJobStatus.Invalid;
                await CompleteRowJobAsync(
                    connection,
                    transaction,
                    rowJob.Id,
                    completedStatus,
                    attempt,
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Transformation returned unsupported status '{result.Status}'.");
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Completed row job {RowJobId} with status {RowJobStatus} using config version {ConfigVersionId}",
            rowJob.Id,
            completedStatus,
            rowJob.ConfigVersionId);
    }

    private async Task<CompiledRowConfig> GetCompiledConfigAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RowJob rowJob,
        CancellationToken cancellationToken)
    {
        if (_compiledConfigs.TryGetValue(
                rowJob.ConfigVersionId,
                out var cached))
        {
            return cached;
        }

        var sourceColumns = await LoadTableColumnsAsync(
            connection,
            transaction,
            rowJob.SourceTableId,
            cancellationToken);
        var normalizedColumns = await LoadTableColumnsAsync(
            connection,
            transaction,
            rowJob.NormalizedTableId,
            cancellationToken);
        var program = PolymorphicJson.Deserialize<TransformationProgram>(
                rowJob.TransformationProgramJson,
                JsonOptions)
            ?? throw new JsonException(
                "Persisted transformation program cannot be null.");
        var compilation = _transformationModule.Compile(
            program,
            new TransformationSchema(
                new TableDefinition(
                    rowJob.SourceTableName,
                    TableKind.Source,
                    sourceColumns),
                new TableDefinition(
                    rowJob.NormalizedTableName,
                    TableKind.Normalized,
                    normalizedColumns)));
        if (!compilation.IsSuccess)
        {
            throw new InvalidOperationException(
                "Persisted transformation program did not compile: " +
                string.Join(
                    "; ",
                    compilation.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Path}: {diagnostic.Code}")));
        }

        var compiled = compilation.CompiledProgram!;
        var loaded = new CompiledRowConfig(
            compiled,
            _transformationModule.GetSourceDependencies(compiled),
            normalizedColumns
                .OrderBy(column => column.Ordinal)
                .Select(column => column.Name)
                .ToArray());
        return _compiledConfigs.GetOrAdd(rowJob.ConfigVersionId, loaded);
    }

    private static async Task<IReadOnlyList<ColumnDefinition>>
        LoadTableColumnsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long tableId,
            CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<TableColumnRow>(
            new CommandDefinition(
                SelectTableColumnsSql,
                new { TableId = tableId },
                transaction,
                cancellationToken: cancellationToken));

        return rows
            .Select(row => new ColumnDefinition(
                row.Name,
                Enum.Parse<ColumnDataType>(row.DataType),
                row.IsRequired,
                row.Ordinal,
                row.Length,
                row.Precision,
                row.Scale))
            .ToArray();
    }

    private static async Task<LoadedSourceRow> LoadSourceRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceTableName,
        long sourceRowId,
        IReadOnlyList<string> sourceDependencies,
        CancellationToken cancellationToken)
    {
        var dependencyProjection = sourceDependencies.Count == 0
            ? string.Empty
            : ", " + string.Join(
                ", ",
                sourceDependencies.Select(SqlIdentifier.Quote));
        var sql = $"""
            SELECT
                physical_line_number AS "PhysicalLineNumber"{dependencyProjection}
            FROM {SqlIdentifier.Quote(sourceTableName)}
            WHERE id = @SourceRowId;
            """;
        var row = await connection.QuerySingleOrDefaultAsync(
                new CommandDefinition(
                    sql,
                    new { SourceRowId = sourceRowId },
                    transaction,
                    cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException(
                $"Source row {sourceRowId} was not found in table '{sourceTableName}'.");
        var rowValues = (IDictionary<string, object?>)row;
        var sourceValues = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var sourceColumn in sourceDependencies)
        {
            if (!rowValues.TryGetValue(sourceColumn, out var value))
            {
                throw new InvalidOperationException(
                    $"Source column '{sourceColumn}' was not found in table '{sourceTableName}'.");
            }

            sourceValues.Add(sourceColumn, (string?)value);
        }

        return new LoadedSourceRow(
            sourceValues,
            Convert.ToInt32(
                rowValues["PhysicalLineNumber"],
                CultureInfo.InvariantCulture));
    }

    private static async Task InsertErrorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RowJob rowJob,
        IReadOnlyList<TransformationRowError> errors,
        CancellationToken cancellationToken)
    {
        foreach (var error in errors)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertRowErrorSql,
                new
                {
                    RowJobId = rowJob.Id,
                    rowJob.FileJobId,
                    rowJob.RowNumber,
                    error.OutputKey,
                    error.Field,
                    error.ExpressionPath,
                    error.Reason
                },
                transaction,
                cancellationToken: cancellationToken));
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
        var updatedRows = await connection.ExecuteAsync(new CommandDefinition(
            CompleteRowJobSql,
            new
            {
                RowJobId = rowJobId,
                Status = status,
                Attempts = attempts
            },
            transaction,
            cancellationToken: cancellationToken));
        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Row job {rowJobId} was not found while recording completion.");
        }
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
        var updatedRows = await connection.ExecuteAsync(new CommandDefinition(
            FailRowJobSql,
            new
            {
                RowJobId = rowJobId,
                Status = RowJobStatus.Failed,
                Attempts = attempts,
                LastError = lastError
            },
            transaction,
            cancellationToken: cancellationToken));

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Row job {rowJobId} was not found while recording failure.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime()),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
        };
    }

    private sealed record RowJob(
        long Id,
        long FileJobId,
        long SourceRowId,
        int RowNumber,
        long ConfigVersionId,
        string Status,
        DateTime EvaluationAt,
        string TransformationProgramJson,
        long SourceTableId,
        long NormalizedTableId,
        string SourceTableName,
        string NormalizedTableName,
        string FileName);

    private sealed record TableColumnRow(
        string Name,
        string DataType,
        bool IsRequired,
        int Ordinal,
        int? Length,
        int? Precision,
        int? Scale);

    private sealed record CompiledRowConfig(
        CompiledTransformationProgram Program,
        IReadOnlyList<string> SourceDependencies,
        IReadOnlyList<string> NormalizedColumnNames);

    private sealed record LoadedSourceRow(
        IReadOnlyDictionary<string, string?> Values,
        int PhysicalLineNumber);
}
