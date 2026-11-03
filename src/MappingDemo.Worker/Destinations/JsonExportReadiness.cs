using System.Globalization;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Serialization;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportReadiness
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string LockFileJobSql = """
        SELECT
            file_jobs.id AS "Id",
            file_jobs.file_name AS "FileName",
            file_jobs.config_version_id AS "ConfigVersionId",
            mapping_config_versions.version_no AS "ConfigVersion",
            mapping_config_versions.destination_definition::text
                AS "DestinationDefinitionJson"
        FROM file_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id = file_jobs.config_version_id
        WHERE file_jobs.id = @FileJobId
        FOR UPDATE OF file_jobs;
        """;

    private const string InsertInitialExportJobSql = """
        INSERT INTO json_export_jobs (
            file_job_id,
            config_version_id,
            destination_profile_id,
            artifact_revision,
            status,
            relative_temp_path,
            relative_final_path)
        VALUES (
            @FileJobId,
            @ConfigVersionId,
            @DestinationProfileId,
            1,
            'Pending',
            @RelativeTempPath,
            @RelativeFinalPath)
        ON CONFLICT (
            file_job_id,
            config_version_id,
            artifact_revision)
        DO NOTHING;
        """;

    private const string SelectCandidateJobsSql = """
        SELECT id AS "Id", config_version_id AS "ConfigVersionId"
        FROM json_export_jobs
        WHERE file_job_id = @FileJobId
          AND status = 'Pending'
        ORDER BY artifact_revision;
        """;

    private const string IsReadySql = """
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
            file_jobs.import_status = 'Imported'
            AND file_jobs.total_rows IS NOT NULL
            AND file_jobs.total_rows = COUNT(latest_row_jobs.id)
            AND (
                @CurrentRowJobId IS NULL
                OR BOOL_OR(latest_row_jobs.id = @CurrentRowJobId))
            AND COALESCE(
                BOOL_AND(
                    latest_row_jobs.config_version_id = @ConfigVersionId),
                true)
            AND COALESCE(
                BOOL_AND(
                    CASE
                        WHEN @CurrentRowJobId IS NOT NULL
                             AND latest_row_jobs.id = @CurrentRowJobId
                            THEN @CurrentCompletionStatus
                        ELSE latest_row_jobs.status
                    END IN ('Done', 'Filtered')),
                true)
        FROM file_jobs
        LEFT JOIN latest_row_jobs ON true
        WHERE file_jobs.id = @FileJobId
        GROUP BY
            file_jobs.import_status,
            file_jobs.total_rows;
        """;

    public async Task TryScheduleAsync(
        long fileJobId,
        long? currentRowJobId,
        string? currentCompletionStatus,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var fileJob = await connection
            .QuerySingleOrDefaultAsync<FileJobMetadata>(new CommandDefinition(
            LockFileJobSql,
            new { FileJobId = fileJobId },
            transaction,
            cancellationToken: cancellationToken));
        if (fileJob is null)
        {
            return;
        }

        JsonFileDestination? jsonFile = null;
        try
        {
            jsonFile = PolymorphicJson.Deserialize<DestinationDefinition>(
                    fileJob.DestinationDefinitionJson,
                    JsonOptions)
                as JsonFileDestination;
        }
        catch (JsonException)
        {
            // Destination validation owns malformed persisted definitions.
        }

        if (jsonFile is not null)
        {
            var relativeFinalPath = JsonExportArtifactPath.Resolve(
                jsonFile,
                fileJob.FileName,
                fileJob.Id,
                fileJob.ConfigVersion,
                fileJob.ConfigVersionId,
                artifactRevision: 1);
            await connection.ExecuteAsync(new CommandDefinition(
                InsertInitialExportJobSql,
                new
                {
                    FileJobId = fileJob.Id,
                    fileJob.ConfigVersionId,
                    DestinationProfileId =
                        jsonFile.FileDestinationProfileId,
                    RelativeTempPath = relativeFinalPath + ".tmp",
                    RelativeFinalPath = relativeFinalPath
                },
                transaction,
                cancellationToken: cancellationToken));
        }
        var candidates = (await connection.QueryAsync<ExportJobCandidate>(
            new CommandDefinition(
                SelectCandidateJobsSql,
                new { FileJobId = fileJobId },
                transaction,
                cancellationToken: cancellationToken))).ToArray();

        foreach (var candidate in candidates)
        {
            var isReady = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    IsReadySql,
                    new
                    {
                        FileJobId = fileJobId,
                        candidate.ConfigVersionId,
                        CurrentRowJobId = currentRowJobId,
                        CurrentCompletionStatus = currentCompletionStatus
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            if (!isReady)
            {
                continue;
            }

            await OutboxWriter.AddOnceAsync(
                connection,
                transaction,
                Topics.JsonExportFinalize,
                candidate.Id.ToString(CultureInfo.InvariantCulture),
                new JsonExportRequested(candidate.Id),
                cancellationToken);
        }
    }

    private sealed record ExportJobCandidate(
        long Id,
        long ConfigVersionId);

    private sealed record FileJobMetadata(
        long Id,
        string FileName,
        long ConfigVersionId,
        int ConfigVersion,
        string DestinationDefinitionJson);
}
