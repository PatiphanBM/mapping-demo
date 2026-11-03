using System.Text;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Api;

public sealed class JsonFilePreviewModuleTests
{
    [Fact]
    public async Task Preview_uses_runtime_serializer_order_and_reports_exact_bytes()
    {
        var schema = new ContractSchemaModule().Compile("""
            {
              "type":"object",
              "properties":{
                "fileJobId":{"type":"integer"},
                "name":{"type":"string"}
              },
              "required":["fileJobId","name"],
              "additionalProperties":false
            }
            """);
        Assert.True(schema.IsSuccess);
        var destination = new JsonFileDestination(
            "operations-json",
            new ContractReference(
                "proposal",
                "1",
                "proposal/1",
                schema.SchemaHash!),
            "exports",
            "{sourceBaseName}.{fileJobId}.r{artifactRevision}.json",
            "compact");
        var records = new PreviewMappingConfigVersionResponse(
            DateTimeOffset.Parse("2026-10-22T00:00:00Z"),
            [
                Record("row-2", 2, "สอง"),
                Record("row-1", 1, "หนึ่ง")
            ]);

        var preview = await new JsonFilePreviewModule(
            new PayloadBindingModule()).BuildAsync(new JsonFilePreviewRequest(
                destination,
                schema.TargetSchema!,
                [new PayloadBinding(
                    "fileJobId",
                    RuntimeValueSource.FileJobId)],
                records,
                ConfigId: 7,
                SourceFileName: "customers.csv"));

        const string expected =
            "[{\"fileJobId\":0,\"name\":\"หนึ่ง\"},{\"fileJobId\":0,\"name\":\"สอง\"}]";
        Assert.Equal(expected, preview.FinalJson);
        Assert.Equal(2, preview.ObjectCount);
        Assert.Equal(
            Encoding.UTF8.GetByteCount(expected),
            preview.EstimatedUtf8Bytes);
        Assert.Equal(
            "exports/customers.0.r1.json",
            preview.RelativeDisplayPath);
        Assert.Empty(preview.Diagnostics);
    }

    [Fact]
    public async Task Preview_reports_binding_diagnostics_without_writing_files()
    {
        var schema = new ContractSchemaModule().Compile("""
            {
              "type":"object",
              "properties":{"name":{"type":"string"}},
              "required":["name"],
              "additionalProperties":false
            }
            """);
        Assert.True(schema.IsSuccess);
        var outputDirectory = $"preview-side-effect-{Guid.NewGuid():N}";
        var destination = new JsonFileDestination(
            "operations-json",
            new ContractReference(
                "proposal",
                "1",
                "proposal/1",
                schema.SchemaHash!),
            outputDirectory,
            "{sourceBaseName}.json",
            "compact");
        var response = new PreviewMappingConfigVersionResponse(
            DateTimeOffset.Parse("2026-10-22T00:00:00Z"),
            [new MappingPreviewRecordResponse(
                "row-1",
                RawRecord: null,
                RowNumber: 1,
                PhysicalLineNumber: 1,
                InputRecordAction.Import,
                RecordRule: null,
                new Dictionary<string, string?>(),
                new TransformationRowResult(
                    "row-1",
                    TransformationRowStatus.Done,
                    [new TransformationOutputResult(
                        "default",
                        new Dictionary<string, object?>())],
                    []))]);

        Assert.False(Directory.Exists(outputDirectory));
        var preview = await new JsonFilePreviewModule(
            new PayloadBindingModule()).BuildAsync(new JsonFilePreviewRequest(
                destination,
                schema.TargetSchema!,
                [],
                response,
                ConfigId: 7,
                SourceFileName: "customers.csv"));

        Assert.Equal("[]", preview.FinalJson);
        Assert.Equal(0, preview.ObjectCount);
        Assert.Equal(2, preview.EstimatedUtf8Bytes);
        Assert.Contains(
            preview.Diagnostics,
            diagnostic => diagnostic.Path == "name");
        Assert.False(Directory.Exists(outputDirectory));
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Preview_does_not_mutate_runtime_tables_or_filesystem()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var databaseSchema = $"json_preview_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(databaseSchema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        var outputDirectory = $"preview-no-write-{Guid.NewGuid():N}";

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = databaseSchema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                scopedConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync("""
                CREATE TABLE source_rows (id bigint PRIMARY KEY);
                CREATE TABLE row_jobs (id bigint PRIMARY KEY);
                CREATE TABLE json_export_jobs (id bigint PRIMARY KEY);
                CREATE TABLE outbox_messages (id bigint PRIMARY KEY);
                INSERT INTO source_rows VALUES (1);
                INSERT INTO row_jobs VALUES (1);
                INSERT INTO json_export_jobs VALUES (1);
                INSERT INTO outbox_messages VALUES (1);
                """);
            const string countsSql = """
                SELECT COUNT(*) FROM source_rows
                UNION ALL SELECT COUNT(*) FROM row_jobs
                UNION ALL SELECT COUNT(*) FROM json_export_jobs
                UNION ALL SELECT COUNT(*) FROM outbox_messages;
                """;
            var before = (await connection.QueryAsync<long>(countsSql)).ToArray();

            var contract = new ContractSchemaModule().Compile("""
                {
                  "type":"object",
                  "properties":{"name":{"type":"string"}},
                  "required":["name"],
                  "additionalProperties":false
                }
                """);
            Assert.True(contract.IsSuccess);
            var destination = new JsonFileDestination(
                "operations-json",
                new ContractReference(
                    "proposal",
                    "1",
                    "proposal/1",
                    contract.SchemaHash!),
                outputDirectory,
                "{sourceBaseName}.json",
                "compact");
            var records = new PreviewMappingConfigVersionResponse(
                DateTimeOffset.Parse("2026-10-22T00:00:00Z"),
                [Record("row-1", 1, "หนึ่ง")]);

            _ = await new JsonFilePreviewModule(
                new PayloadBindingModule()).BuildAsync(
                new JsonFilePreviewRequest(
                    destination,
                    contract.TargetSchema!,
                    [],
                    records,
                    ConfigId: 7,
                    SourceFileName: "customers.csv"));

            var after = (await connection.QueryAsync<long>(countsSql)).ToArray();
            Assert.Equal(before, after);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static MappingPreviewRecordResponse Record(
        string rowKey,
        int rowNumber,
        string name) =>
        new(
            rowKey,
            RawRecord: null,
            rowNumber,
            rowNumber,
            InputRecordAction.Import,
            RecordRule: null,
            new Dictionary<string, string?>(),
            new TransformationRowResult(
                rowKey,
                TransformationRowStatus.Done,
                [new TransformationOutputResult(
                    "default",
                    new Dictionary<string, object?> { ["name"] = name })],
                []));
}
