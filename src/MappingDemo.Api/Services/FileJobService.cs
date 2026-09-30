using Dapper;
using MappingDemo.Api.Contracts.FileJobs;
using MappingDemo.Shared.Jobs;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class FileJobService
{
    private const string SelectFileJobsSql = """
        WITH latest_row_jobs AS (
            SELECT DISTINCT ON (file_job_id, source_row_id)
                file_job_id,
                source_row_id,
                status
            FROM row_jobs
            ORDER BY file_job_id, source_row_id, id DESC
        ),
        row_job_counts AS (
            SELECT
                file_job_id,
                COUNT(*) FILTER (WHERE status = 'Pending')::integer
                    AS "PendingRows",
                COUNT(*) FILTER (WHERE status = 'Done')::integer
                    AS "DoneRows",
                COUNT(*) FILTER (WHERE status = 'Invalid')::integer
                    AS "InvalidRows",
                COUNT(*) FILTER (WHERE status = 'Failed')::integer
                    AS "FailedRows"
            FROM latest_row_jobs
            GROUP BY file_job_id
        )
        SELECT
            file_jobs.id AS "Id",
            file_jobs.config_id AS "ConfigId",
            file_jobs.config_version_id AS "ConfigVersionId",
            file_jobs.file_name AS "FileName",
            file_jobs.import_status AS "ImportStatus",
            file_jobs.archive_status AS "ArchiveStatus",
            file_jobs.total_rows AS "TotalRows",
            COALESCE(row_job_counts."PendingRows", 0) AS "PendingRows",
            COALESCE(row_job_counts."DoneRows", 0) AS "DoneRows",
            COALESCE(row_job_counts."InvalidRows", 0) AS "InvalidRows",
            COALESCE(row_job_counts."FailedRows", 0) AS "FailedRows",
            file_jobs.last_error AS "LastError",
            file_jobs.created_at AS "CreatedAt",
            file_jobs.updated_at AS "UpdatedAt"
        FROM file_jobs
        LEFT JOIN row_job_counts
            ON row_job_counts.file_job_id = file_jobs.id
        WHERE CAST(@Id AS bigint) IS NULL OR file_jobs.id = @Id
        ORDER BY file_jobs.id DESC;
        """;

    private const string SelectLatestErrorsSql = """
        WITH latest_row_jobs AS (
            SELECT DISTINCT ON (source_row_id)
                id,
                source_row_id
            FROM row_jobs
            WHERE file_job_id = @FileJobId
            ORDER BY source_row_id, id DESC
        )
        SELECT
            row_errors.row_number AS "RowNumber",
            row_errors.field AS "Field",
            row_errors.reason AS "Reason"
        FROM latest_row_jobs
        INNER JOIN row_errors
            ON row_errors.row_job_id = latest_row_jobs.id
        ORDER BY
            row_errors.row_number,
            row_errors.field,
            row_errors.id;
        """;

    private const string SelectRowHistorySql = """
        SELECT
            row_jobs.id AS "RowJobId",
            row_jobs.config_version_id AS "ConfigVersionId",
            mapping_config_versions.version_no AS "VersionNo",
            row_jobs.kind AS "Kind",
            row_jobs.status AS "Status",
            row_jobs.attempts AS "Attempts",
            row_jobs.last_error AS "LastError",
            row_jobs.created_at AS "CreatedAt",
            row_jobs.finished_at AS "FinishedAt",
            row_errors.id AS "ErrorId",
            row_errors.row_number AS "ErrorRowNumber",
            row_errors.field AS "ErrorField",
            row_errors.reason AS "ErrorReason"
        FROM row_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id = row_jobs.config_version_id
        LEFT JOIN row_errors
            ON row_errors.row_job_id = row_jobs.id
        WHERE row_jobs.file_job_id = @FileJobId
          AND row_jobs.row_number = @RowNumber
        ORDER BY row_jobs.id, row_errors.id;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public FileJobService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<FileJobResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(id: null, cancellationToken);
        return rows.Select(ToResponse).ToArray();
    }

    public async Task<FileJobResponse?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(id, cancellationToken);
        var row = rows.SingleOrDefault();
        return row is null ? null : ToResponse(row);
    }

    public async Task<IReadOnlyList<RowErrorResponse>> GetErrorsAsync(
        long fileJobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectLatestErrorsSql,
            new { FileJobId = fileJobId },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<RowErrorResponse>(command);

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<RowJobHistoryResponse>> GetRowHistoryAsync(
        long fileJobId,
        int rowNumber,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectRowHistorySql,
            new
            {
                FileJobId = fileJobId,
                RowNumber = rowNumber
            },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<RowHistoryRow>(command);

        return rows
            .GroupBy(row => row.RowJobId)
            .Select(group =>
            {
                var rowJob = group.First();
                var errors = group
                    .Where(row => row.ErrorId.HasValue)
                    .Select(row => new RowErrorResponse(
                        row.ErrorRowNumber!.Value,
                        row.ErrorField!,
                        row.ErrorReason!))
                    .ToArray();

                return new RowJobHistoryResponse(
                    rowJob.RowJobId,
                    rowJob.ConfigVersionId,
                    rowJob.VersionNo,
                    rowJob.Kind,
                    rowJob.Status,
                    rowJob.Attempts,
                    rowJob.LastError,
                    rowJob.CreatedAt,
                    rowJob.FinishedAt,
                    errors);
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<FileJobRow>> GetRowsAsync(
        long? id,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectFileJobsSql,
            new { Id = id },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<FileJobRow>(command);

        return rows.ToArray();
    }

    private static FileJobResponse ToResponse(FileJobRow row)
    {
        var normalizationStatus = NormalizationStatusCalculator.Calculate(
            row.ImportStatus,
            row.PendingRows,
            row.DoneRows,
            row.InvalidRows,
            row.FailedRows);

        return new FileJobResponse(
            row.Id,
            row.ConfigId,
            row.ConfigVersionId,
            row.FileName,
            row.ImportStatus,
            row.ArchiveStatus,
            row.TotalRows,
            normalizationStatus,
            row.PendingRows,
            row.DoneRows,
            row.InvalidRows,
            row.FailedRows,
            row.LastError,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private sealed class FileJobRow
    {
        public long Id { get; init; }

        public long ConfigId { get; init; }

        public long ConfigVersionId { get; init; }

        public string FileName { get; init; } = string.Empty;

        public string ImportStatus { get; init; } = string.Empty;

        public string ArchiveStatus { get; init; } = string.Empty;

        public int? TotalRows { get; init; }

        public int PendingRows { get; init; }

        public int DoneRows { get; init; }

        public int InvalidRows { get; init; }

        public int FailedRows { get; init; }

        public string? LastError { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }
    }

    private sealed class RowHistoryRow
    {
        public long RowJobId { get; init; }

        public long ConfigVersionId { get; init; }

        public int VersionNo { get; init; }

        public string Kind { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public string? LastError { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? FinishedAt { get; init; }

        public long? ErrorId { get; init; }

        public int? ErrorRowNumber { get; init; }

        public string? ErrorField { get; init; }

        public string? ErrorReason { get; init; }
    }
}
