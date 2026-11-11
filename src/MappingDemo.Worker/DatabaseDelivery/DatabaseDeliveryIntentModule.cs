using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Messaging;
using MappingDemo.Worker.Delivery;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed record DatabaseDeliveryIntent(
    long FileJobId,
    long RowJobId,
    long SourceRowId,
    int SourceRowNumber,
    long ConfigVersionId,
    string OutputKey,
    string OperationKind,
    DatabaseTableDestination Destination,
    IReadOnlyDictionary<string, object?> Values);

public enum DatabaseDeliveryIntentPersistenceResult
{
    Persisted,
    Conflict
}

public sealed class DatabaseDeliveryIntentModule(
    DatabaseDeliveryPayloadProtector payloadProtector,
    IDatabaseDestinationProfileCatalog profiles)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string InsertJobSql = """
        INSERT INTO database_delivery_jobs (
            file_job_id,
            row_job_id,
            source_row_id,
            source_row_number,
            config_version_id,
            output_key,
            operation_kind,
            destination_profile_id,
            provider,
            profile_configuration_hash,
            target_schema,
            target_table,
            target_schema_snapshot,
            target_schema_hash,
            duplicate_policy,
            mapped_columns,
            mapped_values_ciphertext,
            mapped_values_nonce,
            mapped_values_tag,
            mapped_values_hash,
            encryption_key_reference,
            status)
        VALUES (
            @FileJobId,
            @RowJobId,
            @SourceRowId,
            @SourceRowNumber,
            @ConfigVersionId,
            @OutputKey,
            @OperationKind,
            @DestinationProfileId,
            @Provider,
            @ProfileConfigurationHash,
            @TargetSchema,
            @TargetTable,
            CAST(@TargetSchemaSnapshotJson AS jsonb),
            @TargetSchemaHash,
            CAST(@DuplicatePolicyJson AS jsonb),
            CAST(@MappedColumnsJson AS jsonb),
            @MappedValuesCiphertext,
            @MappedValuesNonce,
            @MappedValuesTag,
            @MappedValuesHash,
            @EncryptionKeyReference,
            'Pending')
        ON CONFLICT (row_job_id, output_key)
        DO UPDATE SET updated_at = database_delivery_jobs.updated_at
        WHERE database_delivery_jobs.mapped_values_hash =
            EXCLUDED.mapped_values_hash
          AND database_delivery_jobs.target_schema_hash =
            EXCLUDED.target_schema_hash
        RETURNING id;
        """;

    public async Task<DatabaseDeliveryIntentPersistenceResult> PersistAsync(
        DatabaseDeliveryIntent intent,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var plaintext = SerializeValues(intent.Values);
        var hash = Hash(plaintext);
        var protectedValues = await payloadProtector.ProtectAsync(
            plaintext,
            cancellationToken);
        var destination = intent.Destination;
        var jobId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                InsertJobSql,
                new
                {
                    intent.FileJobId,
                    intent.RowJobId,
                    intent.SourceRowId,
                    intent.SourceRowNumber,
                    intent.ConfigVersionId,
                    intent.OutputKey,
                    intent.OperationKind,
                    DestinationProfileId = destination.ProfileId,
                    Provider = destination.SchemaSnapshot.Provider.ToString(),
                    ProfileConfigurationHash = profiles
                        .GetRequired(destination.ProfileId)
                        .ConfigurationHash,
                    TargetSchema = destination.Schema,
                    TargetTable = destination.Table,
                    TargetSchemaSnapshotJson = JsonSerializer.Serialize(
                        destination.SchemaSnapshot,
                        JsonOptions),
                    TargetSchemaHash = destination.SchemaHash,
                    DuplicatePolicyJson = JsonSerializer.Serialize(
                        destination.DuplicatePolicy,
                        JsonOptions),
                    MappedColumnsJson = JsonSerializer.Serialize(
                        intent.Values.Keys.Order(StringComparer.Ordinal),
                        JsonOptions),
                    MappedValuesCiphertext = protectedValues.Ciphertext,
                    MappedValuesNonce = protectedValues.Nonce,
                    MappedValuesTag = protectedValues.Tag,
                    MappedValuesHash = hash,
                    EncryptionKeyReference = protectedValues.KeyReference
                },
                transaction,
                cancellationToken: cancellationToken));
        if (!jobId.HasValue)
        {
            return DatabaseDeliveryIntentPersistenceResult.Conflict;
        }

        await OutboxWriter.AddOnceAsync(
            connection,
            transaction,
            Topics.DatabaseDelivery,
            jobId.Value.ToString(CultureInfo.InvariantCulture),
            new DatabaseDeliveryRequested(jobId.Value),
            cancellationToken);
        return DatabaseDeliveryIntentPersistenceResult.Persisted;
    }

    private static byte[] SerializeValues(
        IReadOnlyDictionary<string, object?> values)
    {
        var sorted = new SortedDictionary<string, object?>(
            StringComparer.Ordinal);
        foreach (var pair in values)
        {
            sorted.Add(pair.Key, pair.Value);
        }

        return JsonSerializer.SerializeToUtf8Bytes(sorted, JsonOptions);
    }

    private static string Hash(ReadOnlySpan<byte> values) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(values)).ToLowerInvariant()}";
}
