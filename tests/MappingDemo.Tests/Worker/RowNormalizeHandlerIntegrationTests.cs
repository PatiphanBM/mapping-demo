using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.FileJobs;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Transformation.Adapters;
using MappingDemo.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MappingDemo.Tests.Worker;

public sealed class RowNormalizeHandlerIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Handler_uses_the_module_for_reprocess_lookup_and_errors()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"normalize_test_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var testConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                testConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await CreateSchemaAsync(connection);
            await SeedMetadataAsync(connection);
            await SeedProgramsAndRowsAsync(connection);

            var module = new TransformationModule(
                new PostgreSqlTransformationLookupResolver(dataSource));
            var handler = new RowNormalizeHandler(
                dataSource,
                module,
                new ConfigurationBuilder().Build(),
                NullLogger<RowNormalizeHandler>.Instance);

            await HandleAsync(handler, 1);
            var initialOutputs = await LoadOutputsAsync(connection, 101);
            Assert.Collection(
                initialOutputs,
                output =>
                {
                    Assert.Equal("ctp", output.OutputKey);
                    Assert.Equal("CTP", output.ProductName);
                    Assert.Equal(1234.50m, output.Amount);
                },
                output =>
                {
                    Assert.Equal("pmx", output.OutputKey);
                    Assert.Equal("PMX", output.ProductName);
                    Assert.Equal(1234.50m, output.Amount);
                });

            await InsertRowJobAsync(connection, 2, 101, 1, 2, "Reprocess");
            await HandleAsync(handler, 2);
            Assert.Collection(
                await LoadOutputsAsync(connection, 101),
                output => Assert.Equal("pmx", output.OutputKey));
            await AssertStatusAsync(connection, 2, RowJobStatus.Done);

            await connection.ExecuteAsync("""
                INSERT INTO mti_original (
                    source_row_id, output_key, file_job_id, row_number,
                    row_job_id, config_version_id,
                    system_id, cproductname, cloannumber, amount)
                VALUES (
                    999, 'pmx', 1, 999, 999, 2,
                    'SYS', 'PMX', 'LN-001', 50.00);
                """);
            await InsertRowJobAsync(connection, 3, 101, 1, 2, "Reprocess");
            await HandleAsync(handler, 3);
            Assert.Empty(await LoadOutputsAsync(connection, 101));
            await AssertStatusAsync(connection, 3, RowJobStatus.Filtered);

            await HandleAsync(handler, 3);
            Assert.Empty(await LoadOutputsAsync(connection, 101));
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM mti_original
                    WHERE source_row_id = 999;
                    """));

            await connection.ExecuteAsync("""
                INSERT INTO mti_original (
                    source_row_id, output_key, file_job_id, row_number,
                    row_job_id, config_version_id,
                    system_id, cproductname, cloannumber, amount)
                VALUES (
                    102, 'legacy', 1, 2, 900, 1,
                    'OLD', 'OLD', 'OLD', 99.00);
                """);
            await InsertRowJobAsync(connection, 4, 102, 2, 3, "Initial");
            await InsertRowJobAsync(connection, 5, 103, 3, 4, "Initial");
            await HandleAsync(handler, 4);
            await HandleAsync(handler, 5);

            await AssertStatusAsync(connection, 4, RowJobStatus.Invalid);
            await AssertStatusAsync(connection, 5, RowJobStatus.Invalid);
            Assert.Collection(
                await LoadOutputsAsync(connection, 102),
                output => Assert.Equal("legacy", output.OutputKey));

            await connection.ExecuteAsync("""
                INSERT INTO mti_original (
                    source_row_id, output_key, file_job_id, row_number,
                    row_job_id, config_version_id,
                    system_id, cproductname, cloannumber, amount)
                VALUES (
                    104, 'legacy', 1, 4, 901, 1,
                    'OLD', 'OLD', 'OLD', 88.00);
                """);
            await InsertRowJobAsync(connection, 6, 104, 4, 1, "Initial");
            var failureConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:FailNormalizeRowNumbers:0"] = "4"
                })
                .Build();
            var failureHandler = new RowNormalizeHandler(
                dataSource,
                module,
                failureConfiguration,
                NullLogger<RowNormalizeHandler>.Instance);
            await HandleAsync(failureHandler, 6);
            await AssertStatusAsync(connection, 6, RowJobStatus.Failed);
            Assert.Equal(
                4,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT attempts
                    FROM row_jobs
                    WHERE id = 6;
                    """));
            Assert.Collection(
                await LoadOutputsAsync(connection, 104),
                output => Assert.Equal("legacy", output.OutputKey));
            await HandleAsync(failureHandler, 6);
            Assert.Collection(
                await LoadOutputsAsync(connection, 104),
                output => Assert.Equal("legacy", output.OutputKey));

            var controller = new FileJobsController(
                new FileJobService(dataSource));
            var errorsResult = await controller.GetErrors(
                1,
                CancellationToken.None);
            var errorsOk = Assert.IsType<OkObjectResult>(errorsResult);
            var errors = Assert.IsAssignableFrom<
                IReadOnlyList<RowErrorResponse>>(errorsOk.Value);
            Assert.Equal(3, errors.Count);
            Assert.Contains(
                errors,
                error => error.OutputKey == "pmx"
                    && error.Field is null
                    && error.ExpressionPath == "outputs[0].emitWhen.left"
                    && error.Reason == "InvalidDate");
            Assert.Contains(
                errors,
                error => error.OutputKey == "ctp"
                    && error.Field == "amount"
                    && error.ExpressionPath == "outputs[1].fields[3].value"
                    && error.Reason == "InvalidDecimal");
            Assert.Contains(
                errors,
                error => error.OutputKey is null
                    && error.Field is null
                    && error.ExpressionPath == "sourceWhen.left"
                    && error.Reason == "InvalidDate");

            var historyResult = await controller.GetRowHistory(
                1,
                2,
                CancellationToken.None);
            var historyOk = Assert.IsType<OkObjectResult>(historyResult);
            var history = Assert.IsAssignableFrom<
                IReadOnlyList<RowJobHistoryResponse>>(historyOk.Value);
            var invalidRun = Assert.Single(history);
            Assert.Equal(RowJobStatus.Invalid, invalidRun.Status);
            Assert.Equal(2, invalidRun.Errors.Count);
            Assert.Contains(
                invalidRun.Errors,
                error => error.OutputKey == "ctp"
                    && error.Field == "amount"
                    && error.ExpressionPath == "outputs[1].fields[3].value");

            var fileJobResult = await controller.GetById(
                1,
                CancellationToken.None);
            var fileJobOk = Assert.IsType<OkObjectResult>(fileJobResult);
            var fileJob = Assert.IsType<FileJobResponse>(fileJobOk.Value);
            Assert.Equal(1, fileJob.FilteredRows);
            Assert.Equal(2, fileJob.InvalidRows);
            Assert.Equal(1, fileJob.FailedRows);
            Assert.Equal(
                NormalizationStatus.CompletedWithErrors,
                fileJob.NormalizationStatus);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task CreateSchemaAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("""
            CREATE TABLE table_definitions (
                id bigint PRIMARY KEY,
                name text NOT NULL,
                kind text NOT NULL
            );
            CREATE TABLE table_columns (
                table_id bigint NOT NULL,
                name text NOT NULL,
                data_type text NOT NULL,
                is_required boolean NOT NULL,
                ordinal integer NOT NULL,
                length integer,
                precision integer,
                scale integer
            );
            CREATE TABLE file_jobs (
                id bigint PRIMARY KEY,
                config_id bigint NOT NULL,
                config_version_id bigint NOT NULL,
                file_name text NOT NULL,
                import_status text NOT NULL,
                archive_status text NOT NULL,
                total_rows integer,
                last_error text,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE mapping_configs (
                id bigint PRIMARY KEY,
                source_table_id bigint NOT NULL,
                normalized_table_id bigint NOT NULL
            );
            CREATE TABLE mapping_config_versions (
                id bigint PRIMARY KEY,
                config_id bigint NOT NULL,
                version_no integer NOT NULL,
                transformation_program jsonb NOT NULL
            );
            CREATE TABLE row_jobs (
                id bigint PRIMARY KEY,
                file_job_id bigint NOT NULL,
                source_row_id bigint NOT NULL,
                row_number integer NOT NULL,
                config_version_id bigint NOT NULL,
                kind text NOT NULL,
                status text NOT NULL,
                attempts integer NOT NULL DEFAULT 0,
                last_error text,
                evaluation_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                finished_at timestamptz
            );
            CREATE UNIQUE INDEX ux_row_jobs_pending_source_row
                ON row_jobs (source_row_id)
                WHERE status = 'Pending';
            CREATE TABLE row_errors (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                row_job_id bigint NOT NULL,
                file_job_id bigint NOT NULL,
                row_number integer NOT NULL,
                output_key text,
                field text,
                expression_path text NOT NULL,
                reason text NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE source_pmib (
                id bigint PRIMARY KEY,
                file_job_id bigint NOT NULL,
                row_number integer NOT NULL,
                physical_line_number integer NOT NULL,
                system_id text,
                product_name text,
                loan_number text,
                amount text
            );
            CREATE TABLE mti_original (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                source_row_id bigint NOT NULL,
                output_key text NOT NULL,
                file_job_id bigint NOT NULL,
                row_number integer NOT NULL,
                row_job_id bigint NOT NULL,
                config_version_id bigint NOT NULL,
                normalized_at timestamptz NOT NULL DEFAULT now(),
                system_id text,
                cproductname text,
                cloannumber text,
                amount numeric(18, 2),
                UNIQUE (source_row_id, output_key)
            );
            CREATE INDEX ix_mti_original_policy_lookup
                ON mti_original (system_id, cproductname, cloannumber);
            """);
    }

    private static async Task SeedMetadataAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("""
            INSERT INTO table_definitions (id, name, kind) VALUES
                (1, 'source_pmib', 'Source'),
                (2, 'mti_original', 'Normalized');
            INSERT INTO table_columns (
                table_id, name, data_type, is_required, ordinal,
                precision, scale)
            VALUES
                (1, 'system_id', 'Text', false, 1, NULL, NULL),
                (1, 'product_name', 'Text', false, 2, NULL, NULL),
                (1, 'loan_number', 'Text', false, 3, NULL, NULL),
                (1, 'amount', 'Text', false, 4, NULL, NULL),
                (2, 'system_id', 'Text', true, 1, NULL, NULL),
                (2, 'cproductname', 'Text', true, 2, NULL, NULL),
                (2, 'cloannumber', 'Text', true, 3, NULL, NULL),
                (2, 'amount', 'Decimal', true, 4, 18, 2);
            INSERT INTO mapping_configs (
                id, source_table_id, normalized_table_id)
            VALUES (1, 1, 2);
            INSERT INTO file_jobs (
                id, config_id, config_version_id, file_name,
                import_status, archive_status, total_rows)
            VALUES (
                1, 1, 1, 'pmib.csv', 'Imported', 'Archived', 4);
            """);
    }

    private static async Task SeedProgramsAndRowsAsync(
        NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("""
            INSERT INTO source_pmib (
                id, file_job_id, row_number, physical_line_number,
                system_id, product_name, loan_number, amount)
            VALUES
                (101, 1, 1, 10, 'SYS', 'PMX', 'LN-001', ' 1234.50 '),
                (102, 1, 2, 11, 'SYS', 'PMX', 'LN-002', 'bad'),
                (103, 1, 3, 12, 'SYS', 'PMX', 'LN-003', 'bad'),
                (104, 1, 4, 13, 'SYS', 'PMX', 'LN-004', '10.00');
            """);
        await connection.ExecuteAsync("""
            INSERT INTO mapping_config_versions (
                id, config_id, version_no, transformation_program)
            VALUES
                (1, 1, 1, CAST(@Initial AS jsonb)),
                (2, 1, 2, CAST(@Lookup AS jsonb)),
                (3, 1, 3, CAST(@OutputErrors AS jsonb)),
                (4, 1, 4, CAST(@SourceError AS jsonb));
            """, new
        {
            Initial = Serialize(InitialProgram()),
            Lookup = Serialize(LookupProgram()),
            OutputErrors = Serialize(OutputErrorProgram()),
            SourceError = Serialize(SourceErrorProgram())
        });
        await InsertRowJobAsync(connection, 1, 101, 1, 1, "Initial");
    }

    private static async Task InsertRowJobAsync(
        NpgsqlConnection connection,
        long id,
        long sourceRowId,
        int rowNumber,
        long configVersionId,
        string kind)
    {
        await connection.ExecuteAsync(
            """
                INSERT INTO row_jobs (
                    id, file_job_id, source_row_id, row_number,
                    config_version_id, kind, status, evaluation_at)
                VALUES (
                    @Id, 1, @SourceRowId, @RowNumber,
                    @ConfigVersionId, @Kind, 'Pending',
                    '2026-10-08T17:00:00Z');
                """,
            new
            {
                Id = id,
                SourceRowId = sourceRowId,
                RowNumber = rowNumber,
                ConfigVersionId = configVersionId,
                Kind = kind
            });
    }

    private static async Task HandleAsync(
        RowNormalizeHandler handler,
        long rowJobId)
    {
        await handler.HandleAsync(
            JsonSerializer.Serialize(
                new RowNormalizeRequested(rowJobId),
                JsonOptions),
            CancellationToken.None);
    }

    private static async Task<IReadOnlyList<OutputRow>> LoadOutputsAsync(
        NpgsqlConnection connection,
        long sourceRowId)
    {
        return (await connection.QueryAsync<OutputRow>("""
            SELECT
                output_key AS "OutputKey",
                cproductname AS "ProductName",
                amount AS "Amount"
            FROM mti_original
            WHERE source_row_id = @SourceRowId
            ORDER BY output_key;
            """, new { SourceRowId = sourceRowId })).ToArray();
    }

    private static async Task AssertStatusAsync(
        NpgsqlConnection connection,
        long rowJobId,
        string expected)
    {
        var status = await connection.ExecuteScalarAsync<string>(
            "SELECT status FROM row_jobs WHERE id = @RowJobId;",
            new { RowJobId = rowJobId });
        Assert.Equal(expected, status);
    }

    private static TransformationProgram InitialProgram()
    {
        return Program(Output("pmx", "PMX"), Output("ctp", "CTP"));
    }

    private static TransformationProgram LookupProgram()
    {
        return Program(Output(
            "pmx",
            "PMX",
            new NotExistsExpression
            {
                Lookup = TransformationCapabilities.MtiOriginalPolicyExistsV1,
                Arguments = new Dictionary<string, ValueExpression>
                {
                    ["systemId"] = Source("system_id"),
                    ["productName"] = Text("PMX"),
                    ["loanNumber"] = Source("loan_number")
                }
            }));
    }

    private static TransformationProgram OutputErrorProgram()
    {
        var invalidDate = new EqualExpression
        {
            Left = new ConvertExpression
            {
                Value = Source("amount"),
                Type = ExpressionValueType.Date
            },
            Right = Date("2026-10-09")
        };
        return Program(
            Output("pmx", "PMX", invalidDate),
            Output("ctp", "CTP"));
    }

    private static TransformationProgram SourceErrorProgram()
    {
        return Program(Output("pmx", "PMX")) with
        {
            SourceWhen = new EqualExpression
            {
                Left = new ConvertExpression
                {
                    Value = Source("amount"),
                    Type = ExpressionValueType.Date
                },
                Right = Date("2026-10-09")
            }
        };
    }

    private static TransformationProgram Program(
        params TransformationOutput[] outputs)
    {
        return new TransformationProgram
        {
            SemanticsProfile =
                TransformationCapabilities.SqlServerLegacyProfile,
            Outputs = outputs
        };
    }

    private static TransformationOutput Output(
        string outputKey,
        string productName,
        BooleanExpression? emitWhen = null)
    {
        return new TransformationOutput
        {
            OutputKey = outputKey,
            EmitWhen = emitWhen,
            Fields =
            [
                Assignment("system_id", Source("system_id")),
                Assignment("cproductname", Text(productName)),
                Assignment("cloannumber", Source("loan_number")),
                Assignment("amount", Source("amount"))
            ]
        };
    }

    private static FieldAssignment Assignment(
        string column,
        ValueExpression value)
    {
        return new FieldAssignment
        {
            NormalizedColumn = column,
            Value = value
        };
    }

    private static SourceExpression Source(string field)
    {
        return new SourceExpression { Field = field };
    }

    private static LiteralExpression Text(string value)
    {
        return Literal(ExpressionValueType.Text, value);
    }

    private static LiteralExpression Date(string value)
    {
        return Literal(ExpressionValueType.Date, value);
    }

    private static LiteralExpression Literal(
        ExpressionValueType type,
        object value)
    {
        return new LiteralExpression
        {
            Type = type,
            Value = JsonSerializer.SerializeToElement(value)
        };
    }

    private static string Serialize(TransformationProgram program)
    {
        return JsonSerializer.Serialize(program, JsonOptions);
    }

    private sealed record OutputRow(
        string OutputKey,
        string ProductName,
        decimal Amount);
}
