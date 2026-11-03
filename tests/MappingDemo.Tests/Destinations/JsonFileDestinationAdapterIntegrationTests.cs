using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Worker.Delivery;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonFileDestinationAdapterIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Row_completion_and_encrypted_stage_commit_atomically()
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

        var schema = $"json_stage_atomic_{Guid.NewGuid():N}"[..36];
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
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedAsync(connection, rowCount: 1);

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var protector = new JsonExportPayloadProtector(
                new TestSecretProvider(key),
                Configuration());
            var router = DestinationRouterTestFactory.Create(
                new JsonFileDestinationAdapter(
                    new PayloadBindingModule(),
                    protector,
                    new JsonExportReadiness()));
            var compiled = Compile(router);
            var envelope = Envelope(
                rowJobId: 10,
                sourceRowId: 100,
                rowNumber: 1,
                [Output("pmx", "สมชาย")]);

            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                var delivered = await router.DeliverAsync(
                    compiled,
                    envelope,
                    connection,
                    transaction);
                Assert.Equal("Done", delivered.CompletionStatus);
                await CompleteAsync(connection, transaction, 10, "Done");
                await transaction.RollbackAsync();
            }

            await AssertCountsAsync(connection, 0, 0, 0);
            Assert.Equal(
                "Pending",
                await connection.ExecuteScalarAsync<string>("""
                    SELECT status FROM row_jobs WHERE id = 10;
                    """));

            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                var delivered = await router.DeliverAsync(
                    compiled,
                    envelope,
                    connection,
                    transaction);
                Assert.Equal("Done", delivered.CompletionStatus);
                await CompleteAsync(connection, transaction, 10, "Done");
                await transaction.CommitAsync();
            }

            await AssertCountsAsync(connection, 1, 1, 1);
            var item = await connection.QuerySingleAsync<StagedItem>("""
                SELECT
                    output_key AS "OutputKey",
                    output_ordinal AS "OutputOrdinal",
                    payload_ciphertext AS "Ciphertext",
                    payload_nonce AS "Nonce",
                    payload_tag AS "Tag",
                    encryption_key_reference AS "KeyReference",
                    payload_sha256 AS "PayloadHash"
                FROM json_export_items;
                """);
            Assert.Equal("pmx", item.OutputKey);
            Assert.Equal(0, item.OutputOrdinal);
            var plaintext = await protector.UnprotectAsync(
                item.Ciphertext,
                item.Nonce,
                item.Tag,
                item.KeyReference,
                CancellationToken.None);
            using var document = JsonDocument.Parse(plaintext);
            Assert.Equal(
                "สมชาย",
                document.RootElement.GetProperty("name").GetString());
            Assert.Equal(
                "sha256:" + Convert.ToHexString(SHA256.HashData(plaintext))
                    .ToLowerInvariant(),
                item.PayloadHash);
            Assert.Equal(
                0,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM delivery_jobs;"));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Reprocess_reconciles_one_source_row_from_two_to_one_to_zero_outputs()
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

        var schema = $"json_stage_reconcile_{Guid.NewGuid():N}"[..39];
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
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedAsync(connection, rowCount: 1);

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var router = DestinationRouterTestFactory.Create(
                new JsonFileDestinationAdapter(
                    new PayloadBindingModule(),
                    new JsonExportPayloadProtector(
                        new TestSecretProvider(key),
                        Configuration()),
                    new JsonExportReadiness()));
            var compiled = Compile(router);

            await DeliverAndCompleteAsync(
                router,
                compiled,
                connection,
                Envelope(
                    rowJobId: 10,
                    sourceRowId: 100,
                    rowNumber: 1,
                    [Output("pmx", "A"), Output("ctp", "B")]));
            Assert.Equal(
                ["pmx", "ctp"],
                await OutputKeysAsync(connection));

            await InsertReprocessRowJobAsync(connection, 20);
            await DeliverAndCompleteAsync(
                router,
                compiled,
                connection,
                Envelope(
                    rowJobId: 20,
                    sourceRowId: 100,
                    rowNumber: 1,
                    [Output("ctp", "C")],
                    operationKind: "Reprocess"));
            Assert.Equal(["ctp"], await OutputKeysAsync(connection));
            Assert.Equal(
                20,
                await connection.ExecuteScalarAsync<long>("""
                    SELECT row_job_id FROM json_export_items;
                    """));

            await InsertReprocessRowJobAsync(connection, 30);
            await DeliverAndCompleteAsync(
                router,
                compiled,
                connection,
                Envelope(
                    rowJobId: 30,
                    sourceRowId: 100,
                    rowNumber: 1,
                    [],
                    operationKind: "Reprocess"));
            Assert.Empty(await OutputKeysAsync(connection));
            Assert.Equal(
                "Filtered",
                await connection.ExecuteScalarAsync<string>("""
                    SELECT status FROM row_jobs WHERE id = 30;
                    """));
            await AssertCountsAsync(connection, 1, 0, 1);

            await connection.ExecuteAsync("""
                UPDATE json_export_jobs
                SET
                    status = 'Published',
                    terminal_at = now(),
                    published_at = now(),
                    payload_expires_at = now() + interval '30 days',
                    artifact_expires_at = now() + interval '30 days',
                    audit_expires_at = now() + interval '365 days'
                WHERE id = 1;
                """);
            await InsertReprocessRowJobAsync(connection, 40);
            await DeliverAndCompleteAsync(
                router,
                compiled,
                connection,
                Envelope(
                    rowJobId: 40,
                    sourceRowId: 100,
                    rowNumber: 1,
                    [Output("pmx", "revision-2")],
                    operationKind: "Reprocess"));

            var revisions = (await connection.QueryAsync<ExportRevision>("""
                SELECT
                    id AS "Id",
                    artifact_revision AS "ArtifactRevision",
                    relative_final_path AS "RelativeFinalPath",
                    previous_export_job_id AS "PreviousExportJobId",
                    status AS "Status",
                    artifact_expires_at AS "ArtifactExpiresAt",
                    audit_expires_at AS "AuditExpiresAt"
                FROM json_export_jobs
                ORDER BY artifact_revision;
                """)).ToArray();
            Assert.Collection(
                revisions,
                revision =>
                {
                    Assert.Equal(1, revision.ArtifactRevision);
                    Assert.Equal("Published", revision.Status);
                    Assert.Equal("exports/customers.1.r1.json", revision.RelativeFinalPath);
                    Assert.NotNull(revision.ArtifactExpiresAt);
                    Assert.NotNull(revision.AuditExpiresAt);
                },
                revision =>
                {
                    Assert.Equal(2, revision.ArtifactRevision);
                    Assert.Equal(revisions[0].Id, revision.PreviousExportJobId);
                    Assert.Equal("Pending", revision.Status);
                    Assert.Equal("exports/customers.1.r2.json", revision.RelativeFinalPath);
                });
            await AssertCountsAsync(connection, 2, 1, 2);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Parallel_last_rows_create_one_export_job_and_one_finalization()
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

        var schema = $"json_stage_parallel_{Guid.NewGuid():N}"[..38];
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
            await using (var seedConnection =
                await dataSource.OpenConnectionAsync())
            {
                await SeedAsync(seedConnection, rowCount: 2);
            }

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var router = DestinationRouterTestFactory.Create(
                new JsonFileDestinationAdapter(
                    new PayloadBindingModule(),
                    new JsonExportPayloadProtector(
                        new TestSecretProvider(key),
                        Configuration()),
                    new JsonExportReadiness()));
            var compiled = Compile(router);

            async Task DeliverAsync(
                long rowJobId,
                long sourceRowId,
                int rowNumber,
                string outputKey)
            {
                await using var connection =
                    await dataSource.OpenConnectionAsync();
                await DeliverAndCompleteAsync(
                    router,
                    compiled,
                    connection,
                    Envelope(
                        rowJobId,
                        sourceRowId,
                        rowNumber,
                        [Output(outputKey, outputKey)]));
            }

            await Task.WhenAll(
                DeliverAsync(10, 100, 1, "first"),
                DeliverAsync(11, 101, 2, "second"));

            await using var verify = await dataSource.OpenConnectionAsync();
            await AssertCountsAsync(verify, 1, 2, 1);
            Assert.Equal(
                0,
                await verify.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM delivery_jobs;"));
            var export = await verify.QuerySingleAsync<ExportJobRow>("""
                SELECT
                    destination_profile_id AS "ProfileId",
                    artifact_revision AS "ArtifactRevision",
                    relative_temp_path AS "RelativeTempPath",
                    relative_final_path AS "RelativeFinalPath"
                FROM json_export_jobs;
                """);
            Assert.Equal("local-json-export", export.ProfileId);
            Assert.Equal(1, export.ArtifactRevision);
            Assert.Equal(
                "exports/customers.1.r1.json.tmp",
                export.RelativeTempPath);
            Assert.Equal(
                "exports/customers.1.r1.json",
                export.RelativeFinalPath);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Import_completion_schedules_rows_that_finished_while_importing()
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

        var schema = $"json_stage_import_{Guid.NewGuid():N}"[..36];
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
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedAsync(
                connection,
                rowCount: 1,
                importStatus: "Importing");

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var readiness = new JsonExportReadiness();
            var router = DestinationRouterTestFactory.Create(
                new JsonFileDestinationAdapter(
                    new PayloadBindingModule(),
                    new JsonExportPayloadProtector(
                        new TestSecretProvider(key),
                        Configuration()),
                    readiness));
            var compiled = Compile(router);

            await DeliverAndCompleteAsync(
                router,
                compiled,
                connection,
                Envelope(
                    rowJobId: 10,
                    sourceRowId: 100,
                    rowNumber: 1,
                    [Output("pmx", "A")]));
            await AssertCountsAsync(connection, 1, 1, 0);

            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                await connection.ExecuteAsync("""
                    UPDATE file_jobs
                    SET import_status = 'Imported'
                    WHERE id = 1;
                    """, transaction: transaction);
                await readiness.TryScheduleAsync(
                    fileJobId: 1,
                    currentRowJobId: null,
                    currentCompletionStatus: null,
                    connection,
                    transaction);
                await readiness.TryScheduleAsync(
                    fileJobId: 1,
                    currentRowJobId: null,
                    currentCompletionStatus: null,
                    connection,
                    transaction);
                await transaction.CommitAsync();
            }

            await AssertCountsAsync(connection, 1, 1, 1);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static CompiledDestination Compile(IDestinationRouter router)
    {
        var schema = new ContractSchemaModule().Compile("""
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
        Assert.True(schema.IsSuccess);
        var compilation = router.Compile(
            Destination(),
            new DestinationCompilationContext(schema.TargetSchema!, []));
        Assert.True(compilation.IsSuccess);
        return compilation.CompiledDestination!;
    }

    private static JsonFileDestination Destination() => new(
        "local-json-export",
        new ContractReference(
            "proposal",
            "1",
            "proposal/1",
            "sha256:test"),
        "exports",
        "{sourceBaseName}.{fileJobId}.r{artifactRevision}.json",
        "compact");

    private static DeliveryEnvelope Envelope(
        long rowJobId,
        long sourceRowId,
        int rowNumber,
        IReadOnlyList<TransformationOutputResult> outputs,
        string operationKind = "Initial") => new(
            sourceRowId,
            1,
            rowNumber,
            rowJobId,
            1,
            outputs,
            1,
            operationKind);

    private static TransformationOutputResult Output(
        string outputKey,
        string name) => new(
            outputKey,
            new Dictionary<string, object?> { ["name"] = name });

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JsonExport:PayloadEncryptionKeyReference"] =
                    "json-export-test-key"
            })
            .Build();

    private static async Task SeedAsync(
        NpgsqlConnection connection,
        int rowCount,
        string importStatus = "Imported")
    {
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
                1,
                1,
                7,
                '{}'::jsonb,
                '{}'::jsonb,
                CAST(@Destination AS jsonb));
            INSERT INTO file_jobs (
                id, config_id, config_version_id, original_path,
                file_name, intake_key, import_status, archive_status,
                total_rows)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, 'input/customers.csv', 'customers.csv',
                'intake-1', @ImportStatus, 'Archived', @RowCount);
            INSERT INTO row_jobs (
                id, file_job_id, source_row_id, row_number,
                config_version_id, kind, status)
            OVERRIDING SYSTEM VALUE
            SELECT
                9 + value,
                1,
                99 + value,
                value,
                1,
                'Initial',
                'Pending'
            FROM generate_series(1, @RowCount) AS value;
            """, new
        {
            Destination = System.Text.Json.JsonSerializer.Serialize<
                DestinationDefinition>(
                Destination(),
                new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web)),
            RowCount = rowCount,
            ImportStatus = importStatus
        });
    }

    private static Task CompleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rowJobId,
        string status) => connection.ExecuteAsync("""
            UPDATE row_jobs
            SET status = @Status,
                attempts = 1,
                finished_at = now()
            WHERE id = @RowJobId;
            """, new { RowJobId = rowJobId, Status = status }, transaction);

    private static async Task DeliverAndCompleteAsync(
        IDestinationRouter router,
        CompiledDestination destination,
        NpgsqlConnection connection,
        DeliveryEnvelope envelope)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var delivered = await router.DeliverAsync(
            destination,
            envelope,
            connection,
            transaction);
        await CompleteAsync(
            connection,
            transaction,
            envelope.RowJobId,
            delivered.CompletionStatus);
        await transaction.CommitAsync();
    }

    private static Task InsertReprocessRowJobAsync(
        NpgsqlConnection connection,
        long rowJobId) => connection.ExecuteAsync("""
            INSERT INTO row_jobs (
                id, file_job_id, source_row_id, row_number,
                config_version_id, kind, status)
            OVERRIDING SYSTEM VALUE
            VALUES (@RowJobId, 1, 100, 1, 1, 'Reprocess', 'Pending');
            """, new { RowJobId = rowJobId });

    private static async Task<string[]> OutputKeysAsync(
        NpgsqlConnection connection) =>
        (await connection.QueryAsync<string>("""
            SELECT output_key
            FROM json_export_items
            ORDER BY output_ordinal;
            """)).ToArray();

    private static async Task AssertCountsAsync(
        NpgsqlConnection connection,
        int jobs,
        int items,
        int finalizations)
    {
        Assert.Equal(
            jobs,
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM json_export_jobs;"));
        Assert.Equal(
            items,
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::integer FROM json_export_items;"));
        Assert.Equal(
            finalizations,
            await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::integer
                FROM outbox
                WHERE topic = 'mapping.json-export-finalize';
                """));
    }

    private sealed class TestSecretProvider(string value)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }

    private sealed record StagedItem(
        string OutputKey,
        int OutputOrdinal,
        byte[] Ciphertext,
        byte[] Nonce,
        byte[] Tag,
        string KeyReference,
        string PayloadHash);

    private sealed record ExportJobRow(
        string ProfileId,
        int ArtifactRevision,
        string RelativeTempPath,
        string RelativeFinalPath);

    private sealed record ExportRevision(
        long Id,
        int ArtifactRevision,
        string RelativeFinalPath,
        long? PreviousExportJobId,
        string Status,
        DateTime? ArtifactExpiresAt,
        DateTime? AuditExpiresAt);
}
