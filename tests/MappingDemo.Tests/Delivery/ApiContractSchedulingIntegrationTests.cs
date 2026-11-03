using System.Text;
using System.Text.Json;
using Dapper;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Tables;
using MappingDemo.Tests.Destinations;
using MappingDemo.Worker.Delivery;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.Delivery;

public sealed class ApiContractSchedulingIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Scheduling_is_atomic_encrypted_and_idempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        using var fixture = LoadCompatibilityFixture();
        var retryBaseline = fixture.RootElement.GetProperty("apiRetry");

        var schema = $"delivery_schedule_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                scopedConnectionString);
            await new MigrationRunner(dataSource).RunAsync();
            await SeedAsync(dataSource);

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiDelivery:PayloadEncryptionKeyReference"] = "test-key"
                })
                .Build();
            var protector = new DeliveryPayloadProtector(
                new TestSecretProvider(key),
                configuration);
            var adapter = new ApiContractDestinationAdapter(
                new PayloadBindingModule(),
                protector);
            var router = DestinationRouterTestFactory.Create(adapter);
            var schemaCompilation = new ContractSchemaModule().Compile("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" }
                  },
                  "required": ["name"],
                  "additionalProperties": false
                }
                """);
            Assert.True(schemaCompilation.IsSuccess);
            var destination = new ApiContractDestination(
                "pa-mtls",
                new ContractReference(
                    "proposal",
                    "1",
                    "proposal/1",
                    schemaCompilation.SchemaHash!));
            var compiled = router.Compile(
                destination,
                new DestinationCompilationContext(
                    schemaCompilation.TargetSchema!,
                    []));
            Assert.True(compiled.IsSuccess);
            var envelope = new DeliveryEnvelope(
                100,
                1,
                1,
                1,
                1,
                [
                    new TransformationOutputResult(
                        "pmx",
                        new Dictionary<string, object?> { ["name"] = "A" }),
                    new TransformationOutputResult(
                        "ctp",
                        new Dictionary<string, object?> { ["name"] = "B" })
                ],
                1);

            await using (var connection =
                await dataSource.OpenConnectionAsync())
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                var rolledBack = await router.DeliverAsync(
                    compiled.CompiledDestination!,
                    envelope,
                    connection,
                    transaction,
                    CancellationToken.None);
                Assert.Equal("Done", rolledBack.CompletionStatus);
                await transaction.RollbackAsync();
            }

            await AssertCountsAsync(dataSource, 0, 0);

            await using (var connection =
                await dataSource.OpenConnectionAsync())
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                var scheduled = await router.DeliverAsync(
                    compiled.CompiledDestination!,
                    envelope,
                    connection,
                    transaction,
                    CancellationToken.None);
                Assert.Equal("Done", scheduled.CompletionStatus);
                await transaction.CommitAsync();
            }

            await AssertCountsAsync(dataSource, 2, 2);
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                var duplicate = await router.DeliverAsync(
                    compiled.CompiledDestination!,
                    envelope,
                    connection,
                    transaction,
                    CancellationToken.None);
                Assert.Equal("Done", duplicate.CompletionStatus);
                await transaction.CommitAsync();
            }

            await AssertCountsAsync(dataSource, 2, 2);
            await using var verify = await dataSource.OpenConnectionAsync();
            var jobs = (await verify.QueryAsync<ScheduledRow>("""
                SELECT
                    id AS "Id",
                    output_key AS "OutputKey",
                    idempotency_key AS "IdempotencyKey",
                    payload_ciphertext AS "Ciphertext",
                    payload_hash AS "PayloadHash"
                FROM delivery_jobs
                ORDER BY output_key;
                """)).ToArray();
            Assert.Equal("1:1:100:ctp", jobs[0].IdempotencyKey);
            Assert.Equal("1:1:100:pmx", jobs[1].IdempotencyKey);
            Assert.All(jobs, job =>
            {
                Assert.StartsWith("sha256:", job.PayloadHash);
                Assert.DoesNotContain(
                    "name",
                    Encoding.UTF8.GetString(job.Ciphertext));
            });

            var http = new SequenceHttpAdapter(
                new DeliverySendResult(
                    503,
                    false,
                    null,
                    "stub-1",
                    "Partner returned HTTP 503."),
                new DeliverySendResult(
                    202,
                    false,
                    null,
                    "stub-2",
                    null));
            var profile = new ApiDestinationProfile(
                "pa-mtls",
                "PA",
                true,
                "https://contracts.example.test",
                "/schemas/{contractReference}",
                "https://delivery.example.test",
                "/proposals/{contractVersion}",
                "POST",
                ["X-Correlation-Id", "Idempotency-Key"],
                string.Empty,
                10,
                [new SupportedContract("proposal", "1", "proposal/1")],
                [],
                "None",
                "Idempotency-Key",
                "X-Correlation-Id");
            var handler = new DeliveryJobHandler(
                dataSource,
                new ApiDestinationProfileCatalog([profile]),
                protector,
                http,
                new DeliveryControlReader(dataSource),
                NullLogger<DeliveryJobHandler>.Instance);

            await handler.HandleAsync(jobs[0].Id, CancellationToken.None);
            Assert.Equal(
                retryBaseline.GetProperty("requestsPerAttempt").GetInt32(),
                http.Requests.Count);
            Assert.Equal(
                retryBaseline.GetProperty("payloadUtf8").GetString(),
                Encoding.UTF8.GetString(http.Requests[0].Payload));
            var retryable = await verify.QuerySingleAsync<JobState>("""
                SELECT status AS "Status", attempts AS "Attempts"
                FROM delivery_jobs WHERE id = @Id;
                """, new { jobs[0].Id });
            Assert.Equal("RetryableFailed", retryable.Status);
            Assert.Equal(1, retryable.Attempts);
            await verify.ExecuteAsync("""
                UPDATE delivery_jobs SET next_attempt_at = now()
                WHERE id = @Id;
                """, new { jobs[0].Id });

            await handler.HandleAsync(jobs[0].Id, CancellationToken.None);
            var delivered = await verify.QuerySingleAsync<JobState>("""
                SELECT status AS "Status", attempts AS "Attempts"
                FROM delivery_jobs WHERE id = @Id;
                """, new { jobs[0].Id });
            Assert.Equal("Delivered", delivered.Status);
            Assert.Equal(2, delivered.Attempts);
            Assert.Equal(
                retryBaseline.GetProperty("requestsPerAttempt").GetInt32() * 2,
                http.Requests.Count);
            Assert.Equal(
                http.Requests[0].IdempotencyKey,
                http.Requests[1].IdempotencyKey);
            Assert.Equal(http.Requests[0].Payload, http.Requests[1].Payload);
            Assert.All(http.Requests, request => Assert.Equal(
                retryBaseline.GetProperty("payloadUtf8").GetString(),
                Encoding.UTF8.GetString(request.Payload)));
            var attempts = (await verify.QueryAsync<AttemptAudit>("""
                SELECT
                    payload_hash AS "PayloadHash",
                    idempotency_key AS "IdempotencyKey"
                FROM delivery_attempts
                WHERE delivery_job_id = @Id
                ORDER BY attempt_no;
                """, new { jobs[0].Id })).ToArray();
            Assert.Equal(2, attempts.Length);
            Assert.Single(attempts.Select(item => item.PayloadHash).Distinct());
            Assert.Single(attempts.Select(item => item.IdempotencyKey).Distinct());

            var actions = new DeliveryJobService(dataSource);
            var deliveredRetry = await actions.RetryAsync(
                jobs[0].Id,
                CancellationToken.None);
            Assert.Equal(
                DeliveryActionOutcome.Conflict,
                deliveredRetry.Outcome);

            var resend = await actions.ResendAsync(
                jobs[0].Id,
                "11111111-1111-1111-1111-111111111111",
                "operator-1",
                "Business-approved replay",
                CancellationToken.None);
            Assert.Equal(DeliveryActionOutcome.Accepted, resend.Outcome);
            var resendRow = await verify.QuerySingleAsync<ResendRow>("""
                SELECT
                    resend_sequence AS "ResendSequence",
                    idempotency_key AS "IdempotencyKey",
                    payload_hash AS "PayloadHash",
                    payload_ciphertext AS "Ciphertext",
                    operation_kind AS "OperationKind"
                FROM delivery_jobs
                WHERE id = @Id;
                """, new { Id = resend.DeliveryJobId });
            Assert.Equal(1, resendRow.ResendSequence);
            Assert.EndsWith(":resend:1", resendRow.IdempotencyKey);
            Assert.Equal(jobs[0].PayloadHash, resendRow.PayloadHash);
            Assert.Equal(jobs[0].Ciphertext, resendRow.Ciphertext);
            Assert.Equal("Resend", resendRow.OperationKind);
            Assert.Equal(
                1,
                await verify.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM delivery_resend_audit
                    WHERE resend_delivery_job_id = @Id
                      AND requested_by = 'operator-1';
                    """, new { Id = resend.DeliveryJobId }));
            var duplicateResend = await actions.ResendAsync(
                jobs[0].Id,
                "11111111-1111-1111-1111-111111111111",
                "operator-1",
                "Business-approved replay",
                CancellationToken.None);
            Assert.Equal(resend.DeliveryJobId, duplicateResend.DeliveryJobId);
            Assert.Equal(
                1,
                await verify.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM delivery_jobs
                    WHERE resend_of_delivery_job_id = @Id;
                    """, new { Id = jobs[0].Id }));

            await verify.ExecuteAsync("""
                UPDATE delivery_jobs
                SET status = 'RetryableFailed', attempts = 1,
                    dispatch_scheduled_at = NULL
                WHERE id = @Id;
                """, new { jobs[1].Id });
            var beforeRetry = await verify.QuerySingleAsync<RetrySnapshot>("""
                SELECT
                    payload_hash AS "PayloadHash",
                    payload_ciphertext AS "Ciphertext",
                    idempotency_key AS "IdempotencyKey"
                FROM delivery_jobs WHERE id = @Id;
                """, new { jobs[1].Id });
            var manualRetry = await actions.RetryAsync(
                jobs[1].Id,
                CancellationToken.None);
            Assert.Equal(DeliveryActionOutcome.Accepted, manualRetry.Outcome);
            var afterRetry = await verify.QuerySingleAsync<RetrySnapshot>("""
                SELECT
                    payload_hash AS "PayloadHash",
                    payload_ciphertext AS "Ciphertext",
                    idempotency_key AS "IdempotencyKey"
                FROM delivery_jobs WHERE id = @Id;
                """, new { jobs[1].Id });
            Assert.Equal(beforeRetry.PayloadHash, afterRetry.PayloadHash);
            Assert.Equal(beforeRetry.Ciphertext, afterRetry.Ciphertext);
            Assert.Equal(beforeRetry.IdempotencyKey, afterRetry.IdempotencyKey);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task SeedAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO table_definitions (name, kind)
            VALUES ('source_rows', 'Source');
            INSERT INTO mapping_configs (
                name, input_folder, source_table_id, normalized_table_id)
            SELECT 'config', 'input/config', id, NULL
            FROM table_definitions
            WHERE name = 'source_rows';
            INSERT INTO mapping_config_versions (
                config_id,
                version_no,
                input_layout,
                transformation_program,
                destination_definition,
                accepted_extensions)
            SELECT
                id,
                1,
                '{}'::jsonb,
                '{}'::jsonb,
                '{"kind":"apiContract","apiDestinationProfileId":"pa-mtls","contract":{"id":"proposal","version":"1","reference":"proposal/1","schemaHash":"test"}}'::jsonb,
                '[".csv"]'::jsonb
            FROM mapping_configs;
            INSERT INTO file_jobs (
                config_id,
                config_version_id,
                original_path,
                file_name,
                intake_key,
                import_status,
                archive_status)
            SELECT config_id, id, 'input/a.csv', 'a.csv', 'a',
                'Imported', 'Archived'
            FROM mapping_config_versions;
            INSERT INTO row_jobs (
                file_job_id,
                source_row_id,
                row_number,
                config_version_id,
                kind,
                status)
            SELECT file_jobs.id, 100, 1, file_jobs.config_version_id,
                'Initial', 'Pending'
            FROM file_jobs;
            """);
    }

    private static JsonDocument LoadCompatibilityFixture()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Phase25",
            "compatibility-baseline.golden.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static async Task AssertCountsAsync(
        NpgsqlDataSource dataSource,
        int jobs,
        int outbox)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        Assert.Equal(
            jobs,
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM delivery_jobs;"));
        Assert.Equal(
            outbox,
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM outbox WHERE topic = 'mapping.api-delivery';"));
    }

    private sealed class TestSecretProvider(string value)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }

    private sealed record ScheduledRow(
        long Id,
        string OutputKey,
        string IdempotencyKey,
        byte[] Ciphertext,
        string PayloadHash);

    private sealed record JobState(string Status, int Attempts);

    private sealed record AttemptAudit(
        string PayloadHash,
        string IdempotencyKey);

    private sealed record ResendRow(
        int ResendSequence,
        string IdempotencyKey,
        string PayloadHash,
        byte[] Ciphertext,
        string OperationKind);

    private sealed record RetrySnapshot(
        string PayloadHash,
        byte[] Ciphertext,
        string IdempotencyKey);

    private sealed class SequenceHttpAdapter(
        params DeliverySendResult[] results) : IDeliveryHttpAdapter
    {
        private readonly Queue<DeliverySendResult> _results = new(results);

        public List<DeliverySendRequest> Requests { get; } = [];

        public ValueTask<DeliverySendResult> SendAsync(
            ApiDestinationProfile profile,
            DeliverySendRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request with { Payload = request.Payload.ToArray() });
            return ValueTask.FromResult(_results.Dequeue());
        }
    }
}
