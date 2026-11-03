using System.Data;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Worker.Delivery;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonFileFinalizer(
    NpgsqlDataSource dataSource,
    FileDestinationProfileCatalog profiles,
    JsonExportPayloadProtector payloadProtector,
    IJsonExportPublisher publisher,
    ILogger<JsonFileFinalizer> logger,
    JsonExportControlReader? control = null)
{
    public const int MaxObjectBytes = JsonArtifactLimits.MaxObjectBytes;
    public const int MaxObjectCount =
        JsonArtifactLimits.MaxObjectsPerArtifact;
    public const long MaxArtifactBytes = JsonArtifactLimits.MaxArtifactBytes;

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private readonly string _leaseOwner =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    private const string LockJobSql = """
        SELECT
            json_export_jobs.id AS "Id",
            json_export_jobs.file_job_id AS "FileJobId",
            json_export_jobs.config_version_id AS "ConfigVersionId",
            json_export_jobs.destination_profile_id AS "DestinationProfileId",
            json_export_jobs.status AS "Status",
            (
                json_export_jobs.status IN ('Pending', 'RetryableFailed')
                OR (
                    json_export_jobs.status = 'Finalizing'
                    AND (
                        json_export_jobs.lease_expires_at IS NULL
                        OR json_export_jobs.lease_expires_at < now())))
                AS "CanClaim",
            json_export_jobs.relative_temp_path AS "RelativeTempPath",
            json_export_jobs.relative_final_path AS "RelativeFinalPath",
            mapping_config_versions.destination_definition ->> 'formatting'
                AS "Formatting"
        FROM json_export_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id =
               json_export_jobs.config_version_id
        WHERE json_export_jobs.id = @JsonExportJobId
        FOR UPDATE OF json_export_jobs;
        """;

    private const string SelectRowSummarySql = """
        WITH latest_row_jobs AS (
            SELECT DISTINCT ON (source_row_id)
                id,
                config_version_id,
                status
            FROM row_jobs
            WHERE file_job_id = @FileJobId
            ORDER BY source_row_id, id DESC
        )
        SELECT
            file_jobs.import_status = 'Imported' AS "Imported",
            file_jobs.total_rows IS NOT NULL
                AND file_jobs.total_rows = COUNT(latest_row_jobs.id)
                AS "RowCountMatches",
            COALESCE(BOOL_AND(
                latest_row_jobs.config_version_id = @ConfigVersionId), true)
                AS "ConfigMatches",
            COALESCE(BOOL_AND(
                latest_row_jobs.status IN (
                    'Done', 'Filtered', 'Invalid', 'Failed')), true)
                AS "AllTerminal",
            COUNT(*) FILTER (
                WHERE latest_row_jobs.status = 'Invalid')::integer
                AS "InvalidCount",
            COUNT(*) FILTER (
                WHERE latest_row_jobs.status = 'Failed')::integer
                AS "FailedCount"
        FROM file_jobs
        LEFT JOIN latest_row_jobs ON true
        WHERE file_jobs.id = @FileJobId
        GROUP BY
            file_jobs.import_status,
            file_jobs.total_rows;
        """;

    private const string MarkBlockedSql = """
        UPDATE json_export_jobs
        SET
            status = 'Blocked',
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted = @LastErrorRedacted,
            updated_at = now(),
            terminal_at = now(),
            payload_expires_at = now() + interval '30 days',
            audit_expires_at = now() + interval '365 days'
        WHERE id = @JsonExportJobId
          AND status IN ('Pending', 'RetryableFailed', 'Finalizing');
        """;

    private const string ClaimSql = """
        UPDATE json_export_jobs
        SET
            status = 'Finalizing',
            lease_owner = @LeaseOwner,
            lease_expires_at = now() + @LeaseDuration,
            attempt_count = attempt_count + 1,
            updated_at = now()
        WHERE id = @JsonExportJobId
          AND (
              status IN ('Pending', 'RetryableFailed')
              OR (
                  status = 'Finalizing'
                  AND (
                      lease_expires_at IS NULL
                      OR lease_expires_at < now())));
        """;

    private const string SelectItemsSql = """
        SELECT
            payload_ciphertext,
            payload_nonce,
            payload_tag,
            encryption_key_reference,
            payload_sha256
        FROM json_export_items
        WHERE json_export_job_id = @JsonExportJobId
          AND purged_at IS NULL
        ORDER BY row_number, output_ordinal, output_key, id;
        """;

    private const string MarkPublishedSql = """
        UPDATE json_export_jobs
        SET
            status = 'Published',
            object_count = @ObjectCount,
            byte_count = @ByteCount,
            sha256 = @Sha256,
            lease_owner = NULL,
            lease_expires_at = NULL,
            updated_at = now(),
            terminal_at = now(),
            published_at = now(),
            payload_expires_at = now() + interval '30 days',
            artifact_expires_at = now() + interval '30 days',
            audit_expires_at = now() + interval '365 days'
        WHERE id = @JsonExportJobId
          AND status = 'Finalizing'
          AND lease_owner = @LeaseOwner;
        """;

    private const string MarkRetryableFailedSql = """
        UPDATE json_export_jobs
        SET
            status = 'RetryableFailed',
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted = @LastErrorRedacted,
            updated_at = now()
        WHERE id = @JsonExportJobId
          AND status = 'Finalizing'
          AND lease_owner = @LeaseOwner;
        """;

    private const string MarkOwnedBlockedSql = """
        UPDATE json_export_jobs
        SET
            status = 'Blocked',
            lease_owner = NULL,
            lease_expires_at = NULL,
            last_error_redacted = @LastErrorRedacted,
            updated_at = now(),
            terminal_at = now(),
            payload_expires_at = now() + interval '30 days',
            audit_expires_at = now() + interval '365 days'
        WHERE id = @JsonExportJobId
          AND status = 'Finalizing'
          AND lease_owner = @LeaseOwner;
        """;

    public async Task HandleAsync(
        long jsonExportJobId,
        CancellationToken cancellationToken = default)
    {
        if (control is not null &&
            await control.IsFinalizerPausedAsync(cancellationToken))
        {
            return;
        }

        var job = await TryClaimAsync(jsonExportJobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        try
        {
            var profile = profiles.ResolvePinned(job.DestinationProfileId)
                ?? throw new InvalidOperationException(
                    "The pinned File destination profile is unavailable.");

            JsonArtifactMetadata? metadata = null;
            await publisher.PublishAsync(
                profile,
                job.RelativeTempPath,
                job.RelativeFinalPath,
                async (stream, token) =>
                {
                    metadata = await WriteDocumentAsync(
                        jsonExportJobId,
                        job.Formatting,
                        stream,
                        token);
                },
                cancellationToken);

            await MarkPublishedAsync(
                jsonExportJobId,
                metadata ?? throw new InvalidOperationException(
                    "JSON artifact metadata was not produced."),
                cancellationToken);
            logger.LogInformation(
                "Published JSON export job {JsonExportJobId} with {ObjectCount} objects and {ByteCount} bytes",
                jsonExportJobId,
                metadata.ObjectCount,
                metadata.ByteCount);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonExportCollisionException exception)
        {
            await MarkOwnedBlockedAsync(
                jsonExportJobId,
                exception.Message,
                cancellationToken);
            await RecordOperationEventAsync(
                job,
                "HashConflict",
                "Final artifact identity contains different bytes.",
                cancellationToken);
            logger.LogWarning(
                "Blocked JSON export job {JsonExportJobId} because the final path contains different bytes",
                jsonExportJobId);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or CryptographicException
                or JsonException
                or ArgumentException)
        {
            await MarkOwnedBlockedAsync(
                jsonExportJobId,
                RedactDeterministicFailure(exception),
                cancellationToken);
            logger.LogWarning(
                "Blocked JSON export job {JsonExportJobId} after deterministic validation failure {FailureType}",
                jsonExportJobId,
                exception.GetType().Name);
        }
        catch (Exception exception)
        {
            const string retryMessage =
                "JSON artifact publication failed and will be retried.";
            await MarkRetryableFailedAsync(
                jsonExportJobId,
                retryMessage,
                cancellationToken);
            logger.LogWarning(
                "JSON export job {JsonExportJobId} will be retried after {FailureType}",
                jsonExportJobId,
                exception.GetType().Name);
        }
    }

    private static string RedactDeterministicFailure(Exception exception) =>
        exception switch
        {
            CryptographicException =>
                "A staged JSON export payload could not be decrypted or verified.",
            JsonException =>
                "A staged JSON export object is not valid JSON.",
            ArgumentException =>
                "JSON export validation failed.",
            InvalidOperationException when
                exception.Message.StartsWith(
                    "A staged JSON export ",
                    StringComparison.Ordinal) ||
                exception.Message.StartsWith(
                    "JSON export ",
                    StringComparison.Ordinal) => exception.Message,
            _ => "JSON export validation failed."
        };

    private async Task RecordOperationEventAsync(
        JsonExportJob job,
        string eventType,
        string detailsRedacted,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO json_export_operation_events (
                config_id,
                config_version_id,
                destination_profile_id,
                event_type,
                details_redacted)
            SELECT
                config_id,
                @ConfigVersionId,
                @DestinationProfileId,
                @EventType,
                @DetailsRedacted
            FROM mapping_config_versions
            WHERE id = @ConfigVersionId;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                job.ConfigVersionId,
                job.DestinationProfileId,
                EventType = eventType,
                DetailsRedacted = detailsRedacted
            },
            cancellationToken: cancellationToken));
    }

    private async Task<JsonExportJob?> TryClaimAsync(
        long jsonExportJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var job = await connection.QuerySingleOrDefaultAsync<JsonExportJob>(
            new CommandDefinition(
                LockJobSql,
                new { JsonExportJobId = jsonExportJobId },
                transaction,
                cancellationToken: cancellationToken));
        if (job is null || !job.CanClaim)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var summary = await connection.QuerySingleAsync<RowJobSummary>(
            new CommandDefinition(
                SelectRowSummarySql,
                new
                {
                    job.FileJobId,
                    job.ConfigVersionId
                },
                transaction,
                cancellationToken: cancellationToken));
        if (!summary.IsComplete)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        if (summary.InvalidCount > 0 || summary.FailedCount > 0)
        {
            var lastError =
                $"Latest row jobs contain {summary.InvalidCount} Invalid and {summary.FailedCount} Failed outcomes.";
            await connection.ExecuteAsync(new CommandDefinition(
                MarkBlockedSql,
                new
                {
                    JsonExportJobId = jsonExportJobId,
                    LastErrorRedacted = lastError
                },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "Blocked JSON export job {JsonExportJobId}: {Reason}",
                jsonExportJobId,
                lastError);
            return null;
        }

        var claimed = await connection.ExecuteAsync(new CommandDefinition(
            ClaimSql,
            new
            {
                JsonExportJobId = jsonExportJobId,
                LeaseOwner = _leaseOwner,
                LeaseDuration
            },
            transaction,
            cancellationToken: cancellationToken));
        if (claimed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    private async Task<JsonArtifactMetadata> WriteDocumentAsync(
        long jsonExportJobId,
        string formatting,
        Stream destination,
        CancellationToken cancellationToken)
    {
        return await JsonArtifactSerializer.WriteAsync(
            destination,
            formatting,
            ReadObjectsAsync(jsonExportJobId, cancellationToken),
            cancellationToken);
    }

    private async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadObjectsAsync(
        long jsonExportJobId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SelectItemsSql, connection);
        command.Parameters.AddWithValue("JsonExportJobId", jsonExportJobId);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ciphertext = reader.GetFieldValue<byte[]>(0);
            var nonce = reader.GetFieldValue<byte[]>(1);
            var tag = reader.GetFieldValue<byte[]>(2);
            var keyReference = reader.GetString(3);
            var expectedPayloadHash = reader.GetString(4);
            var plaintext = await payloadProtector.UnprotectAsync(
                ciphertext,
                nonce,
                tag,
                keyReference,
                cancellationToken);
            try
            {
                var actualPayloadHash = "sha256:" + Convert.ToHexString(
                    SHA256.HashData(plaintext)).ToLowerInvariant();
                if (!string.Equals(
                        actualPayloadHash,
                        expectedPayloadHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A staged JSON export payload failed its integrity check.");
                }

                yield return plaintext;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

    }

    private async Task MarkPublishedAsync(
        long jsonExportJobId,
        JsonArtifactMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            MarkPublishedSql,
            new
            {
                JsonExportJobId = jsonExportJobId,
                LeaseOwner = _leaseOwner,
                metadata.ObjectCount,
                metadata.ByteCount,
                metadata.Sha256
            },
            cancellationToken: cancellationToken));
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"JSON export job {jsonExportJobId} lost its lease before publication was recorded.");
        }
    }

    private async Task MarkRetryableFailedAsync(
        long jsonExportJobId,
        string lastErrorRedacted,
        CancellationToken cancellationToken)
    {
        await UpdateOwnedStateAsync(
            MarkRetryableFailedSql,
            jsonExportJobId,
            lastErrorRedacted,
            cancellationToken);
    }

    private async Task MarkOwnedBlockedAsync(
        long jsonExportJobId,
        string lastErrorRedacted,
        CancellationToken cancellationToken)
    {
        await UpdateOwnedStateAsync(
            MarkOwnedBlockedSql,
            jsonExportJobId,
            lastErrorRedacted,
            cancellationToken);
    }

    private async Task UpdateOwnedStateAsync(
        string sql,
        long jsonExportJobId,
        string lastErrorRedacted,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                JsonExportJobId = jsonExportJobId,
                LeaseOwner = _leaseOwner,
                LastErrorRedacted = lastErrorRedacted
            },
            cancellationToken: cancellationToken));
    }

    private sealed record JsonExportJob(
        long Id,
        long FileJobId,
        long ConfigVersionId,
        string DestinationProfileId,
        string Status,
        bool CanClaim,
        string RelativeTempPath,
        string RelativeFinalPath,
        string Formatting);

    private sealed record RowJobSummary(
        bool Imported,
        bool RowCountMatches,
        bool ConfigMatches,
        bool AllTerminal,
        int InvalidCount,
        int FailedCount)
    {
        public bool IsComplete =>
            Imported && RowCountMatches && ConfigMatches && AllTerminal;
    }

}
