using System.Globalization;
using Dapper;
using MappingDemo.Api.Contracts.DeliveryJobs;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Messaging;
using Npgsql;

namespace MappingDemo.Api.Services;

public enum DeliveryActionOutcome
{
    Accepted,
    NotFound,
    Conflict
}

public sealed record DeliveryActionResult(
    DeliveryActionOutcome Outcome,
    long? DeliveryJobId = null);

public sealed class DeliveryJobService(NpgsqlDataSource dataSource)
{
    private const string SelectForFileJobSql = """
        SELECT
            id AS "Id",
            file_job_id AS "FileJobId",
            row_job_id AS "RowJobId",
            source_row_number AS "SourceRowNumber",
            config_version_id AS "ConfigVersionId",
            output_key AS "OutputKey",
            operation_kind AS "OperationKind",
            destination_profile_id AS "DestinationProfileId",
            contract_reference AS "ContractReference",
            schema_hash AS "SchemaHash",
            payload_hash AS "PayloadHash",
            status AS "Status",
            attempts AS "Attempts",
            next_attempt_at AS "NextAttemptAt",
            http_status AS "HttpStatus",
            correlation_id AS "CorrelationId",
            last_error_redacted AS "LastErrorRedacted",
            created_at AS "CreatedAt",
            updated_at AS "UpdatedAt",
            delivered_at AS "DeliveredAt",
            (status = 'RetryableFailed'
                AND attempts < @MaximumAttempts
                AND dispatch_scheduled_at IS NULL) AS "CanRetry"
        FROM delivery_jobs
        WHERE file_job_id = @FileJobId
        ORDER BY source_row_number, output_key, resend_sequence, id;
        """;

    private const string RetrySql = """
        UPDATE delivery_jobs
        SET
            next_attempt_at = now(),
            dispatch_scheduled_at = now(),
            updated_at = now()
        WHERE id = @DeliveryJobId
          AND status = @RetryableStatus
          AND attempts < @MaximumAttempts
          AND dispatch_scheduled_at IS NULL
        RETURNING id;
        """;

    private const string ExistsSql = """
        SELECT EXISTS (
            SELECT 1 FROM delivery_jobs WHERE id = @DeliveryJobId);
        """;

    private const string SelectDeliveredSql = """
        SELECT
            id AS "Id",
            file_job_id AS "FileJobId",
            row_job_id AS "RowJobId",
            source_row_id AS "SourceRowId",
            source_row_number AS "SourceRowNumber",
            config_version_id AS "ConfigVersionId",
            output_key AS "OutputKey",
            destination_profile_id AS "DestinationProfileId",
            contract_id AS "ContractId",
            contract_version AS "ContractVersion",
            contract_reference AS "ContractReference",
            schema_hash AS "SchemaHash",
            payload_ciphertext AS "PayloadCiphertext",
            payload_nonce AS "PayloadNonce",
            payload_tag AS "PayloadTag",
            payload_hash AS "PayloadHash",
            encryption_key_reference AS "EncryptionKeyReference",
            regexp_replace(
                idempotency_key,
                ':resend:[0-9]+$',
                '') AS "BaseIdempotencyKey",
            COALESCE(resend_of_delivery_job_id, id) AS "RootDeliveryJobId"
        FROM delivery_jobs
        WHERE id = @DeliveryJobId
          AND status = @DeliveredStatus
          AND payload_ciphertext IS NOT NULL
          AND payload_nonce IS NOT NULL
          AND payload_tag IS NOT NULL
          AND payload_expires_at > now()
        FOR UPDATE;
        """;

    private const string SelectExistingResendSql = """
        SELECT resend_delivery_job_id
        FROM delivery_resend_audit
        WHERE original_delivery_job_id = @OriginalDeliveryJobId
          AND request_id = @RequestId;
        """;

    private const string NextResendSequenceSql = """
        SELECT COALESCE(MAX(resend_sequence), 0) + 1
        FROM delivery_jobs
        WHERE id = @RootDeliveryJobId
           OR resend_of_delivery_job_id = @RootDeliveryJobId;
        """;

    private const string InsertResendSql = """
        INSERT INTO delivery_jobs (
            file_job_id,
            row_job_id,
            source_row_id,
            source_row_number,
            config_version_id,
            output_key,
            resend_sequence,
            operation_kind,
            destination_profile_id,
            contract_id,
            contract_version,
            contract_reference,
            schema_hash,
            payload_ciphertext,
            payload_nonce,
            payload_tag,
            payload_hash,
            encryption_key_reference,
            idempotency_key,
            status,
            next_attempt_at,
            dispatch_scheduled_at,
            payload_expires_at,
            audit_expires_at,
            resend_of_delivery_job_id)
        VALUES (
            @FileJobId,
            @RowJobId,
            @SourceRowId,
            @SourceRowNumber,
            @ConfigVersionId,
            @OutputKey,
            @ResendSequence,
            'Resend',
            @DestinationProfileId,
            @ContractId,
            @ContractVersion,
            @ContractReference,
            @SchemaHash,
            @PayloadCiphertext,
            @PayloadNonce,
            @PayloadTag,
            @PayloadHash,
            @EncryptionKeyReference,
            @IdempotencyKey,
            @PendingStatus,
            now(),
            now(),
            now() + interval '30 days',
            now() + interval '365 days',
            @RootDeliveryJobId)
        RETURNING id;
        """;

    private const string InsertAuditSql = """
        INSERT INTO delivery_resend_audit (
            original_delivery_job_id,
            resend_delivery_job_id,
            request_id,
            requested_by,
            reason)
        VALUES (
            @OriginalDeliveryJobId,
            @ResendDeliveryJobId,
            @RequestId,
            @RequestedBy,
            @Reason);
        """;

    public async Task<IReadOnlyList<DeliveryJobResponse>> GetForFileJobAsync(
        long fileJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DeliveryJobRow>(
            new CommandDefinition(
                SelectForFileJobSql,
                new
                {
                    FileJobId = fileJobId,
                    MaximumAttempts = DeliveryRetryPolicy.MaximumAttempts
                },
                cancellationToken: cancellationToken));
        return rows.Select(ToResponse).ToArray();
    }

    public async Task<DeliveryActionResult> RetryAsync(
        long deliveryJobId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                RetrySql,
                new
                {
                    DeliveryJobId = deliveryJobId,
                    RetryableStatus = DeliveryJobStatus.RetryableFailed,
                    MaximumAttempts = DeliveryRetryPolicy.MaximumAttempts
                },
                transaction,
                cancellationToken: cancellationToken));
        if (!id.HasValue)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    ExistsSql,
                    new { DeliveryJobId = deliveryJobId },
                    transaction,
                    cancellationToken: cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
            return new DeliveryActionResult(
                exists
                    ? DeliveryActionOutcome.Conflict
                    : DeliveryActionOutcome.NotFound);
        }

        await AddOutboxAsync(
            connection,
            transaction,
            id.Value,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryActionResult(
            DeliveryActionOutcome.Accepted,
            id.Value);
    }

    public async Task<DeliveryActionResult> ResendAsync(
        long deliveryJobId,
        string requestId,
        string requestedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var existingResendId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                SelectExistingResendSql,
                new
                {
                    OriginalDeliveryJobId = deliveryJobId,
                    RequestId = requestId
                },
                transaction,
                cancellationToken: cancellationToken));
        if (existingResendId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return new DeliveryActionResult(
                DeliveryActionOutcome.Accepted,
                existingResendId.Value);
        }

        var original = await connection.QuerySingleOrDefaultAsync<DeliveredJob>(
            new CommandDefinition(
                SelectDeliveredSql,
                new
                {
                    DeliveryJobId = deliveryJobId,
                    DeliveredStatus = DeliveryJobStatus.Delivered
                },
                transaction,
                cancellationToken: cancellationToken));
        if (original is null)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    ExistsSql,
                    new { DeliveryJobId = deliveryJobId },
                    transaction,
                    cancellationToken: cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
            return new DeliveryActionResult(
                exists
                    ? DeliveryActionOutcome.Conflict
                    : DeliveryActionOutcome.NotFound);
        }

        var sequence = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                NextResendSequenceSql,
                new { original.RootDeliveryJobId },
                transaction,
                cancellationToken: cancellationToken));
        var resendId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                InsertResendSql,
                new
                {
                    original.FileJobId,
                    original.RowJobId,
                    original.SourceRowId,
                    original.SourceRowNumber,
                    original.ConfigVersionId,
                    original.OutputKey,
                    ResendSequence = sequence,
                    original.DestinationProfileId,
                    original.ContractId,
                    original.ContractVersion,
                    original.ContractReference,
                    original.SchemaHash,
                    original.PayloadCiphertext,
                    original.PayloadNonce,
                    original.PayloadTag,
                    original.PayloadHash,
                    original.EncryptionKeyReference,
                    IdempotencyKey =
                        $"{original.BaseIdempotencyKey}:resend:{sequence}",
                    PendingStatus = DeliveryJobStatus.Pending,
                    original.RootDeliveryJobId
                },
                transaction,
                cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            InsertAuditSql,
            new
            {
                OriginalDeliveryJobId = original.Id,
                ResendDeliveryJobId = resendId,
                RequestId = requestId,
                RequestedBy = requestedBy,
                Reason = reason
            },
            transaction,
            cancellationToken: cancellationToken));
        await AddOutboxAsync(
            connection,
            transaction,
            resendId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeliveryActionResult(
            DeliveryActionOutcome.Accepted,
            resendId);
    }

    private static Task AddOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long deliveryJobId,
        CancellationToken cancellationToken) =>
        OutboxWriter.AddAsync(
            connection,
            transaction,
            Topics.ApiDelivery,
            deliveryJobId.ToString(CultureInfo.InvariantCulture),
            new DeliveryRequested(deliveryJobId),
            cancellationToken);

    private static DeliveryJobResponse ToResponse(DeliveryJobRow row) => new(
        row.Id,
        row.FileJobId,
        row.RowJobId,
        row.SourceRowNumber,
        row.ConfigVersionId,
        row.OutputKey,
        row.OperationKind,
        row.DestinationProfileId,
        row.ContractReference,
        row.SchemaHash,
        row.PayloadHash,
        row.Status,
        row.Attempts,
        AsUtcOffset(row.NextAttemptAt),
        row.HttpStatus,
        row.CorrelationId,
        row.LastErrorRedacted,
        AsUtcOffset(row.CreatedAt),
        AsUtcOffset(row.UpdatedAt),
        AsUtcOffset(row.DeliveredAt),
        row.CanRetry);

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? AsUtcOffset(DateTime? value) =>
        value.HasValue ? AsUtcOffset(value.Value) : null;

    private sealed class DeliveryJobRow
    {
        public long Id { get; init; }

        public long FileJobId { get; init; }

        public long RowJobId { get; init; }

        public int SourceRowNumber { get; init; }

        public long ConfigVersionId { get; init; }

        public string OutputKey { get; init; } = string.Empty;

        public string OperationKind { get; init; } = string.Empty;

        public string DestinationProfileId { get; init; } = string.Empty;

        public string ContractReference { get; init; } = string.Empty;

        public string SchemaHash { get; init; } = string.Empty;

        public string PayloadHash { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public DateTime? NextAttemptAt { get; init; }

        public int? HttpStatus { get; init; }

        public string? CorrelationId { get; init; }

        public string? LastErrorRedacted { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public DateTime? DeliveredAt { get; init; }

        public bool CanRetry { get; init; }
    }

    private sealed record DeliveredJob(
        long Id,
        long FileJobId,
        long RowJobId,
        long SourceRowId,
        int SourceRowNumber,
        long ConfigVersionId,
        string OutputKey,
        string DestinationProfileId,
        string ContractId,
        string ContractVersion,
        string ContractReference,
        string SchemaHash,
        byte[] PayloadCiphertext,
        byte[] PayloadNonce,
        byte[] PayloadTag,
        string PayloadHash,
        string EncryptionKeyReference,
        string BaseIdempotencyKey,
        long RootDeliveryJobId);
}
