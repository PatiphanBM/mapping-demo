using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Destinations;
using MappingDemo.Worker.Delivery;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryJobHandler(
    DatabaseDeliveryJobRepository repository,
    DatabaseDeliveryPayloadProtector payloadProtector,
    DatabaseWriteModule writeModule,
    DatabaseDeliveryControlReader control,
    DatabaseDeliveryObservability observability,
    IConfiguration configuration,
    ILogger<DatabaseDeliveryJobHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly bool _deliveryEnabled = configuration.GetValue(
        "Features:DatabaseTableDelivery",
        false);

    public async Task HandleAsync(
        long jobId,
        CancellationToken cancellationToken)
    {
        if (!_deliveryEnabled || await control.IsPausedAsync(cancellationToken))
        {
            await repository.ReleaseScheduledAsync(jobId, cancellationToken);
            return;
        }

        var leaseOwner = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var job = await repository.ClaimAsync(
            jobId,
            leaseOwner,
            cancellationToken);
        if (job is null)
        {
            return;
        }

        DatabaseWriteResult result;
        byte[]? plaintext = null;
        try
        {
            plaintext = await payloadProtector.UnprotectAsync(
                job.Ciphertext,
                job.Nonce,
                job.Tag,
                job.EncryptionKeyReference,
                cancellationToken);
            var hash = $"sha256:{Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant()}";
            if (!string.Equals(hash, job.MappedValuesHash, StringComparison.Ordinal))
            {
                result = new DatabaseWriteResult(
                    DatabaseDeliveryJobStatus.PermanentFailed,
                    "PayloadHashMismatch",
                    "Persisted mapped values failed integrity validation.");
            }
            else
            {
                var snapshot = JsonSerializer.Deserialize<TargetSchemaSnapshot>(
                        job.SchemaSnapshotJson,
                        JsonOptions)
                    ?? throw new JsonException("Schema snapshot is empty.");
                var policy = JsonSerializer.Deserialize<DatabaseDuplicatePolicy>(
                        job.DuplicatePolicyJson,
                        JsonOptions)
                    ?? throw new JsonException("Duplicate policy is empty.");
                var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        plaintext,
                        JsonOptions)
                    ?? throw new JsonException("Mapped values are empty.");
                result = await writeModule.WriteAsync(
                    new DatabaseWriteRequest(
                        job.ProfileId,
                        job.ProfileConfigurationHash,
                        snapshot,
                        policy,
                        values),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or
                                           CryptographicException or
                                           InvalidOperationException)
        {
            logger.LogWarning(
                "Database delivery job {DatabaseDeliveryJobId} could not prepare persisted values: {ErrorType}",
                jobId,
                exception.GetType().Name);
            result = new DatabaseWriteResult(
                DatabaseDeliveryJobStatus.PermanentFailed,
                "PersistedPayloadInvalid",
                "Persisted database delivery values are invalid.");
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        if (result.Status == DatabaseDeliveryJobStatus.Pending)
        {
            await repository.ReleaseClaimAsync(
                job.Id,
                leaseOwner,
                result.ErrorRedacted ?? "Database writes are paused.",
                cancellationToken);
            return;
        }

        if (result.Status == DatabaseDeliveryJobStatus.RetryableFailed &&
            job.AttemptNo >= DeliveryRetryPolicy.MaximumAttempts)
        {
            result = result with
            {
                Status = DatabaseDeliveryJobStatus.PermanentFailed,
                ErrorCode = "RetryLimitExceeded",
                ErrorRedacted = "Database delivery retry limit was exceeded."
            };
        }
        DateTimeOffset? nextAttemptAt =
            result.Status == DatabaseDeliveryJobStatus.RetryableFailed
                ? DateTimeOffset.UtcNow + DeliveryRetryPolicy.Delay(
                    job.AttemptNo,
                    retryAfter: null,
                    jitterFactor: 0.5 + Random.Shared.NextDouble())
                : null;
        stopwatch.Stop();
        await repository.CompleteAsync(
            job,
            leaseOwner,
            startedAt,
            stopwatch.Elapsed,
            result,
            nextAttemptAt,
            cancellationToken);
        observability.RecordDelivery(job.Provider, result.Status);
        logger.LogInformation(
            "Database delivery job {DatabaseDeliveryJobId} attempt {AttemptNo} completed as {DeliveryStatus}",
            job.Id,
            job.AttemptNo,
            result.Status);
    }
}
