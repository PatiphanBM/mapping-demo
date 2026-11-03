using Dapper;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Tests.Destinations;
using MappingDemo.Worker.Destinations;
using Npgsql;

namespace MappingDemo.Tests.Worker;

public sealed class LegacyTableDestinationAdapterIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Reconcile_replaces_the_output_set_and_rolls_back_atomically()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"writer_test_{Guid.NewGuid():N}"[..32];
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
            var router = DestinationRouterTestFactory.Create(
                new LegacyTableDestinationAdapter());
            var compilation = router.Compile(
                new LegacyTableDestination(301),
                new DestinationCompilationContext(TargetSchema(), []));
            Assert.True(compilation.IsSuccess);
            var destination = compilation.CompiledDestination!;
            await connection.ExecuteAsync("""
                CREATE TABLE row_jobs (
                    id bigint PRIMARY KEY,
                    status text NOT NULL,
                    attempts integer NOT NULL DEFAULT 0,
                    last_error text,
                    finished_at timestamptz
                );
                CREATE TABLE normalized_rows (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    source_row_id bigint NOT NULL,
                    output_key text NOT NULL,
                    file_job_id bigint NOT NULL,
                    row_number integer NOT NULL,
                    row_job_id bigint NOT NULL,
                    config_version_id bigint NOT NULL,
                    normalized_at timestamptz NOT NULL DEFAULT now(),
                    value integer,
                    UNIQUE (source_row_id, output_key)
                );
                INSERT INTO row_jobs (id, status)
                SELECT id, 'Pending'
                FROM generate_series(1, 7) AS id;
                """);

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 1,
                Output("pmx", 10),
                Output("ctp", 20));
            await AssertOutputsAsync(connection, ("ctp", 20), ("pmx", 10));
            await AssertStatusAsync(connection, 1, RowJobStatus.Done);

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 2,
                Output("pmx", 11));
            await AssertOutputsAsync(connection, ("pmx", 11));

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 3,
                Output("ctp", 21));
            await AssertOutputsAsync(connection, ("ctp", 21));

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 4);
            await AssertOutputsAsync(connection);
            await AssertStatusAsync(connection, 4, RowJobStatus.Filtered);

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 4);
            await AssertOutputsAsync(connection);
            await AssertStatusAsync(connection, 4, RowJobStatus.Filtered);

            await ReconcileAsync(
                router,
                connection,
                destination,
                rowJobId: 5,
                Output("pmx", 30),
                Output("ctp", 40));

            await using var failedTransaction =
                await connection.BeginTransactionAsync();
            await Assert.ThrowsAsync<PostgresException>(async () =>
                await router.DeliverAsync(
                    destination,
                    Envelope(
                        rowJobId: 6,
                        Output("pmx", 31),
                        Output("ctp", "not-an-integer")),
                    connection,
                    failedTransaction));
            await failedTransaction.RollbackAsync();

            await AssertOutputsAsync(connection, ("ctp", 40), ("pmx", 30));
            await AssertStatusAsync(connection, 6, RowJobStatus.Pending);

            await connection.ExecuteAsync("""
                UPDATE row_jobs SET status = 'Invalid' WHERE id = 6;
                UPDATE row_jobs SET status = 'Failed' WHERE id = 7;
                """);
            await AssertOutputsAsync(connection, ("ctp", 40), ("pmx", 30));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task ReconcileAsync(
        IDestinationRouter router,
        NpgsqlConnection connection,
        CompiledDestination destination,
        long rowJobId,
        params TransformationOutputResult[] outputs)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await router.DeliverAsync(
            destination,
            Envelope(rowJobId, outputs),
            connection,
            transaction);
        await transaction.CommitAsync();
        Assert.Equal(
            outputs.Length == 0 ? RowJobStatus.Filtered : RowJobStatus.Done,
            result.CompletionStatus);
    }

    private static DeliveryEnvelope Envelope(
        long rowJobId,
        params TransformationOutputResult[] outputs)
    {
        return new DeliveryEnvelope(
            SourceRowId: 101,
            FileJobId: 201,
            RowNumber: 1,
            RowJobId: rowJobId,
            ConfigVersionId: 301,
            Outputs: outputs,
            Attempts: 1);
    }

    private static TargetSchema TargetSchema()
    {
        return new LegacyTableTargetSchemaAdapter().Adapt(
            new TableDefinition(
                "normalized_rows",
                TableKind.Normalized,
                [new ColumnDefinition("value", ColumnDataType.Integer, false, 1)]));
    }

    private static TransformationOutputResult Output(
        string outputKey,
        object value)
    {
        return new TransformationOutputResult(
            outputKey,
            new Dictionary<string, object?> { ["value"] = value });
    }

    private static async Task AssertOutputsAsync(
        NpgsqlConnection connection,
        params (string OutputKey, int Value)[] expected)
    {
        var actual = (await connection.QueryAsync<OutputRow>("""
                SELECT output_key AS "OutputKey", value AS "Value"
                FROM normalized_rows
                WHERE source_row_id = 101
                ORDER BY output_key;
                """))
            .Select(row => (row.OutputKey, row.Value))
            .ToArray();

        Assert.Equal(expected, actual);
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

    private sealed record OutputRow(string OutputKey, int Value);
}
