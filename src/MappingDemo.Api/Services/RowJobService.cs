using System.Globalization;
using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class RowJobService
{
    private const string RetryRowJobSql = """
        UPDATE row_jobs
        SET
            status = @PendingStatus,
            attempts = 0,
            last_error = NULL,
            finished_at = NULL
        WHERE id = @RowJobId
          AND status = @FailedStatus
        RETURNING source_row_id;
        """;

    private const string RowJobExistsSql = """
        SELECT EXISTS (
            SELECT 1
            FROM row_jobs
            WHERE id = @RowJobId);
        """;

    private readonly NpgsqlDataSource _dataSource;

    public RowJobService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<JobRetryResult> RetryAsync(
        long rowJobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var retryCommand = new CommandDefinition(
            RetryRowJobSql,
            new
            {
                RowJobId = rowJobId,
                PendingStatus = RowJobStatus.Pending,
                FailedStatus = RowJobStatus.Failed
            },
            transaction,
            cancellationToken: cancellationToken);
        var sourceRowId =
            await connection.ExecuteScalarAsync<long?>(retryCommand);

        if (!sourceRowId.HasValue)
        {
            var existsCommand = new CommandDefinition(
                RowJobExistsSql,
                new { RowJobId = rowJobId },
                transaction,
                cancellationToken: cancellationToken);
            var exists = await connection.ExecuteScalarAsync<bool>(
                existsCommand);
            await transaction.RollbackAsync(cancellationToken);

            return exists
                ? JobRetryResult.Conflict
                : JobRetryResult.NotFound;
        }

        await OutboxWriter.AddAsync(
            connection,
            transaction,
            Topics.RowNormalize,
            sourceRowId.Value.ToString(CultureInfo.InvariantCulture),
            new RowNormalizeRequested(rowJobId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return JobRetryResult.Retried;
    }
}
