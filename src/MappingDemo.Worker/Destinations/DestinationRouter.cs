using System.Text.Json.Serialization;
using MappingDemo.Shared.Destinations;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class DestinationRouter : IDestinationRouter
{
    private readonly IReadOnlyDictionary<Type, IDestinationAdapter> _adapters;

    public DestinationRouter(IEnumerable<IDestinationAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var declaredTypes = typeof(DestinationDefinition)
            .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
            .Cast<JsonDerivedTypeAttribute>()
            .Select(attribute => attribute.DerivedType)
            .ToHashSet();
        var registered = new Dictionary<Type, IDestinationAdapter>();
        foreach (var adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            if (!declaredTypes.Contains(adapter.DestinationType))
            {
                throw new InvalidOperationException(
                    $"Destination adapter '{adapter.GetType().Name}' registers undeclared type '{adapter.DestinationType.Name}'.");
            }

            if (!registered.TryAdd(adapter.DestinationType, adapter))
            {
                throw new InvalidOperationException(
                    $"Destination adapter for '{adapter.DestinationType.Name}' is registered more than once.");
            }
        }

        var missing = declaredTypes
            .Where(type => !registered.ContainsKey(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Destination adapters are missing for: {string.Join(", ", missing)}.");
        }

        _adapters = registered;
    }

    public DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(context);

        var adapter = _adapters[destination.GetType()];
        var result = adapter.Compile(destination, context);
        return result.CompiledDestination is null
            ? result
            : new DestinationCompilationResult(
                new RoutedCompiledDestination(
                    adapter.DestinationType,
                    result.CompiledDestination),
                result.Diagnostics);
    }

    public ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (destination is not RoutedCompiledDestination routed)
        {
            throw new ArgumentException(
                "The compiled destination was not produced by this router.",
                nameof(destination));
        }

        return _adapters[routed.DestinationType].DeliverAsync(
            routed.Inner,
            envelope,
            connection,
            transaction,
            cancellationToken);
    }

    private sealed class RoutedCompiledDestination(
        Type destinationType,
        CompiledDestination inner) : CompiledDestination
    {
        public Type DestinationType { get; } = destinationType;

        public CompiledDestination Inner { get; } = inner;
    }
}
