using System.Globalization;
using System.Text;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Transformation;
using MappingDemo.Worker.Delivery;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class ApiContractDestinationAdapter(
    PayloadBindingModule payloadBindings,
    DeliveryPayloadProtector payloadProtector) : IDestinationAdapter
{
    private const string InsertDeliveryJobSql = """
        INSERT INTO delivery_jobs (
            file_job_id,
            row_job_id,
            source_row_id,
            source_row_number,
            config_version_id,
            output_key,
            operation_kind,
            destination_profile_id,
            destination_profile_version_id,
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
            audit_expires_at)
        VALUES (
            @FileJobId,
            @RowJobId,
            @SourceRowId,
            @SourceRowNumber,
            @ConfigVersionId,
            @OutputKey,
            @OperationKind,
            @DestinationProfileId,
            @DestinationProfileVersionId,
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
            @Status,
            now(),
            now(),
            now() + interval '30 days',
            now() + interval '365 days')
        ON CONFLICT (
            config_version_id,
            file_job_id,
            source_row_id,
            output_key,
            resend_sequence)
        DO NOTHING
        RETURNING id;
        """;

    public Type DestinationType { get; } = typeof(ApiContractDestination);

    public DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CompilePinned(
            destination,
            context.TargetSchema,
            context.PayloadBindings,
            context.ApiDestinationProfileVersionId);
    }

    public DestinationCompilationResult CompilePinned(
        DestinationDefinition destination,
        TargetSchema targetSchema,
        IReadOnlyList<PayloadBinding> bindings,
        long? profileVersionId = null)
    {
        if (destination is not ApiContractDestination api)
        {
            return new DestinationCompilationResult(
                null,
                [new DestinationDiagnostic(
                    "destination.kind",
                    "UnsupportedDestinationKind",
                    "The API contract adapter only supports apiContract destinations.")]);
        }

        return new DestinationCompilationResult(
            new CompiledApiContractDestination(
                api.ApiDestinationProfileId,
                profileVersionId,
                api.Contract,
                targetSchema,
                bindings.ToArray()),
            []);
    }

    public async ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var scheduled = await ScheduleAsync(
            destination,
            envelope,
            connection,
            transaction,
            cancellationToken);
        return new RowDeliveryResult(
            scheduled.CompletionStatus,
            scheduled.Diagnostics.Select(diagnostic =>
                new RowDeliveryDiagnostic(
                    diagnostic.OutputKey,
                    diagnostic.Path,
                    diagnostic.Reason)).ToArray());
    }

    public async ValueTask<ApiSchedulingResult> ScheduleAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (destination is not CompiledApiContractDestination api)
        {
            throw new ArgumentException(
                "The compiled destination was not produced by the API contract adapter.",
                nameof(destination));
        }

        var diagnostics = new List<ApiSchedulingDiagnostic>();
        var prepared = new List<PreparedOutput>();
        foreach (var output in envelope.Outputs)
        {
            var binding = payloadBindings.Bind(
                api.TargetSchema,
                output.Values,
                api.Bindings,
                new PayloadBindingContext(
                    api.Contract.Reference,
                    envelope.FileJobId,
                    envelope.SourceRowId,
                    envelope.RowNumber,
                    envelope.ConfigVersionId.ToString(CultureInfo.InvariantCulture),
                    output.OutputKey,
                    envelope.FileName));
            if (!binding.IsSuccess)
            {
                diagnostics.AddRange(binding.Diagnostics.Select(item =>
                    new ApiSchedulingDiagnostic(
                        output.OutputKey,
                        item.Path,
                        $"{item.Code}: {item.Message}")));
                continue;
            }

            prepared.Add(new PreparedOutput(output.OutputKey, binding.Payload!));
        }

        if (diagnostics.Count > 0)
        {
            return new ApiSchedulingResult(
                RowJobStatus.Invalid,
                0,
                diagnostics);
        }

        var scheduled = 0;
        foreach (var item in prepared)
        {
            var payload = item.Payload;
            var protectedPayload = await payloadProtector.ProtectAsync(
                Encoding.UTF8.GetBytes(payload.Json),
                cancellationToken);
            var idempotencyKey = FormattableString.Invariant(
                $"{envelope.ConfigVersionId}:{envelope.FileJobId}:{envelope.SourceRowId}:{item.OutputKey}");
            var deliveryJobId = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    InsertDeliveryJobSql,
                    new
                    {
                        envelope.FileJobId,
                        envelope.RowJobId,
                        envelope.SourceRowId,
                        SourceRowNumber = envelope.RowNumber,
                        envelope.ConfigVersionId,
                        item.OutputKey,
                        OperationKind = envelope.OperationKind,
                        DestinationProfileId = api.ProfileId,
                        DestinationProfileVersionId = api.ProfileVersionId,
                        ContractId = api.Contract.Id,
                        ContractVersion = api.Contract.Version,
                        ContractReference = api.Contract.Reference,
                        SchemaHash = api.Contract.SchemaHash,
                        PayloadCiphertext = protectedPayload.Ciphertext,
                        PayloadNonce = protectedPayload.Nonce,
                        PayloadTag = protectedPayload.Tag,
                        PayloadHash = payload.Hash,
                        EncryptionKeyReference = protectedPayload.KeyReference,
                        IdempotencyKey = idempotencyKey,
                        Status = DeliveryJobStatus.Pending
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            if (!deliveryJobId.HasValue)
            {
                continue;
            }

            await OutboxWriter.AddAsync(
                connection,
                transaction,
                Topics.ApiDelivery,
                deliveryJobId.Value.ToString(CultureInfo.InvariantCulture),
                new DeliveryRequested(deliveryJobId.Value),
                cancellationToken);
            scheduled++;
        }

        return new ApiSchedulingResult(
            envelope.Outputs.Count == 0
                ? RowJobStatus.Filtered
                : RowJobStatus.Done,
            scheduled,
            []);
    }

    private sealed class CompiledApiContractDestination(
        string profileId,
        long? profileVersionId,
        ContractReference contract,
        TargetSchema targetSchema,
        IReadOnlyList<PayloadBinding> bindings) : CompiledDestination
    {
        public string ProfileId { get; } = profileId;
        public long? ProfileVersionId { get; } = profileVersionId;
        public ContractReference Contract { get; } = contract;
        public TargetSchema TargetSchema { get; } = targetSchema;
        public IReadOnlyList<PayloadBinding> Bindings { get; } = bindings;
    }

    private sealed record PreparedOutput(
        string OutputKey,
        DeliveryPayload Payload);
}

public sealed record ApiSchedulingDiagnostic(
    string OutputKey,
    string Path,
    string Reason);

public sealed record ApiSchedulingResult(
    string CompletionStatus,
    int ScheduledJobs,
    IReadOnlyList<ApiSchedulingDiagnostic> Diagnostics);
