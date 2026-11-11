using Dapper;
using MappingDemo.Api.Contracts.DatabaseDestinations;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class DatabaseDeliveryOperationsService(NpgsqlDataSource dataSource)
{
    private const string SelectOperationsSql = """
        SELECT
            COUNT(*)::integer AS "Total",
            COUNT(*) FILTER (WHERE status = 'Pending')::integer AS "Pending",
            COUNT(*) FILTER (WHERE status = 'Delivering')::integer AS "Delivering",
            COUNT(*) FILTER (WHERE status = 'Delivered')::integer AS "Delivered",
            COUNT(*) FILTER (WHERE status = 'AlreadyDelivered')::integer
                AS "AlreadyDelivered",
            COUNT(*) FILTER (WHERE status = 'RetryableFailed')::integer
                AS "RetryableFailed",
            COUNT(*) FILTER (WHERE status = 'PermanentFailed')::integer
                AS "PermanentFailed",
            COUNT(*) FILTER (WHERE status = 'DeliveryConflict')::integer
                AS "DeliveryConflict",
            COUNT(*) FILTER (WHERE status = 'BlockedSchemaDrift')::integer
                AS "BlockedSchemaDrift",
            COUNT(*) FILTER (WHERE status = 'UnknownOutcome')::integer
                AS "UnknownOutcome",
            COALESCE(EXTRACT(EPOCH FROM (
                now() - MIN(created_at) FILTER (
                    WHERE status IN ('Pending', 'RetryableFailed')))), 0)
                ::double precision AS "OldestQueueAgeSeconds"
        FROM database_delivery_jobs;

        SELECT
            id AS "Id", file_job_id AS "FileJobId",
            config_version_id AS "ConfigVersionId",
            source_row_number AS "SourceRowNumber", output_key AS "OutputKey",
            destination_profile_id AS "DestinationProfileId",
            provider AS "Provider", target_schema AS "TargetSchema",
            target_table AS "TargetTable", target_schema_hash AS "TargetSchemaHash",
            mapped_values_hash AS "MappedValuesHash", status AS "Status",
            attempts AS "Attempts", correlation_id AS "CorrelationId",
            last_error_redacted AS "LastErrorRedacted",
            created_at AS "CreatedAt", updated_at AS "UpdatedAt",
            (duplicate_policy ->> 'kind' = 'existingUniqueKey')
                AS "HasExistingUniqueKey"
        FROM database_delivery_jobs
        ORDER BY updated_at DESC, id DESC
        LIMIT 100;

        SELECT DISTINCT ON (
            destination_profile_id, target_schema, target_table,
            configured_schema_hash)
            destination_profile_id AS "DestinationProfileId",
            target_schema AS "TargetSchema", target_table AS "TargetTable",
            configured_schema_hash AS "ConfiguredSchemaHash",
            current_schema_hash AS "CurrentSchemaHash",
            compatibility AS "Compatibility",
            diagnostics::text AS "DiagnosticsJson",
            checked_at AS "CheckedAt"
        FROM database_target_schema_checks
        ORDER BY destination_profile_id, target_schema, target_table,
            configured_schema_hash, checked_at DESC;
        """;

    public async Task<DatabaseDeliveryOperationsResponse> GetAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        using var rows = await connection.QueryMultipleAsync(
            new CommandDefinition(
                SelectOperationsSql,
                cancellationToken: cancellationToken));
        var metrics = await rows.ReadSingleAsync<DatabaseDeliveryMetricResponse>();
        var jobs = (await rows.ReadAsync<JobRow>())
            .Select(ToResponse)
            .ToArray();
        var checks = (await rows.ReadAsync<SchemaCheckRow>())
            .Select(row => new DatabaseSchemaCheckResponse(
                row.DestinationProfileId,
                row.TargetSchema,
                row.TargetTable,
                row.ConfiguredSchemaHash,
                row.CurrentSchemaHash,
                row.Compatibility,
                row.DiagnosticsJson,
                AsUtc(row.CheckedAt)))
            .ToArray();
        return new DatabaseDeliveryOperationsResponse(
            metrics,
            jobs,
            checks,
            BuildAlerts(metrics, checks));
    }

    public Task<DatabaseDeliveryResolutionResult> RetryAsync(
        long jobId,
        string requestedBy,
        string reason,
        string correlationId,
        bool acceptDuplicateRisk,
        CancellationToken cancellationToken) => ResolveAsync(
            jobId,
            "Retry",
            requestedBy,
            reason,
            correlationId,
            acceptDuplicateRisk,
            cancellationToken);

    public Task<DatabaseDeliveryResolutionResult> MarkDeliveredAsync(
        long jobId,
        string requestedBy,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) => ResolveAsync(
            jobId,
            "MarkDelivered",
            requestedBy,
            reason,
            correlationId,
            acceptDuplicateRisk: false,
            cancellationToken);

    public Task<DatabaseDeliveryResolutionResult> MarkFailedAsync(
        long jobId,
        string requestedBy,
        string reason,
        string correlationId,
        CancellationToken cancellationToken) => ResolveAsync(
            jobId,
            "MarkFailed",
            requestedBy,
            reason,
            correlationId,
            acceptDuplicateRisk: false,
            cancellationToken);

    private async Task<DatabaseDeliveryResolutionResult> ResolveAsync(
        long jobId,
        string action,
        string requestedBy,
        string reason,
        string correlationId,
        bool acceptDuplicateRisk,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT status AS "Status",
                (duplicate_policy ->> 'kind' = 'existingUniqueKey')
                    AS "HasExistingUniqueKey"
            FROM database_delivery_jobs
            WHERE id = @JobId
            FOR UPDATE;
            """;
        const string updateSql = """
            UPDATE database_delivery_jobs
            SET status = @NextStatus,
                next_attempt_at = CASE WHEN @Action = 'Retry' THEN now() ELSE NULL END,
                dispatch_scheduled_at = NULL,
                lease_owner = NULL,
                lease_expires_at = NULL,
                correlation_id = @CorrelationId,
                last_error_redacted = @ResolutionMessage,
                delivered_at = CASE WHEN @Action = 'MarkDelivered'
                    THEN now() ELSE delivered_at END,
                terminal_at = CASE WHEN @Action = 'Retry'
                    THEN NULL ELSE now() END,
                payload_expires_at = CASE WHEN @Action = 'Retry'
                    THEN NULL ELSE COALESCE(payload_expires_at, now() + interval '30 days') END,
                audit_expires_at = CASE WHEN @Action = 'Retry'
                    THEN NULL ELSE COALESCE(audit_expires_at, now() + interval '365 days') END,
                updated_at = now()
            WHERE id = @JobId;

            INSERT INTO database_delivery_resolution_audit (
                database_delivery_job_id, action, requested_by, reason,
                correlation_id)
            VALUES (@JobId, @Action, @RequestedBy, @Reason, @CorrelationId);
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var job = await connection.QuerySingleOrDefaultAsync<ResolutionRow>(
            new CommandDefinition(
                selectSql,
                new { JobId = jobId },
                transaction,
                cancellationToken: cancellationToken));
        if (job is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DatabaseDeliveryResolutionResult.NotFound;
        }

        if (!IsAllowed(action, job.Status))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DatabaseDeliveryResolutionResult.StateConflict;
        }
        if (action == "Retry" &&
            job.Status == "UnknownOutcome" &&
            !job.HasExistingUniqueKey &&
            !acceptDuplicateRisk)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DatabaseDeliveryResolutionResult
                .DuplicateRiskAcceptanceRequired;
        }

        var nextStatus = action switch
        {
            "Retry" => "Pending",
            "MarkDelivered" => "Delivered",
            _ => "PermanentFailed"
        };
        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new
            {
                JobId = jobId,
                Action = action,
                RequestedBy = requestedBy,
                Reason = reason,
                CorrelationId = correlationId,
                NextStatus = nextStatus,
                ResolutionMessage = $"Manually resolved by {requestedBy}: {action}."
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return DatabaseDeliveryResolutionResult.Updated;
    }

    private static bool IsAllowed(string action, string status) => action switch
    {
        "Retry" => status is "UnknownOutcome" or "PermanentFailed" or
            "BlockedSchemaDrift",
        "MarkDelivered" => status == "UnknownOutcome",
        "MarkFailed" => status is "UnknownOutcome" or "DeliveryConflict",
        _ => false
    };

    private static DatabaseDeliveryJobResponse ToResponse(JobRow row)
    {
        var unresolved = row.Status is "UnknownOutcome" or "DeliveryConflict";
        return new DatabaseDeliveryJobResponse(
            row.Id,
            row.FileJobId,
            row.ConfigVersionId,
            row.SourceRowNumber,
            row.OutputKey,
            row.DestinationProfileId,
            row.Provider,
            row.TargetSchema,
            row.TargetTable,
            row.TargetSchemaHash,
            row.MappedValuesHash,
            row.Status,
            row.Attempts,
            row.CorrelationId,
            row.LastErrorRedacted,
            AsUtc(row.CreatedAt),
            AsUtc(row.UpdatedAt),
            row.HasExistingUniqueKey,
            row.Status is "UnknownOutcome" or "PermanentFailed" or
                "BlockedSchemaDrift",
            row.Status == "UnknownOutcome",
            unresolved,
            row.Status == "UnknownOutcome" && !row.HasExistingUniqueKey);
    }

    private static IReadOnlyList<DatabaseDeliveryAlertResponse> BuildAlerts(
        DatabaseDeliveryMetricResponse metrics,
        IReadOnlyList<DatabaseSchemaCheckResponse> checks)
    {
        var alerts = new List<DatabaseDeliveryAlertResponse>();
        if (metrics.UnknownOutcome > 0)
        {
            alerts.Add(new("Critical", "DatabaseUnknownOutcome",
                "A database delivery outcome is unknown; automatic retry is disabled."));
        }
        if (metrics.DeliveryConflict > 0)
        {
            alerts.Add(new("Critical", "DatabaseDeliveryConflict",
                "A unique key matched a row with different mapped values."));
        }
        if (checks.Any(check => check.Compatibility == "BreakingChange"))
        {
            alerts.Add(new("Critical", "DatabaseBreakingSchemaDrift",
                "A configured target table has a breaking schema change."));
        }
        if (checks.Any(check => check.Compatibility == "Unavailable"))
        {
            alerts.Add(new("Warning", "DatabaseSchemaUnavailable",
                "A configured target schema could not be inspected."));
        }
        if (metrics.OldestQueueAgeSeconds > 600)
        {
            alerts.Add(new("Warning", "DatabaseDeliveryQueueAge",
                "The oldest queued database delivery is above 10 minutes."));
        }
        return alerts;
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record JobRow(
        long Id,
        long FileJobId,
        long ConfigVersionId,
        int SourceRowNumber,
        string OutputKey,
        string DestinationProfileId,
        string Provider,
        string TargetSchema,
        string TargetTable,
        string TargetSchemaHash,
        string MappedValuesHash,
        string Status,
        int Attempts,
        string? CorrelationId,
        string? LastErrorRedacted,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool HasExistingUniqueKey);

    private sealed record SchemaCheckRow(
        string DestinationProfileId,
        string TargetSchema,
        string TargetTable,
        string ConfiguredSchemaHash,
        string? CurrentSchemaHash,
        string Compatibility,
        string DiagnosticsJson,
        DateTime CheckedAt);

    private sealed record ResolutionRow(
        string Status,
        bool HasExistingUniqueKey);
}
