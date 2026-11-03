using System.Text;
using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class ContractSnapshotIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Create_version_persists_authoritative_contract_snapshot_atomically(
        bool jsonFile)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var schemaName = $"contract_test_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schemaName);
        await using var adminSource = NpgsqlDataSource.Create(connectionString);
        await using var admin = await adminSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schemaName
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(scopedConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            for (var number = 1; number <= 8; number++)
            {
                var name = number switch
                {
                    1 => "0001_table_definitions.sql",
                    2 => "0002_mapping_configs.sql",
                    3 => "0003_file_jobs_outbox.sql",
                    4 => "0004_row_jobs.sql",
                    5 => "0005_row_job_evaluation_time.sql",
                    6 => "0006_row_output_reconciliation.sql",
                    7 => "0007_destination_definition.sql",
                    8 => "0008_input_file_extensions.sql",
                    _ => throw new InvalidOperationException()
                };
                await connection.ExecuteAsync(await ReadMigrationAsync(name));
            }
            await connection.ExecuteAsync(
                await ReadMigrationAsync("0011_json_file_destination.sql"));
            await connection.ExecuteAsync(
                "CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);");
            await connection.ExecuteAsync(
                await ReadMigrationAsync(
                    "0014_api_destination_profile_catalog.sql"));

            var sourceTableId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO table_definitions (name, kind)
                VALUES ('source_claims', 'Source')
                RETURNING id;
                """);
            await connection.ExecuteAsync("""
                INSERT INTO table_columns (
                    table_id, name, data_type, is_required, ordinal)
                VALUES (@SourceTableId, 'claim_no', 'Text', true, 1);
                """, new { SourceTableId = sourceTableId });
            var configId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO mapping_configs (name, input_folder, source_table_id, normalized_table_id)
                VALUES (@Name, @InputFolder, @SourceTableId, NULL)
                RETURNING id;
                """, new
            {
                Name = jsonFile ? "Claims JSON" : "Claims API",
                InputFolder = jsonFile ? "claims-json" : "claims-api",
                SourceTableId = sourceTableId
            });

            var profile = Profile();
            var fileProfile = FileDestinationProfileCatalogTests.Profile(
                "local-json-export",
                "claims/2") with
            {
                PayloadBindings = profile.PayloadBindings
            };
            var fetcher = new MutableFetcher(Schema(maxLength: 30));
            var registry = new ContractRegistryModule(
                new ApiDestinationProfileCatalog([profile]),
                fetcher,
                fileProfiles: new FileDestinationProfileCatalog([fileProfile]));
            var service = new MappingConfigService(
                dataSource,
                Options.Create(new PathOptions { InputRoot = "input" }),
                new TestHostEnvironment(),
                registry);
            var request = Request(jsonFile, fileProfile.Id);

            var created = await service.CreateVersionAsync(
                configId,
                request,
                CancellationToken.None);

            Assert.NotNull(created.ContractSnapshot);
            Assert.NotEqual("client-provided", created.ContractSnapshot.SchemaHash);
            var createdContract = created.Destination switch
            {
                ApiContractDestination api => api.Contract,
                JsonFileDestination file => file.Contract,
                _ => throw new InvalidOperationException()
            };
            Assert.Equal(
                created.ContractSnapshot.SchemaHash,
                createdContract.SchemaHash);
            var persisted = await connection.QuerySingleAsync<PersistedSnapshot>("""
                SELECT
                    api_destination_profile_id AS "ProfileId",
                    contract_reference AS "ContractReference",
                    contract_schema_snapshot::text AS "SchemaJson",
                    contract_schema_hash AS "SchemaHash",
                    payload_bindings_snapshot::text AS "BindingsJson"
                FROM mapping_config_versions
                WHERE id = @Id;
                """, new { created.Id });
            Assert.Equal(jsonFile ? null : profile.Id, persisted.ProfileId);
            Assert.Equal("claims/2", persisted.ContractReference);
            Assert.Equal(created.ContractSnapshot.SchemaHash, persisted.SchemaHash);
            Assert.Contains("claimNo", persisted.SchemaJson);
            Assert.Contains("meta.batch", persisted.BindingsJson);
            if (jsonFile)
            {
                var responseJson = JsonSerializer.Serialize(
                    created,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.DoesNotContain(fileProfile.OutputRoot, responseJson);
                Assert.DoesNotContain(
                    fileProfile.EncryptionPolicy,
                    responseJson);
            }

            fetcher.Json = Schema(maxLength: 60);
            var current = await registry.ResolveAsync(
                created.Destination,
                CancellationToken.None);
            var fetchCallsBeforeLoad = fetcher.Calls;
            var loaded = await service.GetVersionAsync(
                configId,
                created.VersionNo,
                CancellationToken.None);
            Assert.NotNull(loaded?.ContractSnapshot);
            Assert.Equal(fetchCallsBeforeLoad, fetcher.Calls);
            Assert.NotEqual(current.SchemaHash, loaded.ContractSnapshot.SchemaHash);
            Assert.Equal(persisted.SchemaHash, loaded.ContractSnapshot.SchemaHash);
        }
        finally
        {
            await admin.ExecuteAsync($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static ApiDestinationProfile Profile() =>
        new(
            "claims-envelope",
            "Claims Envelope",
            true,
            "https://contracts.example.test",
            "/schemas/{contractReference}",
            "https://delivery.example.test",
            "/cases/{contractVersion}",
            "PUT",
            ["X-Request-Id", "Idempotency-Key"],
            "claims-oauth-client",
            10,
            [new SupportedContract("claims", "2", "claims/2")],
            [
                new PayloadBinding("meta.contractKey", RuntimeValueSource.ContractReference),
                new PayloadBinding("meta.batch", RuntimeValueSource.FileJobId),
                new PayloadBinding("meta.record", RuntimeValueSource.SourceRowId),
                new PayloadBinding("meta.line", RuntimeValueSource.SourceRowNumber),
                new PayloadBinding("meta.revision", RuntimeValueSource.ConfigVersionReference),
                new PayloadBinding("meta.branch", RuntimeValueSource.OutputKey)
            ],
            "OAuth2ClientCredentials",
            "Idempotency-Key",
            "X-Request-Id");

    private static CreateMappingConfigVersionRequest Request(
        bool jsonFile,
        string fileProfileId) =>
        new(
            new DelimitedInputLayout(
                "utf-8",
                ",",
                0,
                DelimitedHeaderPolicy.SingleRow,
                [new DelimitedInputField("claim_no", "claim_no")]),
            new TransformationProgram
            {
                LanguageVersion = 2,
                SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
                Outputs =
                [
                    new TransformationOutput
                    {
                        OutputKey = "main",
                        Fields =
                        [
                            new FieldAssignment
                            {
                                TargetPath = "case.claimNo",
                                Value = new SourceExpression { Field = "claim_no" }
                            }
                        ]
                    }
                ]
            },
            jsonFile
                ? new JsonFileDestination(
                    fileProfileId,
                    new ContractReference(
                        "claims",
                        "2",
                        "claims/2",
                        "client-provided"),
                    "claims",
                    "{sourceBaseName}.{fileJobId}.json",
                    "compact")
                : new ApiContractDestination(
                    "claims-envelope",
                    new ContractReference(
                        "claims",
                        "2",
                        "claims/2",
                        "client-provided")),
            [".csv"]);

    private static string Schema(int maxLength) => """
        {
          "type":"object","additionalProperties":false,"required":["meta","case"],
          "properties":{
            "meta":{"type":"object","additionalProperties":false,"required":["contractKey","batch","record","line","revision","branch"],"properties":{"contractKey":{"type":"string"},"batch":{"type":"integer"},"record":{"type":"integer"},"line":{"type":"integer"},"revision":{"type":"string"},"branch":{"type":"string"}}},
            "case":{"type":"object","additionalProperties":false,"required":["claimNo"],"properties":{"claimNo":{"type":"string","maxLength":MAX_LENGTH}}}
          }
        }
        """.Replace(
            "MAX_LENGTH",
            maxLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static async Task<string> ReadMigrationAsync(string fileName)
    {
        var resourceName = "MappingDemo.Shared.Database.Migrations." + fileName;
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class MutableFetcher(string json) : IContractSchemaFetcher
    {
        public string Json { get; set; } = json;

        public int Calls { get; private set; }

        public ValueTask<ContractSchemaFetchResult> FetchAsync(
            IContractProfile profile,
            ContractReference contract,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(new ContractSchemaFetchResult(
                Encoding.UTF8.GetBytes(Json),
                "application/schema+json",
                DateTimeOffset.Parse("2026-10-20T00:00:00Z")));
        }
    }

    private sealed record PersistedSnapshot(
        string ProfileId,
        string ContractReference,
        string SchemaJson,
        string SchemaHash,
        string BindingsJson);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MappingDemo.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
