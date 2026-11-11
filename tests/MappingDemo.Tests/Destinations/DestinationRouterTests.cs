using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Worker;
using MappingDemo.Worker.Destinations;
using Npgsql;
using System.Runtime.CompilerServices;

namespace MappingDemo.Tests.Destinations;

public sealed class DestinationRouterTests
{
    [Fact]
    public async Task Router_uses_one_interface_for_every_declared_destination()
    {
        var legacy = new RecordingAdapter(typeof(LegacyTableDestination));
        var api = new RecordingAdapter(typeof(ApiContractDestination));
        var json = new RecordingAdapter(typeof(JsonFileDestination));
        var database = new RecordingAdapter(typeof(DatabaseTableDestination));
        IDestinationRouter router = new DestinationRouter(
            [legacy, api, json, database]);
        var context = new DestinationCompilationContext(TargetSchema(), []);
        var envelope = new DeliveryEnvelope(1, 2, 3, 4, 5, [], 1);
        using var connection = new NpgsqlConnection();
        var transaction = (NpgsqlTransaction)RuntimeHelpers
            .GetUninitializedObject(typeof(NpgsqlTransaction));
        DestinationDefinition[] definitions =
        [
            new LegacyTableDestination(10),
            new ApiContractDestination("api", Contract()),
            new JsonFileDestination(
                "file",
                Contract(),
                "exports",
                "{fileJobId}.json",
                "compact"),
            DatabaseDestination()
        ];

        foreach (var definition in definitions)
        {
            var compilation = router.Compile(definition, context);
            Assert.True(compilation.IsSuccess);

            var delivered = await router.DeliverAsync(
                compilation.CompiledDestination!,
                envelope,
                connection,
                transaction,
                CancellationToken.None);

            Assert.Equal("Done", delivered.CompletionStatus);
        }

        Assert.Equal(1, legacy.CompileCalls);
        Assert.Equal(1, legacy.DeliverCalls);
        Assert.Equal(1, api.CompileCalls);
        Assert.Equal(1, api.DeliverCalls);
        Assert.Equal(1, json.CompileCalls);
        Assert.Equal(1, json.DeliverCalls);
        Assert.Equal(1, database.CompileCalls);
        Assert.Equal(1, database.DeliverCalls);
    }

    [Fact]
    public void Router_fails_fast_when_a_declared_adapter_is_missing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DestinationRouter(
            [
                new RecordingAdapter(typeof(LegacyTableDestination)),
                new RecordingAdapter(typeof(ApiContractDestination))
            ]));

        Assert.Contains("JsonFileDestination", exception.Message);
    }

    [Fact]
    public void Router_fails_fast_when_an_adapter_is_registered_twice()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DestinationRouter(
            [
                new RecordingAdapter(typeof(LegacyTableDestination)),
                new RecordingAdapter(typeof(LegacyTableDestination)),
                new RecordingAdapter(typeof(ApiContractDestination)),
                new RecordingAdapter(typeof(JsonFileDestination))
            ]));

        Assert.Contains("LegacyTableDestination", exception.Message);
    }

    [Fact]
    public void Row_normalize_handler_depends_on_the_router_seam_only()
    {
        var constructors = typeof(RowNormalizeHandler).GetConstructors();

        Assert.NotEmpty(constructors);
        Assert.All(
            constructors,
            constructor => Assert.Contains(
                constructor.GetParameters(),
                parameter => parameter.ParameterType ==
                    typeof(IDestinationRouter)));
        Assert.DoesNotContain(
            constructors.SelectMany(constructor =>
                constructor.GetParameters()),
            parameter => typeof(IDestinationAdapter).IsAssignableFrom(
                parameter.ParameterType));
    }

    private static ContractReference Contract() =>
        new("proposal", "1", "proposal/1", "sha256:abc");

    private static DatabaseTableDestination DatabaseDestination()
    {
        var schema = TargetTableSchema.Create(
            "database",
            DatabaseProvider.PostgreSql,
            "public",
            "customer",
            [],
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

    private static TargetSchema TargetSchema() =>
        new(new TargetNode(
            "root",
            TargetNodeKind.Object,
            false,
            [],
            properties: []));

    private sealed class RecordingAdapter(Type destinationType)
        : IDestinationAdapter
    {
        public Type DestinationType { get; } = destinationType;

        public int CompileCalls { get; private set; }

        public int DeliverCalls { get; private set; }

        public DestinationCompilationResult Compile(
            DestinationDefinition destination,
            DestinationCompilationContext context)
        {
            CompileCalls++;
            return new DestinationCompilationResult(
                new RecordingCompiledDestination(),
                []);
        }

        public ValueTask<RowDeliveryResult> DeliverAsync(
            CompiledDestination destination,
            DeliveryEnvelope envelope,
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            DeliverCalls++;
            return ValueTask.FromResult(new RowDeliveryResult("Done", []));
        }
    }

    private sealed class RecordingCompiledDestination : CompiledDestination;
}
