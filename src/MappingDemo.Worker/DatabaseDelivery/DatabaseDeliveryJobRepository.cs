using Dapper;
using MappingDemo.Shared.Delivery;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryJobRepository(NpgsqlDataSource dataSource)
{
    private const string ClaimSql = """
        UPDATE database_delivery_jobs
        SET status = @Delivering, attempts = attempts + 1,
            lease_owner = @LeaseOwner,
            lease_expires_at = now() + interval '2 minutes',
            dispatch_scheduled_at = NULL, updated_at = now()
        WHERE id = @JobId
          AND ((status IN (@Pending, @RetryableFailed)
                AND (next_attempt_at IS NULL OR next_attempt_at <= now()))
               OR (status = @Delivering AND lease_expires_at < now()
                   AND duplicate_policy ->> 'kind' = 'existingUniqueKey'))
        RETURNING id AS "Id", attempts AS "AttemptNo",
            destination_profile_id AS "ProfileId", provider AS "Provider",
            profile_configuration_hash AS "ProfileConfigurationHash",
            target_schema_snapshot::text AS "SchemaSnapshotJson",
            duplicate_policy::text AS "DuplicatePolicyJson",
            mapped_values_ciphertext AS "Ciphertext",
            mapped_values_nonce AS "Nonce", mapped_values_tag AS "Tag",
            mapped_values_hash AS "MappedValuesHash",
            encryption_key_reference AS "EncryptionKeyReference";
        """;

    private const string CompleteSql = """
        WITH updated AS (
            UPDATE database_delivery_jobs
            SET status = @Status, next_attempt_at = @NextAttemptAt,
                lease_owner = NULL, lease_expires_at = NULL,
                correlation_id = @CorrelationId,
                last_error_redacted = @ErrorRedacted,
                delivered_at = CASE WHEN @Status IN ('Delivered', 'AlreadyDelivered')
                    THEN now() ELSE delivered_at END,
                terminal_at = CASE WHEN @Terminal THEN now() ELSE NULL END,
                payload_expires_at = CASE WHEN @Terminal
                    THEN now() + interval '30 days' ELSE payload_expires_at END,
                audit_expires_at = CASE WHEN @Terminal
                    THEN now() + interval '365 days' ELSE audit_expires_at END,
                updated_at = now()
            WHERE id = @JobId AND lease_owner = @LeaseOwner
            RETURNING id, attempts, mapped_values_hash
        )
        INSERT INTO database_delivery_attempts (
            database_delivery_job_id, attempt_no, started_at, finished_at,
            outcome, duration_ms, correlation_id, error_code,
            error_redacted, target_schema_hash, mapped_values_hash)
        SELECT id, attempts, @StartedAt, now(), @Status, @DurationMs,
            @CorrelationId, @ErrorCode, @ErrorRedacted,
            @CurrentSchemaHash, mapped_values_hash
        FROM updated
        ON CONFLICT (database_delivery_job_id, attempt_no) DO NOTHING;
        """;

    private const string ReleaseClaimSql = """
        UPDATE database_delivery_jobs
        SET status = 'Pending', attempts = GREATEST(attempts - 1, 0),
            next_attempt_at = NULL, dispatch_scheduled_at = NULL,
            lease_owner = NULL, lease_expires_at = NULL,
            last_error_redacted = @Reason, updated_at = now()
        WHERE id = @JobId AND lease_owner = @LeaseOwner;
        """;

    private const string ReleaseScheduledSql = """
        UPDATE database_delivery_jobs
        SET dispatch_scheduled_at = NULL, updated_at = now()
        WHERE id = @JobId AND status IN ('Pending', 'RetryableFailed');
        """;

    public async Task<ClaimedDatabaseDeliveryJob?> ClaimAsync(
        long jobId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ClaimedDatabaseDeliveryJob>(
            new CommandDefinition(
                ClaimSql,
                new
                {
                    JobId = jobId,
                    LeaseOwner = leaseOwner,
                    DatabaseDeliveryJobStatus.Pending,
                    DatabaseDeliveryJobStatus.Delivering,
                    DatabaseDeliveryJobStatus.RetryableFailed
                },
                cancellationToken: cancellationToken));
    }

    public async Task CompleteAsync(
        ClaimedDatabaseDeliveryJob job,
        string leaseOwner,
        DateTimeOffset startedAt,
        TimeSpan duration,
        DatabaseWriteResult result,
        DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken)
    {
        var terminal = result.Status is
            DatabaseDeliveryJobStatus.Delivered or
            DatabaseDeliveryJobStatus.AlreadyDelivered or
            DatabaseDeliveryJobStatus.PermanentFailed or
            DatabaseDeliveryJobStatus.DeliveryConflict or
            DatabaseDeliveryJobStatus.BlockedSchemaDrift or
            DatabaseDeliveryJobStatus.UnknownOutcome;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            CompleteSql,
            new
            {
                JobId = job.Id,
                LeaseOwner = leaseOwner,
                StartedAt = startedAt,
                DurationMs = Math.Max(0, (int)duration.TotalMilliseconds),
                result.Status,
                NextAttemptAt = nextAttemptAt,
                CorrelationId = (string?)null,
                result.ErrorCode,
                result.ErrorRedacted,
                result.CurrentSchemaHash,
                Terminal = terminal
            },
            cancellationToken: cancellationToken));
        if (changed != 1)
        {
            throw new InvalidOperationException(
                $"Database delivery job {job.Id} lost its lease.");
        }
    }

    public async Task ReleaseClaimAsync(
        long jobId,
        string leaseOwner,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            ReleaseClaimSql,
            new { JobId = jobId, LeaseOwner = leaseOwner, Reason = reason },
            cancellationToken: cancellationToken));
    }

    public async Task ReleaseScheduledAsync(
        long jobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            ReleaseScheduledSql,
            new { JobId = jobId },
            cancellationToken: cancellationToken));
    }
}

public sealed record ClaimedDatabaseDeliveryJob(
    long Id,
    int AttemptNo,
    string ProfileId,
    string Provider,
    string ProfileConfigurationHash,
    string SchemaSnapshotJson,
    string DuplicatePolicyJson,
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    string MappedValuesHash,
    string EncryptionKeyReference);
