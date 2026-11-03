using System.Text.Json;
using Dapper;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Tests.Destinations;
using MappingDemo.Worker;
using MappingDemo.Worker.Delivery;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.Delivery;

public sealed class ApiContractRowNormalizeIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task One_row_schedules_one_job_per_output_without_normalized_write()
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
        var expectedEffects = fixture.RootElement
            .GetProperty("databaseEffects")
            .GetProperty("apiContractTwoOutputs");

        var schema = $"api_row_normalize_{Guid.NewGuid():N}"[..32];
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

            var contract = new ContractSchemaModule().Compile("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" },
                    "fileJobId": { "type": "integer" }
                  },
                  "required": ["name", "fileJobId"],
                  "additionalProperties": false
                }
                """);
            Assert.True(contract.IsSuccess);
            var destination = new ApiContractDestination(
                "pa-mtls",
                new ContractReference(
                    "proposal",
                    "1",
                    "proposal/1",
                    contract.SchemaHash!));
            var program = new TransformationProgram
            {
                LanguageVersion = 2,
                SemanticsProfile =
                    TransformationCapabilities.SqlServerLegacyProfile,
                Outputs =
                [
                    Output("pmx", "A"),
                    Output("ctp", "B")
                ]
            };
            await SeedAsync(
                dataSource,
                destination,
                program,
                contract.CanonicalSchema!,
                contract.SchemaHash!,
                [new PayloadBinding(
                    "fileJobId",
                    RuntimeValueSource.FileJobId)]);

            var key = Convert.ToBase64String(Enumerable.Range(0, 32)
                .Select(value => (byte)value)
                .ToArray());
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Features:UseDestinationModule"] = "true",
                    ["ApiDelivery:PayloadEncryptionKeyReference"] = "test-key"
                })
                .Build();
            var apiAdapter = new ApiContractDestinationAdapter(
                new PayloadBindingModule(),
                new DeliveryPayloadProtector(
                    new TestSecretProvider(key),
                    configuration));
            var destinationRouter = DestinationRouterTestFactory.Create(
                apiAdapter);
            var handler = new RowNormalizeHandler(
                dataSource,
                new TransformationModule(),
                destinationRouter,
                new ContractSchemaModule(),
                configuration,
                NullLogger<RowNormalizeHandler>.Instance);

            await handler.HandleAsync(
                JsonSerializer.Serialize(
                    new RowNormalizeRequested(1),
                    JsonOptions),
                CancellationToken.None);

            await using var verify = await dataSource.OpenConnectionAsync();
            Assert.Equal(
                program.Outputs.Count *
                    expectedEffects.GetProperty("jobsPerOutput").GetInt32(),
                expectedEffects.GetProperty("deliveryJobs").GetInt32());
            Assert.Equal(
                program.Outputs.Count *
                    expectedEffects.GetProperty("outboxPerOutput").GetInt32(),
                expectedEffects.GetProperty("apiDeliveryOutbox").GetInt32());
            Assert.Equal(
                "Done",
                await verify.ExecuteScalarAsync<string>(
                    "SELECT status FROM row_jobs WHERE id = 1;"));
            Assert.Equal(
                expectedEffects.GetProperty("deliveryJobs").GetInt32(),
                await verify.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::integer FROM delivery_jobs;"));
            Assert.Equal(
                expectedEffects.GetProperty("apiDeliveryOutbox").GetInt32(),
                await verify.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer FROM outbox
                    WHERE topic = 'mapping.api-delivery';
                    """));
            Assert.Equal(
                expectedEffects.GetProperty("normalizedWrites").GetInt32(),
                await verify.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM table_definitions
                    WHERE kind = 'Normalized';
                    """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task SeedAsync(
        NpgsqlDataSource dataSource,
        ApiContractDestination destination,
        TransformationProgram program,
        string canonicalSchema,
        string schemaHash,
        IReadOnlyList<PayloadBinding> payloadBindings)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            CREATE TABLE source_rows (
                id bigint PRIMARY KEY,
                file_job_id bigint NOT NULL,
                row_number integer NOT NULL,
                physical_line_number integer NOT NULL
            );
            INSERT INTO table_definitions (name, kind)
            VALUES ('source_rows', 'Source');
            INSERT INTO mapping_configs (
                name, input_folder, source_table_id, normalized_table_id)
            SELECT 'api-config', 'input/api', id, NULL
            FROM table_definitions WHERE name = 'source_rows';
            """);
        var configId = await connection.ExecuteScalarAsync<long>(
            "SELECT id FROM mapping_configs WHERE name = 'api-config';");
        var versionId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO mapping_config_versions (
                config_id,
                version_no,
                input_layout,
                transformation_program,
                destination_definition,
                accepted_extensions,
                api_destination_profile_id,
                contract_reference,
                contract_schema_snapshot,
                contract_schema_hash,
                contract_schema_fetched_at,
                payload_bindings_snapshot)
            VALUES (
                @ConfigId,
                1,
                '{}'::jsonb,
                CAST(@Program AS jsonb),
                CAST(@Destination AS jsonb),
                '[".csv"]'::jsonb,
                'pa-mtls',
                'proposal/1',
                CAST(@ContractSchema AS jsonb),
                @SchemaHash,
                now(),
                CAST(@PayloadBindings AS jsonb))
            RETURNING id;
            """, new
        {
            ConfigId = configId,
            Program = JsonSerializer.Serialize(program, JsonOptions),
            Destination = JsonSerializer.Serialize<DestinationDefinition>(
                destination,
                JsonOptions),
            ContractSchema = canonicalSchema,
            SchemaHash = schemaHash,
            PayloadBindings = JsonSerializer.Serialize(
                payloadBindings,
                JsonOptions)
        });
        var fileJobId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO file_jobs (
                config_id,
                config_version_id,
                original_path,
                file_name,
                intake_key,
                import_status,
                archive_status)
            VALUES (
                @ConfigId,
                @VersionId,
                'input/api/a.csv',
                'a.csv',
                'api-a',
                'Imported',
                'Archived')
            RETURNING id;
            """, new { ConfigId = configId, VersionId = versionId });
        await connection.ExecuteAsync("""
            INSERT INTO source_rows (
                id, file_job_id, row_number, physical_line_number)
            VALUES (100, @FileJobId, 1, 2);
            INSERT INTO row_jobs (
                id,
                file_job_id,
                source_row_id,
                row_number,
                config_version_id,
                kind,
                status)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1,
                @FileJobId,
                100,
                1,
                @VersionId,
                'Initial',
                'Pending');
            """, new { FileJobId = fileJobId, VersionId = versionId });
    }

    private static TransformationOutput Output(
        string outputKey,
        string value) => new()
        {
            OutputKey = outputKey,
            Fields =
        [
            new FieldAssignment
            {
                TargetPath = "name",
                Value = new LiteralExpression
                {
                    Type = ExpressionValueType.Text,
                    Value = JsonSerializer.SerializeToElement(value)
                }
            }
        ]
        };

    private sealed class TestSecretProvider(string value)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
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
}
