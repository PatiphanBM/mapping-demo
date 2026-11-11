using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Jobs;
using MappingDemo.Worker.DatabaseDelivery;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class DatabaseTableDestinationAdapter(
    DatabaseDeliveryIntentModule intentModule)
    : IDestinationAdapter
{
    public Type DestinationType { get; } = typeof(DatabaseTableDestination);

    public DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context)
    {
        if (destination is not DatabaseTableDestination database)
        {
            return Failure(
                "destination.kind",
                "UnsupportedDestinationKind",
                "The database table adapter only supports databaseTable destinations.");
        }

        var errors = MappingDemo.Shared.MappingConfigs
            .DestinationDefinitionValidator.Validate(database);
        if (errors.Count > 0)
        {
            return new DestinationCompilationResult(
                null,
                errors.Select(error => new DestinationDiagnostic(
                    error.Field,
                    "InvalidDatabaseTableDestination",
                    error.Message)).ToArray());
        }

        return new DestinationCompilationResult(
            new CompiledDatabaseTableDestination(database),
            []);
    }

    public async ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (destination is not CompiledDatabaseTableDestination database)
        {
            throw new ArgumentException(
                "The compiled destination was not produced by the database table adapter.",
                nameof(destination));
        }

        foreach (var output in envelope.Outputs)
        {
            var result = await intentModule.PersistAsync(
                new DatabaseDeliveryIntent(
                    envelope.FileJobId,
                    envelope.RowJobId,
                    envelope.SourceRowId,
                    envelope.RowNumber,
                    envelope.ConfigVersionId,
                    output.OutputKey,
                    envelope.OperationKind,
                    database.Definition,
                    output.Values),
                connection,
                transaction,
                cancellationToken);
            if (result == DatabaseDeliveryIntentPersistenceResult.Conflict)
            {
                return new RowDeliveryResult(
                    RowJobStatus.Invalid,
                    [new RowDeliveryDiagnostic(
                        output.OutputKey,
                        "destination",
                        "Database delivery intent conflicts with persisted deterministic values.")]);
            }
        }

        return new RowDeliveryResult(
            envelope.Outputs.Count == 0
                ? RowJobStatus.Filtered
                : RowJobStatus.Done,
            []);
    }

    private static DestinationCompilationResult Failure(
        string path,
        string code,
        string message) =>
        new(null, [new DestinationDiagnostic(path, code, message)]);

    private sealed class CompiledDatabaseTableDestination(
        DatabaseTableDestination definition) : CompiledDestination
    {
        public DatabaseTableDestination Definition { get; } = definition;
    }
}
