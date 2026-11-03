using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Transformation;
using Npgsql;

namespace MappingDemo.Shared.Destinations;

public interface IDestinationRouter
{
    DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context);

    ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}

public interface IDestinationAdapter
{
    Type DestinationType { get; }

    DestinationCompilationResult Compile(
        DestinationDefinition destination,
        DestinationCompilationContext context);

    ValueTask<RowDeliveryResult> DeliverAsync(
        CompiledDestination destination,
        DeliveryEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}

public sealed record DestinationCompilationContext(
    TargetSchema TargetSchema,
    IReadOnlyList<PayloadBinding> PayloadBindings,
    long? ApiDestinationProfileVersionId = null);

public sealed class DestinationCompilationResult
{
    public DestinationCompilationResult(
        CompiledDestination? compiledDestination,
        IReadOnlyList<DestinationDiagnostic> diagnostics)
    {
        CompiledDestination = compiledDestination;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess => CompiledDestination is not null;

    public CompiledDestination? CompiledDestination { get; }

    public IReadOnlyList<DestinationDiagnostic> Diagnostics { get; }
}

public abstract class CompiledDestination;

public sealed record DestinationDiagnostic(
    string Path,
    string Code,
    string Message);

public sealed record DeliveryEnvelope(
    long SourceRowId,
    long FileJobId,
    int RowNumber,
    long RowJobId,
    long ConfigVersionId,
    IReadOnlyList<TransformationOutputResult> Outputs,
    int Attempts,
    string OperationKind = "Initial",
    string FileName = "");

public sealed record RowDeliveryResult(
    string CompletionStatus,
    IReadOnlyList<RowDeliveryDiagnostic> Diagnostics);

public sealed record RowDeliveryDiagnostic(
    string OutputKey,
    string Path,
    string Reason);
