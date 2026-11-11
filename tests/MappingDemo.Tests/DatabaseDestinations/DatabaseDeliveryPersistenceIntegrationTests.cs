using Dapper;
using MappingDemo.Api.Contracts.DatabaseDestinations;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Messaging;
using MappingDemo.Worker.Delivery;
using MappingDemo.Worker.DatabaseDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.DatabaseDestinations;

public sealed class DatabaseDeliveryPersistenceIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Job_and_outbox_are_atomic_and_payload_is_encrypted()
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

        var schemaName = $"db_delivery_{Guid.NewGuid():N}"[..35];
        var quotedSchema = MappingDemo.Shared.Tables.SqlIdentifier.Quote(
            schemaName);
        await using var adminDataSource = NpgsqlDataSource.Create(connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scoped = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schemaName
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(scoped);
            await new MigrationRunner(dataSource).RunAsync();
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedAsync(connection);

            var module = Module();
            var destination = Destination();
            var intent = Intent(
                destination,
                new Dictionary<string, object?>
                {
                    ["description"] = "TOP-SECRET-DESCRIPTION",
                    ["code"] = "SECRET-CUSTOMER"
                });

            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                Assert.Equal(
                    DatabaseDeliveryIntentPersistenceResult.Persisted,
                    await module.PersistAsync(
                        intent,
                        connection,
                        transaction));
                Assert.Equal(
                    1,
                    await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*)::integer FROM database_delivery_jobs;",
                        transaction: transaction));
                Assert.Equal(
                    1,
                    await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*)::integer FROM outbox WHERE topic = @Topic;",
                        new { Topic = Topics.DatabaseDelivery },
                        transaction));
                await transaction.RollbackAsync();
            }

            Assert.Equal(
                0,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM database_delivery_jobs;"));
            Assert.Equal(
                0,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM outbox WHERE topic = @Topic;",
                    new { Topic = Topics.DatabaseDelivery }));

            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                Assert.Equal(
                    DatabaseDeliveryIntentPersistenceResult.Persisted,
                    await module.PersistAsync(
                        intent,
                        connection,
                        transaction));
                await transaction.CommitAsync();
            }

            var ciphertext = await connection.ExecuteScalarAsync<byte[]>(
                "SELECT mapped_values_ciphertext FROM database_delivery_jobs;");
            var ciphertextText = System.Text.Encoding.UTF8.GetString(ciphertext!);
            Assert.DoesNotContain("SECRET-CUSTOMER", ciphertextText);
            Assert.DoesNotContain("TOP-SECRET-DESCRIPTION", ciphertextText);
            Assert.Equal(
                "[\"code\", \"description\"]",
                await connection.ExecuteScalarAsync<string>(
                    "SELECT mapped_columns::text FROM database_delivery_jobs;"));
            var mappedValuesHash = await connection.ExecuteScalarAsync<string>(
                "SELECT mapped_values_hash FROM database_delivery_jobs;");
            var persistedJobId = await connection.ExecuteScalarAsync<long>(
                "SELECT id FROM database_delivery_jobs;");
            Assert.Equal(
                persistedJobId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                await connection.ExecuteScalarAsync<string>("""
                    SELECT message_key FROM outbox WHERE topic = @Topic;
                    """, new { Topic = Topics.DatabaseDelivery }));
            using (var payload = System.Text.Json.JsonDocument.Parse(
                (await connection.ExecuteScalarAsync<string>("""
                    SELECT payload::text FROM outbox WHERE topic = @Topic;
                    """, new { Topic = Topics.DatabaseDelivery }))!))
            {
                Assert.Equal(
                    persistedJobId,
                    payload.RootElement
                        .GetProperty("databaseDeliveryJobId")
                        .GetInt64());
            }

            var replay = Intent(
                destination,
                new Dictionary<string, object?>
                {
                    ["code"] = "SECRET-CUSTOMER",
                    ["description"] = "TOP-SECRET-DESCRIPTION"
                });
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                Assert.Equal(
                    DatabaseDeliveryIntentPersistenceResult.Persisted,
                    await module.PersistAsync(
                        replay,
                        connection,
                        transaction));
                await transaction.CommitAsync();
            }
            Assert.Equal(
                mappedValuesHash,
                await connection.ExecuteScalarAsync<string>(
                    "SELECT mapped_values_hash FROM database_delivery_jobs;"));
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM database_delivery_jobs;"));
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer FROM outbox WHERE topic = @Topic;
                    """, new { Topic = Topics.DatabaseDelivery }));

            var conflict = Intent(
                destination,
                new Dictionary<string, object?>
                {
                    ["code"] = "SECRET-CUSTOMER",
                    ["description"] = "CHANGED-DESCRIPTION"
                });
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                Assert.Equal(
                    DatabaseDeliveryIntentPersistenceResult.Conflict,
                    await module.PersistAsync(
                        conflict,
                        connection,
                        transaction));
                await transaction.CommitAsync();
            }
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM database_delivery_jobs;"));
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer FROM outbox WHERE topic = @Topic;
                    """, new { Topic = Topics.DatabaseDelivery }));

            await connection.ExecuteAsync("""
                UPDATE database_delivery_jobs
                SET status = 'UnknownOutcome', terminal_at = now();
                """);
            var operations = new DatabaseDeliveryOperationsService(dataSource);
            var snapshot = await operations.GetAsync(CancellationToken.None);
            var job = Assert.Single(snapshot.Jobs);
            Assert.True(job.RetryRequiresDuplicateRiskAcceptance);
            Assert.Equal(
                DatabaseDeliveryResolutionResult.DuplicateRiskAcceptanceRequired,
                await operations.RetryAsync(
                    job.Id,
                    "operator-1",
                    "Target owner approved retry",
                    "test-correlation-1",
                    acceptDuplicateRisk: false,
                    CancellationToken.None));
            Assert.Equal(
                DatabaseDeliveryResolutionResult.Updated,
                await operations.RetryAsync(
                    job.Id,
                    "operator-1",
                    "Target owner approved duplicate risk",
                    "test-correlation-2",
                    acceptDuplicateRisk: true,
                    CancellationToken.None));
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM database_delivery_resolution_audit;
                    """));
            Assert.Equal(
                "Pending",
                await connection.ExecuteScalarAsync<string>("""
                    SELECT status FROM database_delivery_jobs;
                    """));

            await connection.ExecuteAsync("""
                UPDATE outbox SET sent_at = now()
                WHERE topic = @Topic;
                """, new { Topic = Topics.DatabaseDelivery });
            await using (var transaction =
                await connection.BeginTransactionAsync())
            {
                await OutboxWriter.AddOnceAsync(
                    connection,
                    transaction,
                    Topics.DatabaseDelivery,
                    job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    new DatabaseDeliveryRequested(job.Id));
                await transaction.CommitAsync();
            }
            Assert.Equal(
                2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer FROM outbox WHERE topic = @Topic;
                    """, new { Topic = Topics.DatabaseDelivery }));

            using var observability = new DatabaseDeliveryObservability();
            var monitor = new DatabaseTargetSchemaMonitor(
                dataSource,
                new CompatibleSchemaInspector(),
                observability,
                new ConfigurationBuilder().Build(),
                NullLogger<DatabaseTargetSchemaMonitor>.Instance);
            Assert.Equal(1, await monitor.RunOnceAsync(CancellationToken.None));
            snapshot = await operations.GetAsync(CancellationToken.None);
            Assert.Equal(
                "CompatibleChange",
                Assert.Single(snapshot.SchemaChecks).Compatibility);

            await connection.ExecuteAsync(
                """
                DELETE FROM database_target_schema_checks;
                DELETE FROM database_delivery_jobs;
                UPDATE mapping_config_versions
                SET destination_definition = CAST(@Destination AS jsonb),
                    transformation_program = CAST(@Program AS jsonb)
                WHERE id = 1;
                UPDATE mapping_configs SET active_version_id = 1 WHERE id = 1;
                """,
                new
                {
                    Destination = System.Text.Json.JsonSerializer.Serialize<
                        DestinationDefinition>(
                        Destination(),
                        new System.Text.Json.JsonSerializerOptions(
                            System.Text.Json.JsonSerializerDefaults.Web)),
                    Program = """
                        {"outputs":[{"fields":[{"normalizedColumn":"code"}]}]}
                        """
                });
            Assert.Equal(
                1,
                await monitor.RunOnceAsync(CancellationToken.None));
            snapshot = await operations.GetAsync(CancellationToken.None);
            Assert.Equal(
                "CompatibleChange",
                Assert.Single(snapshot.SchemaChecks).Compatibility);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static DatabaseDeliveryIntentModule Module()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseDelivery:PayloadEncryptionKeyReference"] = "test-key"
            })
            .Build();
        var profile = new DatabaseDestinationProfile(
            "target-db",
            DatabaseProvider.PostgreSql,
            "Unused",
            ["public"],
            30,
            0,
            WriteEnabled: false,
            "sha256:profile");
        return new DatabaseDeliveryIntentModule(
            new DatabaseDeliveryPayloadProtector(
                new TestSecretProvider(key),
                configuration),
            new DatabaseDestinationProfileCatalog([profile]));
    }

    private static DatabaseDeliveryIntent Intent(
        DatabaseTableDestination destination,
        IReadOnlyDictionary<string, object?> values) =>
        new(
            FileJobId: 1,
            RowJobId: 1,
            SourceRowId: 100,
            SourceRowNumber: 1,
            ConfigVersionId: 1,
            OutputKey: "main",
            OperationKind: "Initial",
            Destination: destination,
            Values: values);

    private static DatabaseTableDestination Destination()
    {
        var schema = TargetTableSchema.Create(
            "target-db",
            DatabaseProvider.PostgreSql,
            "public",
            "customer",
            [new TargetColumnSchema(
                "code",
                "text",
                DatabaseScalarType.Text,
                1,
                IsNullable: false,
                HasDefault: false,
                IsIdentity: false,
                IsGenerated: false,
                IsComputed: false,
                IsWritable: true,
                Length: null,
                Precision: null,
                Scale: null),
                new TargetColumnSchema(
                "description",
                "text",
                DatabaseScalarType.Text,
                2,
                IsNullable: true,
                HasDefault: false,
                IsIdentity: false,
                IsGenerated: false,
                IsComputed: false,
                IsWritable: true,
                Length: null,
                Precision: null,
                Scale: null)],
            []);
        return new DatabaseTableDestination(
            schema.ProfileId,
            schema.Schema,
            schema.Table,
            "insert",
            new ManualUnknownOutcomeDuplicatePolicy(),
            schema.SchemaHash,
            schema.ToSnapshot());
    }

    private static Task SeedAsync(NpgsqlConnection connection) =>
        connection.ExecuteAsync("""
            INSERT INTO table_definitions (id, name, kind)
            OVERRIDING SYSTEM VALUE VALUES (1, 'source_rows', 'Source');
            INSERT INTO mapping_configs (
                id, name, input_folder, source_table_id, normalized_table_id)
            OVERRIDING SYSTEM VALUE VALUES (1, 'database', 'database-in', 1, NULL);
            INSERT INTO mapping_config_versions (
                id, config_id, version_no, input_layout,
                transformation_program, destination_definition)
            OVERRIDING SYSTEM VALUE VALUES (
                1, 1, 1, '{}'::jsonb, '{}'::jsonb,
                '{"kind":"databaseTable"}'::jsonb);
            INSERT INTO file_jobs (
                id, config_id, config_version_id, original_path,
                file_name, intake_key, import_status, archive_status)
            OVERRIDING SYSTEM VALUE VALUES (
                1, 1, 1, 'input.csv', 'input.csv', 'intake-1',
                'Imported', 'Archived');
            INSERT INTO row_jobs (
                id, file_job_id, source_row_id, row_number,
                config_version_id, kind, status)
            OVERRIDING SYSTEM VALUE VALUES (
                1, 1, 100, 1, 1, 'Initial', 'Pending');
            """);

    private sealed class TestSecretProvider(string secret)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(secret);
    }

    private sealed class CompatibleSchemaInspector
        : IDatabaseTargetSchemaInspector
    {
        public Task<IReadOnlyList<TargetTableSummary>> ListTablesAsync(
            string profileId,
            string schema,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TargetTableSchema> InspectAsync(
            TargetTableReference target,
            bool refresh = false,
            CancellationToken cancellationToken = default) => Task.FromResult(
                TargetTableSchema.Create(
                    target.ProfileId,
                    DatabaseProvider.PostgreSql,
                    target.Schema,
                    target.Table,
                    [
                        new TargetColumnSchema(
                            "code", "text", DatabaseScalarType.Text, 1,
                            IsNullable: false, HasDefault: false,
                            IsIdentity: false, IsGenerated: false,
                            IsComputed: false, IsWritable: true,
                            Length: null, Precision: null, Scale: null),
                        new TargetColumnSchema(
                            "description", "text", DatabaseScalarType.Text, 2,
                            IsNullable: true, HasDefault: false,
                            IsIdentity: false, IsGenerated: false,
                            IsComputed: false, IsWritable: true,
                            Length: null, Precision: null, Scale: null),
                        new TargetColumnSchema(
                            "notes", "text", DatabaseScalarType.Text, 3,
                            IsNullable: true, HasDefault: false,
                            IsIdentity: false, IsGenerated: false,
                            IsComputed: false, IsWritable: true,
                            Length: null, Precision: null, Scale: null)
                    ],
                    []));
    }
}
