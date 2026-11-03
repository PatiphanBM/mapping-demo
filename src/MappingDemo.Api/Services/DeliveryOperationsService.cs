using Dapper;
using MappingDemo.Api.Contracts.DeliveryJobs;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed record ApiContractActivationGate(bool Allowed, string? Reason);

public sealed class DeliveryOperationsService(NpgsqlDataSource dataSource)
{
    private const string SelectControlSql = """
        SELECT
            delivery_paused AS "DeliveryPaused",
            api_contract_activation_enabled AS "ApiContractActivationEnabled",
            json_file_activation_enabled AS "JsonFileActivationEnabled",
            json_file_finalizer_paused AS "JsonFileFinalizerPaused",
            updated_at AS "UpdatedAt",
            updated_by AS "UpdatedBy",
            reason AS "Reason"
        FROM delivery_runtime_control
        WHERE singleton = true;
        """;

    private const string SelectRolloutsSql = """
        SELECT
            rollouts.config_id AS "ConfigId",
            configs.name AS "ConfigName",
            rollouts.stage AS "Stage",
            rollouts.enabled AS "Enabled",
            rollouts.approved_by AS "ApprovedBy",
            rollouts.reason AS "Reason",
            rollouts.updated_at AS "UpdatedAt"
        FROM api_delivery_rollouts AS rollouts
        INNER JOIN mapping_configs AS configs ON configs.id = rollouts.config_id
        ORDER BY configs.name, rollouts.config_id;
        """;

    private const string SelectMetricGroupsSql = """
        SELECT
            config_version_id AS "ConfigVersionId",
            contract_reference AS "ContractReference",
            COUNT(*)::integer AS "Total",
            COUNT(*) FILTER (WHERE status = 'Pending')::integer AS "Pending",
            COUNT(*) FILTER (WHERE status = 'Delivering')::integer AS "Delivering",
            COUNT(*) FILTER (WHERE status = 'Delivered')::integer AS "Delivered",
            COUNT(*) FILTER (WHERE status = 'RetryableFailed')::integer
                AS "RetryableFailed",
            COUNT(*) FILTER (WHERE status = 'PermanentFailed')::integer
                AS "PermanentFailed"
        FROM delivery_jobs
        GROUP BY config_version_id, contract_reference
        ORDER BY config_version_id, contract_reference;
        """;

    private const string SelectAggregateMetricsSql = """
        SELECT
            COUNT(*)::integer AS "Total",
            COUNT(*) FILTER (WHERE status = 'Delivered')::integer AS "Delivered",
            COUNT(*) FILTER (WHERE status = 'RetryableFailed')::integer
                AS "RetryableFailed",
            COUNT(*) FILTER (WHERE status = 'PermanentFailed')::integer
                AS "PermanentFailed",
            COALESCE(EXTRACT(EPOCH FROM (
                now() - MIN(created_at) FILTER (
                    WHERE status IN ('Pending', 'RetryableFailed')))), 0)::double precision
                AS "OldestQueueAgeSeconds",
            COALESCE(SUM(GREATEST(attempts - 1, 0)), 0)::integer AS "RetryCount"
        FROM delivery_jobs;

        SELECT
            COALESCE(percentile_cont(0.50) WITHIN GROUP (ORDER BY duration_ms), 0)
                AS "P50",
            COALESCE(percentile_cont(0.95) WITHIN GROUP (ORDER BY duration_ms), 0)
                AS "P95",
            COALESCE(percentile_cont(0.99) WITHIN GROUP (ORDER BY duration_ms), 0)
                AS "P99",
            COUNT(*) FILTER (WHERE http_status = 409)::integer
                AS "DuplicateConflictCount"
        FROM delivery_attempts;

        SELECT COUNT(*)::integer
        FROM row_errors
        INNER JOIN row_jobs ON row_jobs.id = row_errors.row_job_id
        INNER JOIN mapping_config_versions AS versions
            ON versions.id = row_jobs.config_version_id
        WHERE versions.destination_definition->>'kind' = 'apiContract'
          AND row_errors.expression_path LIKE 'delivery.%';

        SELECT percentile_cont(0.95) WITHIN GROUP (
            ORDER BY EXTRACT(EPOCH FROM (delivery_jobs.delivered_at - file_jobs.created_at)))
        FROM delivery_jobs
        INNER JOIN file_jobs ON file_jobs.id = delivery_jobs.file_job_id
        WHERE delivery_jobs.status = 'Delivered';
        """;

    private const string SelectJsonExportMetricsSql = """
        WITH event_counts AS (
            SELECT
                config_id,
                config_version_id,
                destination_profile_id,
                COUNT(*) FILTER (WHERE event_type = 'HashConflict')::integer
                    AS hash_conflict_count,
                COUNT(*) FILTER (WHERE event_type = 'CleanupFailure')::integer
                    AS cleanup_failure_count
            FROM json_export_operation_events
            GROUP BY config_id, config_version_id, destination_profile_id
        )
        SELECT
            versions.config_id AS "ConfigId",
            jobs.config_version_id AS "ConfigVersionId",
            jobs.destination_profile_id AS "DestinationProfileId",
            COUNT(*)::integer AS "Total",
            COUNT(*) FILTER (WHERE jobs.status = 'Pending')::integer AS "Pending",
            COUNT(*) FILTER (WHERE jobs.status = 'Finalizing')::integer AS "Finalizing",
            COUNT(*) FILTER (WHERE jobs.status = 'Published')::integer AS "Published",
            COUNT(*) FILTER (WHERE jobs.status = 'Blocked')::integer AS "Blocked",
            COUNT(*) FILTER (WHERE jobs.status = 'RetryableFailed')::integer
                AS "RetryableFailed",
            COALESCE(SUM(jobs.object_count), 0)::bigint AS "ObjectCount",
            COALESCE(SUM(jobs.byte_count), 0)::bigint AS "ByteCount",
            COALESCE(SUM(GREATEST(jobs.attempt_count - 1, 0)), 0)::integer
                AS "RetryCount",
            COALESCE(events.hash_conflict_count, 0) AS "HashConflictCount",
            COALESCE(events.cleanup_failure_count, 0) AS "CleanupFailureCount",
            COALESCE(EXTRACT(EPOCH FROM (
                now() - MIN(jobs.created_at) FILTER (
                    WHERE jobs.status IN (
                        'Pending', 'Finalizing', 'RetryableFailed')))), 0)::double precision
                AS "OldestQueueAgeSeconds",
            COALESCE(percentile_cont(0.95) WITHIN GROUP (
                ORDER BY EXTRACT(EPOCH FROM (
                    jobs.published_at - jobs.created_at)) * 1000)
                FILTER (WHERE jobs.status = 'Published'), 0)::double precision
                AS "P95FinalizeLatencyMilliseconds"
        FROM json_export_jobs AS jobs
        INNER JOIN mapping_config_versions AS versions
            ON versions.id = jobs.config_version_id
        LEFT JOIN event_counts AS events
            ON events.config_id = versions.config_id
           AND events.config_version_id = jobs.config_version_id
           AND events.destination_profile_id = jobs.destination_profile_id
        GROUP BY
            versions.config_id,
            jobs.config_version_id,
            jobs.destination_profile_id,
            events.hash_conflict_count,
            events.cleanup_failure_count
        ORDER BY versions.config_id, jobs.config_version_id, jobs.destination_profile_id;
        """;

    public async Task<DeliveryOperationsResponse> GetAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var controlRow = await connection.QuerySingleAsync<DeliveryControlRow>(
            new CommandDefinition(
                SelectControlSql,
                cancellationToken: cancellationToken));
        var control = ToResponse(controlRow);
        var rollouts = (await connection.QueryAsync<DeliveryRolloutRow>(
            new CommandDefinition(
                SelectRolloutsSql,
                cancellationToken: cancellationToken)))
            .Select(ToResponse)
            .ToArray();
        var groups = (await connection.QueryAsync<DeliveryMetricGroupResponse>(
            new CommandDefinition(
                SelectMetricGroupsSql,
                cancellationToken: cancellationToken))).ToArray();
        using var metrics = await connection.QueryMultipleAsync(
            new CommandDefinition(
                SelectAggregateMetricsSql,
                cancellationToken: cancellationToken));
        var aggregate = await metrics.ReadSingleAsync<AggregateMetrics>();
        var latency = await metrics.ReadSingleAsync<LatencyMetrics>();
        var schemaFailures = await metrics.ReadSingleAsync<int>();
        var sourceToDeliveredP95 = await metrics.ReadSingleAsync<double?>();
        var jsonExportGroups = (await connection
            .QueryAsync<JsonExportMetricGroupResponse>(new CommandDefinition(
                SelectJsonExportMetricsSql,
                cancellationToken: cancellationToken))).ToArray();

        var terminal = aggregate.Delivered + aggregate.PermanentFailed;
        var successRate = Ratio(aggregate.Delivered, aggregate.Total);
        var retryableRate = Ratio(aggregate.RetryableFailed, aggregate.Total);
        var permanentRate = Ratio(aggregate.PermanentFailed, terminal);
        var alerts = BuildAlerts(
            retryableRate,
            permanentRate,
            latency.P95,
            aggregate.OldestQueueAgeSeconds,
            latency.DuplicateConflictCount,
            schemaFailures);

        return new DeliveryOperationsResponse(
            control,
            rollouts,
            groups,
            successRate,
            retryableRate,
            permanentRate,
            latency.P50,
            latency.P95,
            latency.P99,
            aggregate.OldestQueueAgeSeconds,
            aggregate.RetryCount,
            latency.DuplicateConflictCount,
            schemaFailures,
            sourceToDeliveredP95,
            alerts,
            new JsonExportOperationsResponse(
                jsonExportGroups,
                BuildJsonExportAlerts(jsonExportGroups)));
    }

    public Task<DeliveryControlResponse> UpdateControlAsync(
        bool deliveryPaused,
        bool activationEnabled,
        string changedBy,
        string reason,
        CancellationToken cancellationToken) =>
        UpdateControlAsync(
            deliveryPaused,
            activationEnabled,
            jsonFileActivationEnabled: false,
            jsonFileFinalizerPaused: true,
            changedBy,
            reason,
            cancellationToken);

    public async Task<DeliveryControlResponse> UpdateControlAsync(
        bool deliveryPaused,
        bool apiActivationEnabled,
        bool? jsonFileActivationEnabled,
        bool? jsonFileFinalizerPaused,
        string changedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE delivery_runtime_control
            SET
                delivery_paused = @DeliveryPaused,
                api_contract_activation_enabled = @ActivationEnabled,
                json_file_activation_enabled = COALESCE(
                    @JsonFileActivationEnabled,
                    json_file_activation_enabled),
                json_file_finalizer_paused = COALESCE(
                    @JsonFileFinalizerPaused,
                    json_file_finalizer_paused),
                updated_at = now(),
                updated_by = @ChangedBy,
                reason = @Reason
            WHERE singleton = true;

            INSERT INTO delivery_control_audit (
                delivery_paused,
                api_contract_activation_enabled,
                json_file_activation_enabled,
                json_file_finalizer_paused,
                changed_by,
                reason)
            SELECT
                delivery_paused,
                api_contract_activation_enabled,
                json_file_activation_enabled,
                json_file_finalizer_paused,
                @ChangedBy,
                @Reason
            FROM delivery_runtime_control
            WHERE singleton = true;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                DeliveryPaused = deliveryPaused,
                ActivationEnabled = apiActivationEnabled,
                JsonFileActivationEnabled = jsonFileActivationEnabled,
                JsonFileFinalizerPaused = jsonFileFinalizerPaused,
                ChangedBy = changedBy,
                Reason = reason
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        var control = await connection.QuerySingleAsync<DeliveryControlRow>(
            new CommandDefinition(
                SelectControlSql,
                cancellationToken: cancellationToken));
        return ToResponse(control);
    }

    public async Task<bool> UpdateRolloutAsync(
        long configId,
        string stage,
        bool enabled,
        string changedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO api_delivery_rollouts (
                config_id, stage, enabled, approved_by, reason)
            SELECT @ConfigId, @Stage, @Enabled, @ChangedBy, @Reason
            WHERE EXISTS (SELECT 1 FROM mapping_configs WHERE id = @ConfigId)
            ON CONFLICT (config_id) DO UPDATE SET
                stage = EXCLUDED.stage,
                enabled = EXCLUDED.enabled,
                approved_by = EXCLUDED.approved_by,
                reason = EXCLUDED.reason,
                updated_at = now()
            RETURNING config_id;
            """;
        const string auditSql = """
            INSERT INTO api_delivery_rollout_audit (
                config_id, stage, enabled, changed_by, reason)
            VALUES (@ConfigId, @Stage, @Enabled, @ChangedBy, @Reason);
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            ConfigId = configId,
            Stage = stage,
            Enabled = enabled,
            ChangedBy = changedBy,
            Reason = reason
        };
        var updated = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken));
        if (!updated.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            auditSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static DeliveryControlResponse ToResponse(
        DeliveryControlRow row) => new(
            row.DeliveryPaused,
            row.ApiContractActivationEnabled,
        row.JsonFileActivationEnabled,
        row.JsonFileFinalizerPaused,
            AsUtcOffset(row.UpdatedAt),
            row.UpdatedBy,
            row.Reason);

    private static DeliveryRolloutResponse ToResponse(
        DeliveryRolloutRow row) => new(
            row.ConfigId,
            row.ConfigName,
            row.Stage,
            row.Enabled,
            row.ApprovedBy,
            row.Reason,
            AsUtcOffset(row.UpdatedAt));

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record DeliveryControlRow(
        bool DeliveryPaused,
        bool ApiContractActivationEnabled,
        bool JsonFileActivationEnabled,
        bool JsonFileFinalizerPaused,
        DateTime UpdatedAt,
        string UpdatedBy,
        string Reason);

    private sealed record DeliveryRolloutRow(
        long ConfigId,
        string ConfigName,
        string Stage,
        bool Enabled,
        string ApprovedBy,
        string Reason,
        DateTime UpdatedAt);

    public async Task<ApiContractActivationGate> CanActivateApiContractAsync(
        long configId,
        string profileEnvironment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                control.api_contract_activation_enabled AS "ActivationEnabled",
                rollouts.stage AS "Stage",
                COALESCE(rollouts.enabled, false) AS "RolloutEnabled"
            FROM delivery_runtime_control AS control
            LEFT JOIN api_delivery_rollouts AS rollouts
                ON rollouts.config_id = @ConfigId
            WHERE control.singleton = true;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var gate = await connection.QuerySingleAsync<ActivationGateRow>(
            new CommandDefinition(
                sql,
                new { ConfigId = configId },
                cancellationToken: cancellationToken));
        if (!gate.ActivationEnabled)
        {
            return new(false, "New ApiContract activation is disabled by the global kill switch.");
        }

        if (!gate.RolloutEnabled || gate.Stage is null)
        {
            return new(false, "This Mapping Config is not enabled for API delivery rollout.");
        }

        if (gate.Stage == "Shadow" && profileEnvironment == "Production")
        {
            return new(false, "Shadow rollout requires a Stub or Sandbox destination profile.");
        }

        return new(true, null);
    }

    public async Task<ApiContractActivationGate> CanActivateJsonFileAsync(
        long configId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT json_file_activation_enabled
            FROM delivery_runtime_control
            WHERE singleton = true;
            """;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var enabled = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return enabled
            ? new(true, null)
            : new(false,
                $"New JsonFile activation is disabled by the global kill switch for config {configId}.");
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;

    private static IReadOnlyList<DeliveryAlertResponse> BuildAlerts(
        double retryableRate,
        double permanentRate,
        double p95Latency,
        double queueAge,
        int duplicateConflicts,
        int schemaFailures)
    {
        var alerts = new List<DeliveryAlertResponse>();
        if (duplicateConflicts > 0)
        {
            alerts.Add(new("Critical", "DuplicateConflict", "Duplicate/idempotency conflicts are greater than zero; roll back immediately."));
        }
        if (schemaFailures > 0)
        {
            alerts.Add(new("Critical", "SchemaValidationFailure", "API schema validation failures are greater than zero; roll back immediately."));
        }
        if (permanentRate > 0.01)
        {
            alerts.Add(new("Warning", "PermanentFailureRate", "Permanent delivery failure rate is above 1%; pause delivery."));
        }
        if (retryableRate > 0.05)
        {
            alerts.Add(new("Warning", "RetryableFailureRate", "Retryable delivery failure rate is above 5%; pause delivery."));
        }
        if (p95Latency > 5_000)
        {
            alerts.Add(new("Warning", "P95Latency", "Delivery p95 latency is above 5 seconds; pause delivery."));
        }
        if (queueAge > 600)
        {
            alerts.Add(new("Warning", "QueueAge", "Oldest queued delivery is above 10 minutes; pause delivery."));
        }
        return alerts;
    }

    private static IReadOnlyList<DeliveryAlertResponse> BuildJsonExportAlerts(
        IReadOnlyList<JsonExportMetricGroupResponse> groups)
    {
        var alerts = new List<DeliveryAlertResponse>();
        if (groups.Any(group => group.HashConflictCount > 0))
        {
            alerts.Add(new(
                "Critical",
                "JsonExportHashConflict",
                "JSON artifact hash conflicts are greater than zero; pause finalization and investigate the destination volume."));
        }
        if (groups.Any(group => group.CleanupFailureCount > 0))
        {
            alerts.Add(new(
                "Warning",
                "JsonExportCleanupFailure",
                "JSON export cleanup failures are greater than zero; inspect retention, permissions and disk health."));
        }
        if (groups.Any(group => group.Blocked > 0))
        {
            alerts.Add(new(
                "Warning",
                "JsonExportBlocked",
                "One or more JSON exports are blocked; inspect redacted job diagnostics before retry or reprocess."));
        }
        if (groups.Any(group => group.RetryableFailed > 0))
        {
            alerts.Add(new(
                "Warning",
                "JsonExportPublicationFailure",
                "JSON publication failures are waiting for retry; check quota, disk capacity and Output Root permissions."));
        }
        if (groups.Any(group => group.OldestQueueAgeSeconds > 600))
        {
            alerts.Add(new(
                "Warning",
                "JsonExportQueueAge",
                "The oldest JSON export queue item is above 10 minutes; inspect the finalizer kill switch and worker health."));
        }
        return alerts;
    }

    private sealed record AggregateMetrics(
        int Total,
        int Delivered,
        int RetryableFailed,
        int PermanentFailed,
        double OldestQueueAgeSeconds,
        int RetryCount);

    private sealed record LatencyMetrics(
        double P50,
        double P95,
        double P99,
        int DuplicateConflictCount);

    private sealed record ActivationGateRow(
        bool ActivationEnabled,
        string? Stage,
        bool RolloutEnabled);
}
