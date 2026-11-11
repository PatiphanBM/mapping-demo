using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Worker.Destinations;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

internal static class DestinationRouterTestFactory
{
    public static IDestinationRouter Create(params IDestinationAdapter[] adapters)
    {
        var registeredTypes = adapters
            .Select(adapter => adapter.DestinationType)
            .ToHashSet();
        var completed = new List<IDestinationAdapter>(adapters);
        foreach (var destinationType in new[]
        {
            typeof(LegacyTableDestination),
            typeof(ApiContractDestination),
            typeof(JsonFileDestination),
            typeof(DatabaseTableDestination)
        })
        {
            if (!registeredTypes.Contains(destinationType))
            {
                completed.Add(new UnexpectedDestinationAdapter(destinationType));
            }
        }

        return new DestinationRouter(completed);
    }

    private sealed class UnexpectedDestinationAdapter(Type destinationType)
        : IDestinationAdapter
    {
        public Type DestinationType { get; } = destinationType;

        public DestinationCompilationResult Compile(
            DestinationDefinition destination,
            DestinationCompilationContext context)
        {
            throw new InvalidOperationException(
                $"Destination '{DestinationType.Name}' was not expected by this test.");
        }

        public ValueTask<RowDeliveryResult> DeliverAsync(
            CompiledDestination destination,
            DeliveryEnvelope envelope,
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                $"Destination '{DestinationType.Name}' was not expected by this test.");
        }
    }
}
