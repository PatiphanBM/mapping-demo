using System.Diagnostics;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Destinations;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseTargetSchemaMonitor(
    NpgsqlDataSource dataSource,
    IDatabaseTargetSchemaInspector inspector,
    DatabaseDeliveryObservability observability,
    IConfiguration configuration,
    ILogger<DatabaseTargetSchemaMonitor> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string SelectTargetsSql = """
        WITH active_targets AS (
            SELECT
                versions.destination_definition ->> 'profileId' AS profile_id,
                versions.destination_definition ->> 'schema' AS target_schema,
                versions.destination_definition ->> 'table' AS target_table,
                (versions.destination_definition -> 'schemaSnapshot')::text
                    AS snapshot_json,
                (versions.destination_definition -> 'duplicatePolicy')::text
                    AS duplicate_policy_json,
                COALESCE((
                    SELECT jsonb_agg(fields.column_name ORDER BY fields.column_name)
                    FROM (
                        SELECT DISTINCT COALESCE(
                            field.value ->> 'normalizedColumn',
                            field.value ->> 'targetPath') AS column_name
                        FROM jsonb_path_query(
                            versions.transformation_program,
                            '$.outputs[*].fields[*]') AS field(value)
                        WHERE COALESCE(
                            field.value ->> 'normalizedColumn',
                            field.value ->> 'targetPath') IS NOT NULL
                    ) AS fields
                ), '[]'::jsonb)::text AS mapped_columns_json
            FROM mapping_configs AS configs
            INNER JOIN mapping_config_versions AS versions
                ON versions.id = configs.active_version_id
            WHERE versions.destination_definition ->> 'kind' = 'databaseTable'
        ), job_targets AS (
            SELECT DISTINCT
                destination_profile_id AS profile_id,
                target_schema,
                target_table,
                target_schema_snapshot::text AS snapshot_json,
                duplicate_policy::text AS duplicate_policy_json,
                mapped_columns::text AS mapped_columns_json
            FROM database_delivery_jobs
            WHERE payload_purged_at IS NULL
              AND status NOT IN ('Delivered', 'AlreadyDelivered')
        ), targets AS (
            SELECT * FROM active_targets
            UNION
            SELECT * FROM job_targets
        )
        SELECT
            profile_id AS "ProfileId",
            target_schema AS "TargetSchema",
            target_table AS "TargetTable",
            snapshot_json AS "SnapshotJson",
            duplicate_policy_json AS "DuplicatePolicyJson",
            mapped_columns_json AS "MappedColumnsJson"
        FROM targets
        ORDER BY profile_id, target_schema, target_table,
            snapshot_json, duplicate_policy_json, mapped_columns_json
        LIMIT @MaximumTargets;
        """;

    private const string InsertCheckSql = """
        INSERT INTO database_target_schema_checks (
            destination_profile_id, target_schema, target_table,
            configured_schema_hash, current_schema_hash,
            compatibility, diagnostics)
        VALUES (
            @ProfileId, @TargetSchema, @TargetTable,
            @ConfiguredSchemaHash, @CurrentSchemaHash,
            @Compatibility, CAST(@DiagnosticsJson AS jsonb));
        """;

    private readonly bool _enabled = configuration.GetValue(
        "Features:DatabaseTableSchemaMonitor",
        false);
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(Math.Clamp(
        configuration.GetValue("DatabaseDelivery:SchemaMonitorSeconds", 300),
        30,
        86_400));
    private readonly int _maximumTargets = Math.Clamp(
        configuration.GetValue("DatabaseDelivery:SchemaMonitorMaximumTargets", 100),
        1,
        500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            return;
        }

        await RunSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var targets = (await connection.QueryAsync<MonitorTarget>(
            new CommandDefinition(
                SelectTargetsSql,
                new { MaximumTargets = _maximumTargets },
                cancellationToken: cancellationToken))).ToArray();
        foreach (var target in targets)
        {
            var snapshot = JsonSerializer.Deserialize<TargetSchemaSnapshot>(
                    target.SnapshotJson,
                    JsonOptions)
                ?? throw new JsonException("Target schema snapshot is empty.");
            var policy = JsonSerializer.Deserialize<DatabaseDuplicatePolicy>(
                    target.DuplicatePolicyJson,
                    JsonOptions)
                ?? throw new JsonException("Database duplicate policy is empty.");
            var mappedColumns = JsonSerializer.Deserialize<string[]>(
                    target.MappedColumnsJson,
                    JsonOptions)
                ?? [];
            var selectedKey = policy is ExistingUniqueKeyDuplicatePolicy key
                ? key.Columns
                : [];
            var stopwatch = Stopwatch.StartNew();
            SchemaCompatibilityResult compatibility;
            try
            {
                var current = await inspector.InspectAsync(
                    new TargetTableReference(
                        target.ProfileId,
                        target.TargetSchema,
                        target.TargetTable),
                    refresh: true,
                    cancellationToken);
                compatibility = TargetSchemaCompatibilityAnalyzer.Compare(
                    snapshot,
                    current,
                    mappedColumns.ToHashSet(StringComparer.Ordinal),
                    selectedKey);
            }
            catch (DatabaseSchemaInspectionException)
            {
                compatibility = TargetSchemaCompatibilityAnalyzer.Unavailable(
                    snapshot);
            }
            stopwatch.Stop();
            var compatibilityName = compatibility.Status.ToString();
            await connection.ExecuteAsync(new CommandDefinition(
                InsertCheckSql,
                new
                {
                    target.ProfileId,
                    target.TargetSchema,
                    target.TargetTable,
                    ConfiguredSchemaHash = snapshot.SchemaHash,
                    CurrentSchemaHash = compatibility.CurrentHash,
                    Compatibility = compatibilityName,
                    DiagnosticsJson = JsonSerializer.Serialize(
                        compatibility.Changes,
                        JsonOptions)
                },
                cancellationToken: cancellationToken));
            observability.RecordSchemaCheck(
                snapshot.Provider.ToString(),
                compatibilityName,
                stopwatch.Elapsed);
            if (compatibility.BlocksDelivery)
            {
                logger.LogWarning(
                    "Database schema check blocked {ProfileId}/{Schema}/{Table} with {Compatibility}",
                    target.ProfileId,
                    target.TargetSchema,
                    target.TargetTable,
                    compatibilityName);
            }
        }
        return targets.Length;
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database target schema monitor failed.");
        }
    }

    private sealed record MonitorTarget(
        string ProfileId,
        string TargetSchema,
        string TargetTable,
        string SnapshotJson,
        string DuplicatePolicyJson,
        string MappedColumnsJson);
}
