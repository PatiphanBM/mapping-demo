using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Tests.Input;
using MappingDemo.Worker;
using MappingDemo.Worker.Destinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Tests.Worker;

public sealed class FileImportHandlerInputMatrixIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Four_extensions_import_and_invalid_or_rollback_excel_write_no_partial_rows()
    {
        var rootConnection = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(rootConnection))
        {
            return;
        }

        var schema = $"file_gate_b_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var temp = Path.Combine(Path.GetTempPath(), $"mapping-gate-b-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        await using var adminDataSource = NpgsqlDataSource.Create(rootConnection);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var connectionString = new NpgsqlConnectionStringBuilder(rootConnection)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await new MigrationRunner(dataSource).RunAsync();

            var tables = new TableService(dataSource);
            var sourceTableId = await tables.CreateAsync(new TableDefinition(
                "source_gate_b", TableKind.Source,
                [
                    new ColumnDefinition("order_no", ColumnDataType.Text, false, 1),
                    new ColumnDefinition("amount", ColumnDataType.Text, false, 2),
                    new ColumnDefinition("active", ColumnDataType.Text, false, 3),
                    new ColumnDefinition("formula", ColumnDataType.Text, false, 4)
                ]));
            var normalizedTableId = await tables.CreateAsync(new TableDefinition(
                "normalized_gate_b", TableKind.Normalized,
                [new ColumnDefinition("order_no", ColumnDataType.Text, false, 1)]));
            var environment = new TestEnvironment(temp);
            var configs = new MappingConfigService(
                dataSource,
                Options.Create(new PathOptions { InputRoot = "input" }),
                environment);
            var configId = await configs.CreateAsync(new CreateMappingConfigRequest(
                "Gate B", "in", sourceTableId, normalizedTableId,
                new LegacyTableDestination(normalizedTableId)));

            var delimited = await configs.CreateVersionAsync(configId, new(
                new DelimitedInputLayout("utf-8", ",", 0,
                    DelimitedHeaderPolicy.SingleRow,
                    [
                        new DelimitedInputField("Order No", "order_no"),
                        new DelimitedInputField("Amount", "amount"),
                        new DelimitedInputField("Active", "active"),
                        new DelimitedInputField("Formula", "formula")
                    ]),
                Program(),
                new LegacyTableDestination(normalizedTableId),
                [".txt", ".csv"]));
            var xlsx = await configs.CreateVersionAsync(configId, new(
                ExcelLayout(ExcelWorkbookKind.Xlsx, "Orders", 2, 4),
                Program(),
                new LegacyTableDestination(normalizedTableId),
                [".xlsx"]));
            var xls = await configs.CreateVersionAsync(configId, new(
                ExcelLayout(ExcelWorkbookKind.Xls, "Sheet", 1, 2),
                Program(),
                new LegacyTableDestination(normalizedTableId),
                [".xls"]));
            await configs.ActivateVersionAsync(configId, xlsx.Id);

            var intake = new FileIntake(dataSource);
            var handler = Handler(dataSource, environment, temp);
            await ImportText("one.txt", "TXT-1,1,true,2", delimited.Id);
            await ImportText("two.csv", "CSV-1,2,false,4", delimited.Id);
            await ImportWorkbook("three.xlsx", ExcelInputRecordTests.GoldenWorkbook.Xlsx(), xlsx.Id);
            await ImportWorkbook("four.xls", ExcelInputRecordTests.GoldenWorkbook.Xls(), xls.Id);

            await using var connection = await dataSource.OpenConnectionAsync();
            Assert.Equal(5, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM source_gate_b;"));
            Assert.Equal(5, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM row_jobs;"));
            Assert.Equal(5, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM outbox WHERE topic = @Topic;",
                new { Topic = Topics.RowNormalize }));

            var corruptPath = Path.Combine(temp, "corrupt.xlsx");
            await File.WriteAllBytesAsync(corruptPath, [0x50, 0x4B, 0x03, 0x04, 1, 2, 3]);
            var corruptJob = await intake.CreateJobAsync(
                configId, xlsx.Id, intake.Inspect(corruptPath));
            await handler.HandleAsync(Payload(corruptJob!.Value), CancellationToken.None);
            Assert.Equal("ImportFailed", await connection.ExecuteScalarAsync<string>(
                "SELECT import_status FROM file_jobs WHERE id = @Id;", new { Id = corruptJob }));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM source_gate_b WHERE file_job_id = @Id;", new { Id = corruptJob }));

            await configs.ActivateVersionAsync(configId, delimited.Id);
            var rollbackPath = Path.Combine(temp, "rollback.xlsx");
            await WriteAsync(rollbackPath, ExcelInputRecordTests.GoldenWorkbook.Xlsx());
            var rollbackJob = await intake.CreateJobAsync(
                configId, delimited.Id, intake.Inspect(rollbackPath));
            await handler.HandleAsync(Payload(rollbackJob!.Value), CancellationToken.None);
            Assert.Contains("FileExtensionNotAccepted", await connection.ExecuteScalarAsync<string>(
                "SELECT last_error FROM file_jobs WHERE id = @Id;", new { Id = rollbackJob }));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM source_gate_b WHERE file_job_id = @Id;", new { Id = rollbackJob }));

            async Task ImportText(string name, string row, long versionId)
            {
                var path = Path.Combine(temp, name);
                await File.WriteAllTextAsync(path,
                    $"Order No,Amount,Active,Formula{Environment.NewLine}{row}{Environment.NewLine}");
                await Import(path, versionId);
            }

            async Task ImportWorkbook(string name, Stream workbook, long versionId)
            {
                await using (workbook)
                {
                    var path = Path.Combine(temp, name);
                    await WriteAsync(path, workbook);
                    await Import(path, versionId);
                }
            }

            async Task Import(string path, long versionId)
            {
                var jobId = await intake.CreateJobAsync(
                    configId, versionId, intake.Inspect(path));
                Assert.NotNull(jobId);
                Assert.True(await handler.HandleAsync(
                    Payload(jobId.Value), CancellationToken.None));
            }
        }
        finally
        {
            await admin.ExecuteAsync($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    private static ExcelInputLayout ExcelLayout(
        ExcelWorkbookKind kind, string sheet, int headerRow, int dataStartRow) => new(
        kind, sheet, headerRow, dataStartRow,
        [
            new ExcelInputField("Order No", "order_no"),
            new ExcelInputField("Amount", "amount"),
            new ExcelInputField("Active", "active"),
            new ExcelInputField("Formula", "formula")
        ], ExcelBlankRowPolicy.Skip);

    private static TransformationProgram Program() => new()
    {
        LanguageVersion = 1,
        SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
        Outputs =
        [
            new TransformationOutput
            {
                OutputKey = "default",
                Fields =
                [
                    new FieldAssignment
                    {
                        NormalizedColumn = "order_no",
                        Value = new SourceExpression { Field = "order_no" }
                    }
                ]
            }
        ]
    };

    private static FileImportHandler Handler(
        NpgsqlDataSource dataSource,
        IHostEnvironment environment,
        string root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Import:DelaySeconds"] = "1",
                ["Import:StagingRoot"] = Path.Combine(root, "staging"),
                ["Paths:ArchiveRoot"] = Path.Combine(root, "archive"),
                ["Demo:ImportRowDelayMs"] = "0"
            }).Build();
        return new FileImportHandler(
            dataSource,
            new InputRecordModule(),
            new JsonExportReadiness(),
            configuration,
            environment,
            NullLogger<FileImportHandler>.Instance);
    }

    private static string Payload(long fileJobId) => JsonSerializer.Serialize(
        new FileImportRequested(fileJobId),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task WriteAsync(string path, Stream source)
    {
        source.Position = 0;
        await using var target = File.Create(path);
        await source.CopyToAsync(target);
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MappingDemo.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
