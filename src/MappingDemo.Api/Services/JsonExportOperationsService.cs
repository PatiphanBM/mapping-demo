using Dapper;
using MappingDemo.Api.Contracts.DeliveryJobs;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class JsonExportOperationsService(NpgsqlDataSource dataSource)
{
    private const string SelectHistorySql = """
        SELECT
            id AS "Id",
            file_job_id AS "FileJobId",
            config_version_id AS "ConfigVersionId",
            destination_profile_id AS "DestinationProfileId",
            artifact_revision AS "ArtifactRevision",
            status AS "Status",
            relative_final_path AS "RelativeDisplayPath",
            object_count AS "ObjectCount",
            byte_count AS "ByteCount",
            sha256 AS "Sha256",
            attempt_count AS "AttemptCount",
            CASE
                WHEN status IN ('Blocked', 'RetryableFailed')
                THEN last_error_redacted
                ELSE NULL
            END AS "LastErrorRedacted",
            created_at AS "CreatedAt",
            updated_at AS "UpdatedAt",
            published_at AS "PublishedAt",
            (status = 'RetryableFailed') AS "CanRetry"
        FROM json_export_jobs
        WHERE file_job_id = @FileJobId
        ORDER BY artifact_revision DESC, id DESC;
        """;

    public async Task<IReadOnlyList<JsonExportHistoryResponse>>
        GetForFileJobAsync(
            long fileJobId,
            CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<JsonExportHistoryRow>(
            new CommandDefinition(
                SelectHistorySql,
                new { FileJobId = fileJobId },
                cancellationToken: cancellationToken));
        return rows.Select(ToResponse).ToArray();
    }

    public async Task<DeliveryActionResult> RetryAsync(
        long jsonExportJobId,
        string requestedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
            UPDATE json_export_jobs
            SET
                status = 'Pending',
                lease_owner = NULL,
                lease_expires_at = NULL,
                last_error_redacted = NULL,
                updated_at = now()
            WHERE id = @JsonExportJobId
              AND status = 'RetryableFailed'
            RETURNING id;
            """;
        const string existsSql = """
            SELECT EXISTS (
                SELECT 1 FROM json_export_jobs WHERE id = @JsonExportJobId);
            """;
        const string auditSql = """
            INSERT INTO json_export_retry_audit (
                json_export_job_id, requested_by, reason)
            VALUES (@JsonExportJobId, @RequestedBy, @Reason);
            """;

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                updateSql,
                new
                {
                    JsonExportJobId = jsonExportJobId,
                    RequestedBy = requestedBy,
                    Reason = reason
                },
                transaction,
                cancellationToken: cancellationToken));
        if (!id.HasValue)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    existsSql,
                    new { JsonExportJobId = jsonExportJobId },
                    transaction,
                    cancellationToken: cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
            return new DeliveryActionResult(
                exists
                    ? DeliveryActionOutcome.Conflict
                    : DeliveryActionOutcome.NotFound);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            auditSql,
            new
            {
                JsonExportJobId = jsonExportJobId,
                RequestedBy = requestedBy,
                Reason = reason
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryActionResult(DeliveryActionOutcome.Accepted, id);
    }

    private static JsonExportHistoryResponse ToResponse(
        JsonExportHistoryRow row) => new(
            row.Id,
            row.FileJobId,
            row.ConfigVersionId,
            row.DestinationProfileId,
            row.ArtifactRevision,
            row.Status,
            row.RelativeDisplayPath,
            row.ObjectCount,
            row.ByteCount,
            row.Sha256,
            row.AttemptCount,
            row.LastErrorRedacted,
            AsUtcOffset(row.CreatedAt),
            AsUtcOffset(row.UpdatedAt),
            row.PublishedAt.HasValue
                ? AsUtcOffset(row.PublishedAt.Value)
                : null,
            row.CanRetry);

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record JsonExportHistoryRow(
        long Id,
        long FileJobId,
        long ConfigVersionId,
        string DestinationProfileId,
        int ArtifactRevision,
        string Status,
        string RelativeDisplayPath,
        int? ObjectCount,
        long? ByteCount,
        string? Sha256,
        int AttemptCount,
        string? LastErrorRedacted,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? PublishedAt,
        bool CanRetry);
}
