using Dapper;
using MappingDemo.Shared.Contracts;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportCleanupService(
    NpgsqlDataSource dataSource,
    FileDestinationProfileCatalog profiles,
    IJsonExportArtifactCleaner artifactCleaner,
    ILogger<JsonExportCleanupService> logger) : BackgroundService
{
    private const string SelectExpiredSql = """
        SELECT
            jobs.id AS "Id",
            versions.config_id AS "ConfigId",
            jobs.config_version_id AS "ConfigVersionId",
            jobs.destination_profile_id AS "DestinationProfileId",
            jobs.relative_final_path AS "RelativeFinalPath",
            (jobs.payload_expires_at IS NOT NULL
                AND jobs.payload_expires_at <= now()
                AND jobs.payload_purged_at IS NULL) AS "PurgePayload",
            (jobs.artifact_expires_at IS NOT NULL
                AND jobs.artifact_expires_at <= now()
                AND jobs.artifact_deleted_at IS NULL) AS "DeleteArtifact"
        FROM json_export_jobs AS jobs
        INNER JOIN mapping_config_versions AS versions
            ON versions.id = jobs.config_version_id
        WHERE NOT jobs.legal_hold
          AND (
              (jobs.payload_expires_at IS NOT NULL
               AND jobs.payload_expires_at <= now()
               AND jobs.payload_purged_at IS NULL)
              OR
              (jobs.artifact_expires_at IS NOT NULL
               AND jobs.artifact_expires_at <= now()
               AND jobs.artifact_deleted_at IS NULL))
        ORDER BY jobs.updated_at, jobs.id
        LIMIT 100;
        """;

    public async Task<int> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var jobs = (await connection.QueryAsync<ExpiredExport>(
            new CommandDefinition(
                SelectExpiredSql,
                cancellationToken: cancellationToken))).ToArray();

        foreach (var job in jobs)
        {
            if (job.PurgePayload)
            {
                await PurgePayloadAsync(job.Id, cancellationToken);
            }
            if (job.DeleteArtifact)
            {
                await DeleteArtifactAsync(job, cancellationToken);
            }
        }
        return jobs.Length;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "JSON export cleanup scan failed after {FailureType}",
                    exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgePayloadAsync(
        long jobId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE json_export_items
            SET
                payload_ciphertext = NULL,
                payload_nonce = NULL,
                payload_tag = NULL,
                encryption_key_reference = NULL,
                purged_at = now(),
                updated_at = now()
            WHERE json_export_job_id = @JobId
              AND purged_at IS NULL;
            UPDATE json_export_jobs
            SET payload_purged_at = now(), updated_at = now()
            WHERE id = @JobId
              AND NOT legal_hold;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { JobId = jobId },
            cancellationToken: cancellationToken));
    }

    private async Task DeleteArtifactAsync(
        ExpiredExport job,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = profiles.ResolvePinned(job.DestinationProfileId)
                ?? throw new InvalidOperationException(
                    "The pinned File destination profile is unavailable.");
            await artifactCleaner.DeleteAsync(
                profile,
                job.RelativeFinalPath,
                cancellationToken);
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE json_export_jobs
                SET artifact_deleted_at = now(), updated_at = now()
                WHERE id = @Id AND NOT legal_hold;
                """,
                new { job.Id },
                cancellationToken: cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(job, cancellationToken);
            logger.LogWarning(
                "JSON artifact cleanup failed for export job {JsonExportJobId} after {FailureType}",
                job.Id,
                exception.GetType().Name);
        }
    }

    private async Task RecordFailureAsync(
        ExpiredExport job,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO json_export_operation_events (
                config_id,
                config_version_id,
                destination_profile_id,
                event_type,
                details_redacted)
            VALUES (
                @ConfigId,
                @ConfigVersionId,
                @DestinationProfileId,
                'CleanupFailure',
                'Artifact cleanup failed; inspect storage permissions and health.');
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            job,
            cancellationToken: cancellationToken));
    }

    private sealed record ExpiredExport(
        long Id,
        long ConfigId,
        long ConfigVersionId,
        string DestinationProfileId,
        string RelativeFinalPath,
        bool PurgePayload,
        bool DeleteArtifact);
}
