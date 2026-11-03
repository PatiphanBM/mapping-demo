using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using MappingDemo.Worker.Delivery;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonFileFinalizerIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Publishes_empty_array_when_every_row_was_filtered()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values: []);

        Assert.Equal("[]", Encoding.UTF8.GetString(result.Bytes));
        Assert.Equal(0, result.Job.ObjectCount);
        Assert.Equal(2, result.Job.ByteCount);
        Assert.Equal(
            "sha256:4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945",
            result.Job.Sha256);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Publishes_pinned_indented_format_with_unicode_and_scalars()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "indented",
            values:
            [
                new(10, 100, 1, 0, "only", """
                    {"name":"หนึ่ง","nullable":null,"amount":12.50,"active":true,"date":"2026-10-22"}
                    """)
            ]);

        var expected = "[\n  {\n    \"name\": \"หนึ่ง\",\n    \"nullable\": null,\n    \"amount\": 12.50,\n    \"active\": true,\n    \"date\": \"2026-10-22\"\n  }\n]";
        Assert.Equal(
            Encoding.UTF8.GetBytes(expected),
            result.Bytes);
        Assert.Equal(1, result.Job.ObjectCount);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Final_path_is_invisible_until_atomic_publish_and_parallel_claim_is_single()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var schema = $"json_atomic_{Guid.NewGuid():N}"[..34];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "mapping-demo-json-finalizer",
            Guid.NewGuid().ToString("N"));
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
            var protector = Protector();
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await SeedAsync(
                    connection,
                    protector,
                    [new(10, 100, 1, 0, "only", "{\"name\":\"ready\"}")]);
            }

            var publisher = new PausingPublisher();
            var first = new JsonFileFinalizer(
                dataSource,
                Profiles(outputRoot),
                protector,
                publisher,
                NullLogger<JsonFileFinalizer>.Instance);
            var second = new JsonFileFinalizer(
                dataSource,
                Profiles(outputRoot),
                protector,
                publisher,
                NullLogger<JsonFileFinalizer>.Instance);
            var firstRun = first.HandleAsync(1);
            await publisher.DocumentWritten.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            var finalPath = Path.Combine(
                outputRoot,
                "exports",
                "result.json");
            Assert.False(File.Exists(finalPath));
            await using (var duringPublish =
                await dataSource.OpenConnectionAsync())
            {
                var lease = await duringPublish.QuerySingleAsync<ActiveLease>(
                    """
                    SELECT
                        status AS "Status",
                        lease_owner AS "LeaseOwner",
                        lease_expires_at AS "LeaseExpiresAt"
                    FROM json_export_jobs
                    WHERE id = 1;
                    """);
                Assert.Equal("Finalizing", lease.Status);
                Assert.False(string.IsNullOrWhiteSpace(lease.LeaseOwner));
                Assert.True(lease.LeaseExpiresAt > DateTime.UtcNow);
            }
            await second.HandleAsync(1);
            Assert.False(File.Exists(finalPath));

            publisher.Release.TrySetResult();
            await firstRun;

            Assert.True(File.Exists(finalPath));
            using var document = JsonDocument.Parse(
                await File.ReadAllBytesAsync(finalPath));
            Assert.Equal(
                "ready",
                document.RootElement[0].GetProperty("name").GetString());
            await using var verify = await dataSource.OpenConnectionAsync();
            Assert.Equal(
                1,
                await verify.ExecuteScalarAsync<int>("""
                    SELECT attempt_count
                    FROM json_export_jobs
                    WHERE id = 1;
                    """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Rejects_object_over_runtime_limit_before_publish()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var oversizedJson = "{\"value\":\"" +
            new string('x', JsonFileFinalizer.MaxObjectBytes) +
            "\"}";
        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "oversized", oversizedJson)
            ],
            expectFinal: false);

        Assert.Equal("Blocked", result.Job.Status);
        Assert.Equal(
            "A staged JSON export object exceeds the 1 MiB limit.",
            result.Job.LastErrorRedacted);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Does_not_claim_when_latest_row_job_is_not_terminal()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var schema = $"json_not_ready_{Guid.NewGuid():N}"[..36];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "mapping-demo-json-finalizer",
            Guid.NewGuid().ToString("N"));
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
            var protector = Protector();
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await SeedAsync(
                    connection,
                    protector,
                    [new(10, 100, 1, 0, "old", "{\"name\":\"old\"}")]);
                await connection.ExecuteAsync("""
                    INSERT INTO row_jobs (
                        id, file_job_id, source_row_id, row_number,
                        config_version_id, kind, status)
                    OVERRIDING SYSTEM VALUE
                    VALUES (20, 1, 100, 1, 1, 'Reprocess', 'Pending');
                    """);
            }

            var finalizer = new JsonFileFinalizer(
                dataSource,
                Profiles(outputRoot),
                protector,
                new FileSystemJsonExportPublisher(),
                NullLogger<JsonFileFinalizer>.Instance);
            await finalizer.HandleAsync(1);

            Assert.False(File.Exists(Path.Combine(
                outputRoot,
                "exports",
                "result.json")));
            await using var verify = await dataSource.OpenConnectionAsync();
            var job = await verify.QuerySingleAsync<(string Status, int Attempts)>(
                """
                SELECT status AS "Status", attempt_count AS "Attempts"
                FROM json_export_jobs
                WHERE id = 1;
                """);
            Assert.Equal("Pending", job.Status);
            Assert.Equal(0, job.Attempts);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Failed")]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Blocks_when_a_latest_row_job_failed(
        string rowStatus)
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "old", "{\"name\":\"old\"}")
            ],
            arrangeAsync: connection => connection.ExecuteAsync("""
                UPDATE row_jobs
                SET status = @RowStatus
                WHERE id = 10;
                """, new { RowStatus = rowStatus }),
            expectFinal: false);

        Assert.False(result.FinalExists);
        Assert.Equal("Blocked", result.Job.Status);
        Assert.Equal(
            $"Latest row jobs contain 0 Invalid and {(rowStatus == "Failed" ? 1 : 0)} Failed outcomes."
                .Replace(
                    "0 Invalid",
                    rowStatus == "Invalid" ? "1 Invalid" : "0 Invalid",
                    StringComparison.Ordinal),
            result.Job.LastErrorRedacted);
        Assert.NotNull(result.Job.TerminalAt);
        Assert.NotNull(result.Job.PayloadExpiresAt);
        Assert.Null(result.Job.ArtifactExpiresAt);
        Assert.NotNull(result.Job.AuditExpiresAt);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Import_readiness_creates_and_blocks_all_invalid_export()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "invalid", "{\"ignored\":true}")
            ],
            arrangeAsync: async connection =>
            {
                await connection.ExecuteAsync("""
                    DELETE FROM json_export_jobs;
                    UPDATE row_jobs SET status = 'Invalid' WHERE id = 10;
                    """);
                await using var transaction =
                    await connection.BeginTransactionAsync();
                await new JsonExportReadiness().TryScheduleAsync(
                    fileJobId: 1,
                    currentRowJobId: 10,
                    currentCompletionStatus: "Invalid",
                    connection,
                    transaction);
                await transaction.CommitAsync();
            },
            expectFinal: false,
            useRecoveryScheduler: true);

        Assert.Equal("Blocked", result.Job.Status);
        Assert.Equal(
            "Latest row jobs contain 1 Invalid and 0 Failed outcomes.",
            result.Job.LastErrorRedacted);
        Assert.Equal(0, result.Job.AttemptCount);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Reclaims_stale_finalization_lease_with_same_staged_payload()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "only", "{\"name\":\"recovered\"}")
            ],
            arrangeAsync: connection => connection.ExecuteAsync("""
                UPDATE json_export_jobs
                SET
                    status = 'Finalizing',
                    lease_owner = 'dead-worker',
                    lease_expires_at = now() - interval '1 minute'
                WHERE id = 1;
                """),
            useRecoveryScheduler: true);

        Assert.True(result.FinalExists);
        Assert.Equal("Published", result.Job.Status);
        using var document = JsonDocument.Parse(result.Bytes);
        Assert.Equal(
            "recovered",
            document.RootElement[0].GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Retries_failure_before_rename_without_partial_artifact()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "only", "{\"name\":\"retried\"}")
            ],
            publisher: new FailOnceBeforeRenamePublisher(),
            handleCount: 2);

        Assert.Equal("Published", result.Job.Status);
        Assert.Equal(2, result.Job.AttemptCount);
        Assert.False(result.TempExists);
        using var document = JsonDocument.Parse(result.Bytes);
        Assert.Equal(
            "retried",
            document.RootElement[0].GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Reconciles_crash_after_rename_without_duplicate_artifact()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "only", "{\"name\":\"reconciled\"}")
            ],
            publisher: new FailOnceAfterPublishPublisher(),
            handleCount: 2);

        Assert.Equal("Published", result.Job.Status);
        Assert.Equal(2, result.Job.AttemptCount);
        Assert.False(result.TempExists);
        Assert.Equal(
            "sha256:" + Convert.ToHexString(
                SHA256.HashData(result.Bytes)).ToLowerInvariant(),
            result.Job.Sha256);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Blocks_final_path_collision_when_content_differs()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var existing = Encoding.UTF8.GetBytes("[{\"owner\":\"other\"}]");
        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values:
            [
                new(10, 100, 1, 0, "only", "{\"owner\":\"this-job\"}")
            ],
            arrangeOutputAsync: async outputRoot =>
            {
                var finalPath = Path.Combine(
                    outputRoot,
                    "exports",
                    "result.json");
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                await File.WriteAllBytesAsync(finalPath, existing);
            });

        Assert.Equal(existing, result.Bytes);
        Assert.Equal("Blocked", result.Job.Status);
        Assert.Equal(
            "The final JSON artifact already exists with different content.",
            result.Job.LastErrorRedacted);
        Assert.Null(result.Job.Sha256);
        Assert.False(result.TempExists);
        Assert.NotNull(result.Job.PayloadExpiresAt);
        Assert.Null(result.Job.ArtifactExpiresAt);
        Assert.NotNull(result.Job.AuditExpiresAt);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Streams_large_ordered_artifact_with_exact_byte_hash()
    {
        var connectionString = RequiredConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var padding = new string('x', 4096);
        var values = Enumerable.Range(1, 128)
            .Reverse()
            .Select(number => new StagedValue(
                RowJobId: 1000 + number,
                SourceRowId: 2000 + number,
                RowNumber: number,
                OutputOrdinal: 0,
                OutputKey: $"output-{number:D3}",
                Json: $"{{\"index\":{number},\"text\":\"{padding}\"}}"))
            .ToArray();

        var result = await FinalizeAsync(
            connectionString,
            formatting: "compact",
            values: values);

        Assert.True(result.Bytes.Length > 512 * 1024);
        using var document = JsonDocument.Parse(result.Bytes);
        Assert.Equal(128, document.RootElement.GetArrayLength());
        Assert.Equal(1, document.RootElement[0].GetProperty("index").GetInt32());
        Assert.Equal(
            128,
            document.RootElement[127].GetProperty("index").GetInt32());
        Assert.Equal(128, result.Job.ObjectCount);
        Assert.Equal(result.Bytes.Length, result.Job.ByteCount);
        Assert.Equal(
            "sha256:" + Convert.ToHexString(
                SHA256.HashData(result.Bytes)).ToLowerInvariant(),
            result.Job.Sha256);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Publishes_ordered_compact_utf8_and_records_metadata()
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

        var schema = $"json_finalize_{Guid.NewGuid():N}"[..36];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "mapping-demo-json-finalizer",
            Guid.NewGuid().ToString("N"));
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

            var protector = Protector();
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await SeedAsync(
                    connection,
                    protector,
                    [
                        new(11, 101, 2, 0, "third", """
                            {"name":"สาม","nullable":null,"amount":0,"active":false,"date":"2026-10-24"}
                            """),
                        new(10, 100, 1, 1, "second", """
                            {"name":"สอง","nullable":null,"amount":0.5,"active":false,"date":"2026-10-23"}
                            """),
                        new(10, 100, 1, 0, "first", """
                            {"name":"หนึ่ง","nullable":null,"amount":12.50,"active":true,"date":"2026-10-22"}
                            """)
                    ]);
            }

            var finalizer = new JsonFileFinalizer(
                dataSource,
                Profiles(outputRoot),
                protector,
                new FileSystemJsonExportPublisher(),
                NullLogger<JsonFileFinalizer>.Instance);

            await finalizer.HandleAsync(1);

            var expected = """
                [{"name":"หนึ่ง","nullable":null,"amount":12.50,"active":true,"date":"2026-10-22"},{"name":"สอง","nullable":null,"amount":0.5,"active":false,"date":"2026-10-23"},{"name":"สาม","nullable":null,"amount":0,"active":false,"date":"2026-10-24"}]
                """;
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var finalPath = Path.Combine(outputRoot, "exports", "result.json");
            var actualBytes = await File.ReadAllBytesAsync(finalPath);
            Assert.Equal(expectedBytes, actualBytes);
            Assert.Equal((byte)'[', actualBytes[0]);

            await using var verify = await dataSource.OpenConnectionAsync();
            var job = await verify.QuerySingleAsync<PublishedJob>("""
                SELECT
                    status AS "Status",
                    object_count AS "ObjectCount",
                    byte_count AS "ByteCount",
                    sha256 AS "Sha256",
                    lease_owner AS "LeaseOwner",
                    lease_expires_at AS "LeaseExpiresAt",
                    published_at AS "PublishedAt",
                    terminal_at AS "TerminalAt",
                    last_error_redacted AS "LastErrorRedacted",
                    attempt_count AS "AttemptCount",
                    payload_expires_at AS "PayloadExpiresAt",
                    artifact_expires_at AS "ArtifactExpiresAt",
                    audit_expires_at AS "AuditExpiresAt"
                FROM json_export_jobs
                WHERE id = 1;
                """);
            Assert.Equal("Published", job.Status);
            Assert.Equal(3, job.ObjectCount);
            Assert.Equal(expectedBytes.Length, job.ByteCount);
            Assert.Equal(
                "sha256:" + Convert.ToHexString(
                    SHA256.HashData(expectedBytes)).ToLowerInvariant(),
                job.Sha256);
            Assert.Null(job.LeaseOwner);
            Assert.Null(job.LeaseExpiresAt);
            Assert.NotNull(job.PublishedAt);
            Assert.NotNull(job.TerminalAt);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static JsonExportPayloadProtector Protector()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JsonExport:PayloadEncryptionKeyReference"] =
                    "json-export-test-key"
            })
            .Build();
        return new JsonExportPayloadProtector(
            new TestSecretProvider(key),
            configuration);
    }

    private static FileDestinationProfileCatalog Profiles(string outputRoot) =>
        new([
            new FileDestinationProfile(
                "local-json-export",
                true,
                Path.GetFullPath(outputRoot),
                "volume-encrypted",
                1024 * 1024 * 1024,
                0,
                DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
                10,
                [new SupportedContract("proposal", "1", "proposal/1")],
                [])
        ]);

    private static async Task SeedAsync(
        NpgsqlConnection connection,
        JsonExportPayloadProtector protector,
        IReadOnlyList<StagedValue> values,
        string formatting = "compact")
    {
        var destination = JsonSerializer.Serialize<DestinationDefinition>(
            new JsonFileDestination(
                "local-json-export",
                new ContractReference(
                    "proposal",
                    "1",
                    "proposal/1",
                    "sha256:test"),
                "exports",
                "result.json",
                formatting),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await connection.ExecuteAsync("""
            INSERT INTO table_definitions (id, name, kind)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'source_rows', 'Source');
            INSERT INTO mapping_configs (
                id, name, input_folder, source_table_id,
                normalized_table_id)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'json', 'json-in', 1, NULL);
            INSERT INTO mapping_config_versions (
                id, config_id, version_no, input_layout,
                transformation_program, destination_definition)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, '{}'::jsonb, '{}'::jsonb,
                CAST(@Destination AS jsonb));
            INSERT INTO file_jobs (
                id, config_id, config_version_id, original_path,
                file_name, intake_key, import_status, archive_status,
                total_rows)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, 'input/source.csv', 'source.csv',
                'intake-1', 'Imported', 'Archived', @TotalRows);
            INSERT INTO json_export_jobs (
                id, file_job_id, config_version_id,
                destination_profile_id, artifact_revision, status,
                relative_temp_path, relative_final_path)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, 'local-json-export', 1, 'Pending',
                'exports/result.json.tmp', 'exports/result.json');
            """, new
        {
            Destination = destination,
            TotalRows = values
                .Select(value => value.SourceRowId)
                .Distinct()
                .Count()
        });

        foreach (var group in values.GroupBy(value => value.RowJobId))
        {
            var first = group.First();
            await connection.ExecuteAsync("""
                INSERT INTO row_jobs (
                    id, file_job_id, source_row_id, row_number,
                    config_version_id, kind, status, finished_at)
                OVERRIDING SYSTEM VALUE
                VALUES (
                    @RowJobId, 1, @SourceRowId, @RowNumber,
                    1, 'Initial', 'Done', now());
                """, first);
        }

        foreach (var value in values)
        {
            var plaintext = Encoding.UTF8.GetBytes(value.Json);
            var protectedPayload = await protector.ProtectAsync(
                plaintext,
                CancellationToken.None);
            await connection.ExecuteAsync("""
                INSERT INTO json_export_items (
                    json_export_job_id, file_job_id, config_version_id,
                    artifact_revision, row_job_id, source_row_id,
                    row_number, output_key, output_ordinal,
                    payload_ciphertext, payload_nonce, payload_tag,
                    encryption_key_reference, payload_sha256)
                VALUES (
                    1, 1, 1, 1, @RowJobId, @SourceRowId,
                    @RowNumber, @OutputKey, @OutputOrdinal,
                    @Ciphertext, @Nonce, @Tag, @KeyReference, @PayloadSha256);
                """, new
            {
                value.RowJobId,
                value.SourceRowId,
                value.RowNumber,
                value.OutputKey,
                value.OutputOrdinal,
                protectedPayload.Ciphertext,
                protectedPayload.Nonce,
                protectedPayload.Tag,
                protectedPayload.KeyReference,
                PayloadSha256 = "sha256:" + Convert.ToHexString(
                    SHA256.HashData(plaintext)).ToLowerInvariant()
            });
        }
    }

    private sealed class TestSecretProvider(string value)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }

    private sealed class PausingPublisher : IJsonExportPublisher
    {
        private readonly FileSystemJsonExportPublisher _inner = new();

        public TaskCompletionSource DocumentWritten { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PublishAsync(
            FileDestinationProfile profile,
            string relativeTempPath,
            string relativeFinalPath,
            Func<Stream, CancellationToken, Task> writeAsync,
            CancellationToken cancellationToken = default) =>
            _inner.PublishAsync(
                profile,
                relativeTempPath,
                relativeFinalPath,
                async (stream, token) =>
                {
                    await writeAsync(stream, token);
                    DocumentWritten.TrySetResult();
                    await Release.Task.WaitAsync(token);
                },
                cancellationToken);
    }

    private sealed class FailOnceBeforeRenamePublisher : IJsonExportPublisher
    {
        private readonly FileSystemJsonExportPublisher _inner = new();
        private int _attempt;

        public async Task PublishAsync(
            FileDestinationProfile profile,
            string relativeTempPath,
            string relativeFinalPath,
            Func<Stream, CancellationToken, Task> writeAsync,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _attempt) == 1)
            {
                var tempPath = Path.Combine(
                    profile.OutputRoot,
                    relativeTempPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                await using var stream = File.Create(tempPath);
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                throw new IOException("Simulated failure before rename.");
            }

            await _inner.PublishAsync(
                profile,
                relativeTempPath,
                relativeFinalPath,
                writeAsync,
                cancellationToken);
        }
    }

    private sealed class FailOnceAfterPublishPublisher : IJsonExportPublisher
    {
        private readonly FileSystemJsonExportPublisher _inner = new();
        private int _attempt;

        public async Task PublishAsync(
            FileDestinationProfile profile,
            string relativeTempPath,
            string relativeFinalPath,
            Func<Stream, CancellationToken, Task> writeAsync,
            CancellationToken cancellationToken = default)
        {
            await _inner.PublishAsync(
                profile,
                relativeTempPath,
                relativeFinalPath,
                writeAsync,
                cancellationToken);
            if (Interlocked.Increment(ref _attempt) == 1)
            {
                throw new IOException(
                    "Simulated crash after rename and before DB update.");
            }
        }
    }

    private static string? RequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return null;
        }

        return connectionString;
    }

    private static async Task<FinalizedResult> FinalizeAsync(
        string connectionString,
        string formatting,
        IReadOnlyList<StagedValue> values,
        Func<NpgsqlConnection, Task>? arrangeAsync = null,
        bool expectFinal = true,
        IJsonExportPublisher? publisher = null,
        int handleCount = 1,
        Func<string, Task>? arrangeOutputAsync = null,
        bool useRecoveryScheduler = false)
    {
        var schema = $"json_finalize_{Guid.NewGuid():N}"[..36];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "mapping-demo-json-finalizer",
            Guid.NewGuid().ToString("N"));
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
            var protector = Protector();
            await using (var connection =
                await dataSource.OpenConnectionAsync())
            {
                await SeedAsync(
                    connection,
                    protector,
                    values,
                    formatting);
                if (arrangeAsync is not null)
                {
                    await arrangeAsync(connection);
                }
            }
            if (arrangeOutputAsync is not null)
            {
                await arrangeOutputAsync(outputRoot);
            }

            var finalizer = new JsonFileFinalizer(
                dataSource,
                Profiles(outputRoot),
                protector,
                publisher ?? new FileSystemJsonExportPublisher(),
                NullLogger<JsonFileFinalizer>.Instance);
            for (var attempt = 0; attempt < handleCount; attempt++)
            {
                if (useRecoveryScheduler)
                {
                    var scheduler = new JsonExportRecoveryScheduler(
                        dataSource,
                        finalizer,
                        NullLogger<JsonExportRecoveryScheduler>.Instance);
                    await scheduler.RecoverOnceAsync();
                }
                else
                {
                    await finalizer.HandleAsync(1);
                }
            }
            var finalPath = Path.Combine(
                outputRoot,
                "exports",
                "result.json");
            var tempPath = finalPath + ".tmp";
            var finalExists = File.Exists(finalPath);
            if (expectFinal)
            {
                Assert.True(finalExists);
            }
            var bytes = finalExists
                ? await File.ReadAllBytesAsync(finalPath)
                : [];
            await using var verify = await dataSource.OpenConnectionAsync();
            var job = await verify.QuerySingleAsync<PublishedJob>("""
                SELECT
                    status AS "Status",
                    object_count AS "ObjectCount",
                    byte_count AS "ByteCount",
                    sha256 AS "Sha256",
                    lease_owner AS "LeaseOwner",
                    lease_expires_at AS "LeaseExpiresAt",
                    published_at AS "PublishedAt",
                    terminal_at AS "TerminalAt",
                    last_error_redacted AS "LastErrorRedacted",
                    attempt_count AS "AttemptCount",
                    payload_expires_at AS "PayloadExpiresAt",
                    artifact_expires_at AS "ArtifactExpiresAt",
                    audit_expires_at AS "AuditExpiresAt"
                FROM json_export_jobs
                WHERE id = 1;
                """);
            return new FinalizedResult(
                bytes,
                job,
                finalExists,
                File.Exists(tempPath));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private sealed record StagedValue(
        long RowJobId,
        long SourceRowId,
        int RowNumber,
        int OutputOrdinal,
        string OutputKey,
        string Json);

    private sealed record PublishedJob(
        string Status,
        int? ObjectCount,
        long? ByteCount,
        string? Sha256,
        string? LeaseOwner,
        DateTime? LeaseExpiresAt,
        DateTime? PublishedAt,
        DateTime? TerminalAt,
        string? LastErrorRedacted,
        int AttemptCount,
        DateTime? PayloadExpiresAt,
        DateTime? ArtifactExpiresAt,
        DateTime? AuditExpiresAt);

    private sealed record FinalizedResult(
        byte[] Bytes,
        PublishedJob Job,
        bool FinalExists,
        bool TempExists);

    private sealed record ActiveLease(
        string Status,
        string LeaseOwner,
        DateTime LeaseExpiresAt);
}
