using System.Diagnostics;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Delivery;
using Npgsql;

namespace MappingDemo.Worker.Delivery;

public sealed class DeliveryJobHandler(
    NpgsqlDataSource dataSource,
    IApiDestinationProfileCatalog profiles,
    DeliveryPayloadProtector payloadProtector,
    IDeliveryHttpAdapter httpAdapter,
    DeliveryControlReader control,
    ILogger<DeliveryJobHandler> logger)
{
    private const string ClaimSql = """
        UPDATE delivery_jobs
        SET
            status = @DeliveringStatus,
            attempts = attempts + 1,
            lease_owner = @LeaseOwner,
            lease_expires_at = now() + interval '2 minutes',
            dispatch_scheduled_at = NULL,
            updated_at = now()
        WHERE id = @DeliveryJobId
          AND attempts < @MaximumAttempts
          AND (
              (status IN (@PendingStatus, @RetryableStatus)
               AND (next_attempt_at IS NULL OR next_attempt_at <= now()))
              OR
              (status = @DeliveringStatus AND lease_expires_at < now()))
        RETURNING
            id AS "Id",
            destination_profile_id AS "DestinationProfileId",
            destination_profile_version_id AS "DestinationProfileVersionId",
            contract_version AS "ContractVersion",
            contract_reference AS "ContractReference",
            payload_ciphertext AS "PayloadCiphertext",
            payload_nonce AS "PayloadNonce",
            payload_tag AS "PayloadTag",
            payload_hash AS "PayloadHash",
            encryption_key_reference AS "EncryptionKeyReference",
            idempotency_key AS "IdempotencyKey",
            attempts AS "AttemptNo";
        """;

    private const string InsertAttemptSql = """
        INSERT INTO delivery_attempts (
            delivery_job_id,
            attempt_no,
            started_at,
            finished_at,
            outcome,
            http_status,
            duration_ms,
            correlation_id,
            error_redacted,
            payload_hash,
            idempotency_key)
        VALUES (
            @DeliveryJobId,
            @AttemptNo,
            @StartedAt,
            @FinishedAt,
            @Outcome,
            @HttpStatus,
            @DurationMs,
            @CorrelationId,
            @ErrorRedacted,
            @PayloadHash,
            @IdempotencyKey)
        ON CONFLICT (delivery_job_id, attempt_no) DO NOTHING;
        """;

    private const string CompleteSql = """
        UPDATE delivery_jobs
        SET
            status = @Status,
            next_attempt_at = @NextAttemptAt,
            http_status = @HttpStatus,
            correlation_id = @CorrelationId,
            last_error_redacted = @ErrorRedacted,
            lease_owner = NULL,
            lease_expires_at = NULL,
            delivered_at = CASE
                WHEN @Status = 'Delivered' THEN now()
                ELSE delivered_at
            END,
            terminal_at = CASE
                WHEN @Status IN ('Delivered', 'PermanentFailed') THEN now()
                ELSE NULL
            END,
            payload_expires_at = CASE
                WHEN @Status IN ('Delivered', 'PermanentFailed')
                    THEN now() + interval '30 days'
                ELSE payload_expires_at
            END,
            audit_expires_at = CASE
                WHEN @Status IN ('Delivered', 'PermanentFailed')
                    THEN now() + interval '365 days'
                ELSE audit_expires_at
            END,
            updated_at = now()
        WHERE id = @DeliveryJobId
          AND lease_owner = @LeaseOwner;
        """;

    public async Task HandleAsync(
        long deliveryJobId,
        CancellationToken cancellationToken)
    {
        if (await control.IsPausedAsync(cancellationToken))
        {
            await control.ReleaseScheduledJobAsync(
                deliveryJobId,
                cancellationToken);
            logger.LogInformation(
                "Delivery job {DeliveryJobId} remains queued because delivery is paused.",
                deliveryJobId);
            return;
        }

        var leaseOwner = Guid.NewGuid().ToString("N");
        var job = await ClaimAsync(
            deliveryJobId,
            leaseOwner,
            cancellationToken);
        if (job is null)
        {
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        DeliverySendResult send;
        var profileResult = job.DestinationProfileVersionId is { } versionId
            ? await profiles.ResolvePinnedAsync(versionId, cancellationToken)
            : await profiles.ResolveActiveAsync(
                job.DestinationProfileId,
                cancellationToken);
        if (!profileResult.IsSuccess)
        {
            send = new DeliverySendResult(
                null,
                true,
                null,
                null,
                AssertSafeError(profileResult.Diagnostics[0].Code));
        }
        else
        {
            try
            {
                var payload = await payloadProtector.UnprotectAsync(
                    job.PayloadCiphertext,
                    job.PayloadNonce,
                    job.PayloadTag,
                    job.EncryptionKeyReference,
                    cancellationToken);
                try
                {
                    send = await httpAdapter.SendAsync(
                        profileResult.Profile!,
                        new DeliverySendRequest(
                            job.Id,
                            job.ContractVersion,
                            job.ContractReference,
                            job.IdempotencyKey,
                            payload),
                        cancellationToken);
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations
                        .ZeroMemory(payload);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Delivery job {DeliveryJobId} could not prepare its request: {ErrorType}",
                    deliveryJobId,
                    exception.GetType().Name);
                send = new DeliverySendResult(
                    null,
                    true,
                    null,
                    null,
                    "Delivery request preparation failed.");
            }
        }

        stopwatch.Stop();
        var failureKind = DeliveryRetryPolicy.Classify(
            send.StatusCode,
            send.TransportFailure);
        var canRetry = failureKind == DeliveryFailureKind.Retryable &&
            job.AttemptNo < DeliveryRetryPolicy.MaximumAttempts;
        var status = failureKind == DeliveryFailureKind.Delivered
            ? DeliveryJobStatus.Delivered
            : canRetry
                ? DeliveryJobStatus.RetryableFailed
                : DeliveryJobStatus.PermanentFailed;
        DateTimeOffset? nextAttemptAt = canRetry
            ? DateTimeOffset.UtcNow + DeliveryRetryPolicy.Delay(
                job.AttemptNo,
                send.RetryAfter,
                0.5 + Random.Shared.NextDouble())
            : null;
        await PersistAttemptAsync(
            job,
            leaseOwner,
            startedAt,
            stopwatch.Elapsed,
            send,
            status,
            nextAttemptAt,
            cancellationToken);
    }

    private async Task<DeliveryJob?> ClaimAsync(
        long deliveryJobId,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DeliveryJob>(
            new CommandDefinition(
                ClaimSql,
                new
                {
                    DeliveryJobId = deliveryJobId,
                    LeaseOwner = leaseOwner,
                    MaximumAttempts = DeliveryRetryPolicy.MaximumAttempts,
                    PendingStatus = DeliveryJobStatus.Pending,
                    RetryableStatus = DeliveryJobStatus.RetryableFailed,
                    DeliveringStatus = DeliveryJobStatus.Delivering
                },
                cancellationToken: cancellationToken));
    }

    private async Task PersistAttemptAsync(
        DeliveryJob job,
        string leaseOwner,
        DateTimeOffset startedAt,
        TimeSpan duration,
        DeliverySendResult send,
        string status,
        DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var finishedAt = DateTimeOffset.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            InsertAttemptSql,
            new
            {
                DeliveryJobId = job.Id,
                job.AttemptNo,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Outcome = status,
                HttpStatus = send.StatusCode,
                DurationMs = Math.Max(0, (int)duration.TotalMilliseconds),
                send.CorrelationId,
                ErrorRedacted = Truncate(send.ErrorRedacted),
                job.PayloadHash,
                job.IdempotencyKey
            },
            transaction,
            cancellationToken: cancellationToken));
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            CompleteSql,
            new
            {
                DeliveryJobId = job.Id,
                LeaseOwner = leaseOwner,
                Status = status,
                NextAttemptAt = nextAttemptAt,
                HttpStatus = send.StatusCode,
                send.CorrelationId,
                ErrorRedacted = Truncate(send.ErrorRedacted)
            },
            transaction,
            cancellationToken: cancellationToken));
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Delivery job {job.Id} lost its lease before persistence.");
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Delivery job {DeliveryJobId} attempt {AttemptNo} completed as {DeliveryStatus} with HTTP {HttpStatus}",
            job.Id,
            job.AttemptNo,
            status,
            send.StatusCode);
    }

    private static string AssertSafeError(string code) =>
        code is "UnknownProfile" or "ProfileDisabled"
            ? code
            : "ProfileUnavailable";

    private static string? Truncate(string? value) =>
        value is null || value.Length <= 500 ? value : value[..500];

    private sealed record DeliveryJob(
        long Id,
        string DestinationProfileId,
        long? DestinationProfileVersionId,
        string ContractVersion,
        string ContractReference,
        byte[] PayloadCiphertext,
        byte[] PayloadNonce,
        byte[] PayloadTag,
        string PayloadHash,
        string EncryptionKeyReference,
        string IdempotencyKey,
        int AttemptNo);
}
