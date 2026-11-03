using System.Globalization;
using System.Text;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Transformation;
using MappingDemo.Worker.Delivery;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonFileDestinationAdapter(
    PayloadBindingModule payloadBindings,
    JsonExportPayloadProtector payloadProtector,
    JsonExportReadiness readiness) : IDestinationAdapter
{
    private const string LockFileJobSql = """
        SELECT
            file_jobs.file_name AS "FileName",
            mapping_config_versions.version_no AS "ConfigVersion"
        FROM file_jobs
        INNER JOIN mapping_config_versions
            ON mapping_config_versions.id = @ConfigVersionId
           AND mapping_config_versions.config_id = file_jobs.config_id
        WHERE file_jobs.id = @FileJobId
        FOR UPDATE OF file_jobs;
        """;

    private const string SelectLatestExportJobSql = """
        SELECT
            id AS "Id",
            config_version_id AS "ConfigVersionId",
            artifact_revision AS "ArtifactRevision",
            status AS "Status"
        FROM json_export_jobs
        WHERE file_job_id = @FileJobId
        ORDER BY artifact_revision DESC, id DESC
        LIMIT 1;
        """;

    private const string InsertExportJobSql = """
        INSERT INTO json_export_jobs (
            file_job_id,
            config_version_id,
            destination_profile_id,
            artifact_revision,
            status,
            relative_temp_path,
            relative_final_path,
            previous_export_job_id)
        VALUES (
            @FileJobId,
            @ConfigVersionId,
            @DestinationProfileId,
            @ArtifactRevision,
            @Status,
            @RelativeTempPath,
            @RelativeFinalPath,
            @PreviousExportJobId)
        ON CONFLICT (
            file_job_id,
            config_version_id,
            artifact_revision)
        DO UPDATE SET updated_at = json_export_jobs.updated_at
        RETURNING id;
        """;

    private const string UpsertItemSql = """
        INSERT INTO json_export_items (
            json_export_job_id,
            file_job_id,
            config_version_id,
            artifact_revision,
            row_job_id,
            source_row_id,
            row_number,
            output_key,
            output_ordinal,
            payload_ciphertext,
            payload_nonce,
            payload_tag,
            encryption_key_reference,
            payload_sha256)
        VALUES (
            @JsonExportJobId,
            @FileJobId,
            @ConfigVersionId,
            @ArtifactRevision,
            @RowJobId,
            @SourceRowId,
            @RowNumber,
            @OutputKey,
            @OutputOrdinal,
            @PayloadCiphertext,
            @PayloadNonce,
            @PayloadTag,
            @EncryptionKeyReference,
            @PayloadSha256)
        ON CONFLICT (
            file_job_id,
            config_version_id,
            artifact_revision,
            source_row_id,
            output_key)
        DO UPDATE SET
            json_export_job_id = EXCLUDED.json_export_job_id,
            row_job_id = EXCLUDED.row_job_id,
            row_number = EXCLUDED.row_number,
            output_ordinal = EXCLUDED.output_ordinal,
            payload_ciphertext = CASE
                WHEN json_export_items.payload_sha256 = EXCLUDED.payload_sha256
                     AND json_export_items.purged_at IS NULL
                    THEN json_export_items.payload_ciphertext
                ELSE EXCLUDED.payload_ciphertext
            END,
            payload_nonce = CASE
                WHEN json_export_items.payload_sha256 = EXCLUDED.payload_sha256
                     AND json_export_items.purged_at IS NULL
                    THEN json_export_items.payload_nonce
                ELSE EXCLUDED.payload_nonce
            END,
            payload_tag = CASE
                WHEN json_export_items.payload_sha256 = EXCLUDED.payload_sha256
                     AND json_export_items.purged_at IS NULL
                    THEN json_export_items.payload_tag
                ELSE EXCLUDED.payload_tag
            END,
            encryption_key_reference = CASE
                WHEN json_export_items.payload_sha256 = EXCLUDED.payload_sha256
                     AND json_export_items.purged_at IS NULL
                    THEN json_export_items.encryption_key_reference
                ELSE EXCLUDED.encryption_key_reference
            END,
            payload_sha256 = EXCLUDED.payload_sha256,
            updated_at = now(),
            expires_at = NULL,
            purged_at = NULL;
        """;

    private const string DeleteStaleItemsSql = """
        DELETE FROM json_export_items
        WHERE json_export_job_id = @JsonExportJobId
          AND source_row_id = @SourceRowId
          AND NOT (output_key = ANY(@OutputKeys));
        """;

    public Type DestinationType { get; } = typeof(JsonFileDestination);

    public DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(context);

        if (destination is not JsonFileDestination jsonFile)
        {
            return new DestinationCompilationResult(
                null,
                [new DestinationDiagnostic(
                    "destination.kind",
                    "UnsupportedDestinationKind",
                    "The JSON file adapter only supports jsonFile destinations.")]);
        }

        return new DestinationCompilationResult(
            new CompiledJsonFileDestination(
                jsonFile,
                context.TargetSchema,
                context.PayloadBindings.ToArray()),
            []);
    }

    public async ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (destination is not CompiledJsonFileDestination jsonFile)
        {
            throw new ArgumentException(
                "The compiled destination was not produced by the JSON file adapter.",
                nameof(destination));
        }

        var prepared = new List<PreparedItem>(envelope.Outputs.Count);
        for (var outputOrdinal = 0;
             outputOrdinal < envelope.Outputs.Count;
             outputOrdinal++)
        {
            var output = envelope.Outputs[outputOrdinal];
            var binding = payloadBindings.Bind(
                jsonFile.TargetSchema,
                output.Values,
                jsonFile.Bindings,
                new PayloadBindingContext(
                    jsonFile.Definition.Contract.Reference,
                    envelope.FileJobId,
                    envelope.SourceRowId,
                    envelope.RowNumber,
                    envelope.ConfigVersionId.ToString(
                        CultureInfo.InvariantCulture),
                    output.OutputKey,
                    envelope.FileName));
            if (!binding.IsSuccess)
            {
                return new RowDeliveryResult(
                    RowJobStatus.Invalid,
                    binding.Diagnostics.Select(diagnostic =>
                        new RowDeliveryDiagnostic(
                            output.OutputKey,
                            diagnostic.Path,
                            $"{diagnostic.Code}: {diagnostic.Message}"))
                        .ToArray());
            }

            var payload = binding.Payload!;
            var protectedPayload = await payloadProtector.ProtectAsync(
                Encoding.UTF8.GetBytes(payload.Json),
                cancellationToken);
            prepared.Add(new PreparedItem(
                output.OutputKey,
                outputOrdinal,
                payload.Hash,
                protectedPayload));
        }

        var fileJob = await connection.QuerySingleAsync<FileJobMetadata>(
            new CommandDefinition(
                LockFileJobSql,
                new
                {
                    envelope.FileJobId,
                    envelope.ConfigVersionId
                },
                transaction,
                cancellationToken: cancellationToken));
        var latest = await connection.QuerySingleOrDefaultAsync<ExportJob>(
            new CommandDefinition(
                SelectLatestExportJobSql,
                new { envelope.FileJobId },
                transaction,
                cancellationToken: cancellationToken));
        var startsNewRevision = latest is null
            || latest.ConfigVersionId != envelope.ConfigVersionId
            || latest.Status is "Published" or "Blocked";
        var artifactRevision = startsNewRevision
            ? (latest?.ArtifactRevision ?? 0) + 1
            : latest!.ArtifactRevision;
        var previousExportJobId = startsNewRevision ? latest?.Id : null;
        var relativeFinalPath = JsonExportArtifactPath.Resolve(
            jsonFile.Definition,
            fileJob.FileName,
            envelope.FileJobId,
            fileJob.ConfigVersion,
            envelope.ConfigVersionId,
            artifactRevision);
        var exportJobId = startsNewRevision
            ? await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                InsertExportJobSql,
                new
                {
                    envelope.FileJobId,
                    envelope.ConfigVersionId,
                    DestinationProfileId = jsonFile.Definition
                        .FileDestinationProfileId,
                    ArtifactRevision = artifactRevision,
                    Status = "Pending",
                    RelativeTempPath = relativeFinalPath + ".tmp",
                    RelativeFinalPath = relativeFinalPath,
                    PreviousExportJobId = previousExportJobId
                },
                transaction,
                cancellationToken: cancellationToken))
            : latest!.Id;

        foreach (var item in prepared)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UpsertItemSql,
                new
                {
                    JsonExportJobId = exportJobId,
                    envelope.FileJobId,
                    envelope.ConfigVersionId,
                    ArtifactRevision = artifactRevision,
                    envelope.RowJobId,
                    envelope.SourceRowId,
                    envelope.RowNumber,
                    item.OutputKey,
                    item.OutputOrdinal,
                    PayloadCiphertext = item.ProtectedPayload.Ciphertext,
                    PayloadNonce = item.ProtectedPayload.Nonce,
                    PayloadTag = item.ProtectedPayload.Tag,
                    EncryptionKeyReference =
                        item.ProtectedPayload.KeyReference,
                    PayloadSha256 = item.PayloadHash
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            DeleteStaleItemsSql,
            new
            {
                JsonExportJobId = exportJobId,
                envelope.SourceRowId,
                OutputKeys = prepared
                    .Select(item => item.OutputKey)
                    .ToArray()
            },
            transaction,
            cancellationToken: cancellationToken));

        var completionStatus = prepared.Count == 0
            ? RowJobStatus.Filtered
            : RowJobStatus.Done;
        await readiness.TryScheduleAsync(
            envelope.FileJobId,
            envelope.RowJobId,
            completionStatus,
            connection,
            transaction,
            cancellationToken);

        return new RowDeliveryResult(completionStatus, []);
    }

    private sealed class CompiledJsonFileDestination(
        JsonFileDestination definition,
        TargetSchema targetSchema,
        IReadOnlyList<PayloadBinding> bindings) : CompiledDestination
    {
        public JsonFileDestination Definition { get; } = definition;

        public TargetSchema TargetSchema { get; } = targetSchema;

        public IReadOnlyList<PayloadBinding> Bindings { get; } = bindings;
    }

    private sealed record PreparedItem(
        string OutputKey,
        int OutputOrdinal,
        string PayloadHash,
        ProtectedDeliveryPayload ProtectedPayload);

    private sealed record FileJobMetadata(
        string FileName,
        int ConfigVersion);

    private sealed record ExportJob(
        long Id,
        long ConfigVersionId,
        int ArtifactRevision,
        string Status);
}
