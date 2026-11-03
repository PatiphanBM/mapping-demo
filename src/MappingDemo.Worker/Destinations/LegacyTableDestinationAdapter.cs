using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using MappingDemo.Worker;
using Npgsql;

namespace MappingDemo.Worker.Destinations;

public sealed class LegacyTableDestinationAdapter : IDestinationAdapter
{
    public Type DestinationType { get; } = typeof(LegacyTableDestination);

    public DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(context);
        var targetSchema = context.TargetSchema;

        if (destination is not LegacyTableDestination legacyTable)
        {
            return Failure(
                "destination.kind",
                "UnsupportedDestinationKind",
                $"Destination kind '{DestinationKind(destination)}' is not supported by the legacy table adapter.");
        }

        if (legacyTable.NormalizedTableId <= 0)
        {
            return Failure(
                "destination.normalizedTableId",
                "InvalidNormalizedTableId",
                "Normalized table ID must be a positive integer.");
        }

        if (targetSchema.Root.Kind != TargetNodeKind.Object
            || targetSchema.Root.Properties.Any(node =>
                node.Kind != TargetNodeKind.Scalar
                || node.PathTokens.Count != 1))
        {
            return Failure(
                "targetSchema.root",
                "LegacyTargetSchemaMustBeFlat",
                "Legacy table delivery requires a flat object with scalar properties.");
        }

        var compiled = new CompiledLegacyTableDestination(
            targetSchema.Root.Name,
            targetSchema.Root.Properties.Select(node => node.Name));
        return new DestinationCompilationResult(compiled, []);
    }

    public async ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Outputs);

        if (destination is not CompiledLegacyTableDestination legacyTable)
        {
            throw new ArgumentException(
                "The compiled destination was not produced by the legacy table adapter.",
                nameof(destination));
        }

        var completionStatus = await NormalizedRowWriter
            .ReconcileAndCompleteAsync(
                connection,
                transaction,
                legacyTable.TableName,
                envelope.SourceRowId,
                envelope.FileJobId,
                envelope.RowNumber,
                envelope.RowJobId,
                envelope.ConfigVersionId,
                legacyTable.ColumnNames,
                envelope.Outputs,
                envelope.Attempts,
                cancellationToken);
        return new RowDeliveryResult(completionStatus, []);
    }

    private static DestinationCompilationResult Failure(
        string path,
        string code,
        string message)
    {
        return new DestinationCompilationResult(
            null,
            [new DestinationDiagnostic(path, code, message)]);
    }

    private static string DestinationKind(DestinationDefinition destination)
    {
        return destination switch
        {
            ApiContractDestination => "apiContract",
            JsonFileDestination => "jsonFile",
            LegacyTableDestination => "legacyTable",
            _ => destination.GetType().Name
        };
    }

    private sealed class CompiledLegacyTableDestination
        : CompiledDestination
    {
        public CompiledLegacyTableDestination(
            string tableName,
            IEnumerable<string> columnNames)
        {
            TableName = tableName;
            ColumnNames = Array.AsReadOnly(columnNames.ToArray());
        }

        public string TableName { get; }

        public IReadOnlyList<string> ColumnNames { get; }
    }
}
