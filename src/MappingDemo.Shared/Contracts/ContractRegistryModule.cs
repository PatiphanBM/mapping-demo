using System.Text;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Shared.Contracts;

public interface IContractProfile
{
    string Id { get; }

    bool Enabled { get; }

    int TimeoutSeconds { get; }

    IReadOnlyList<SupportedContract> SupportedContracts { get; }

    IReadOnlyList<PayloadBinding> PayloadBindings { get; }
}

public interface IContractSchemaFetcher
{
    ValueTask<ContractSchemaFetchResult> FetchAsync(
        IContractProfile profile,
        ContractReference contract,
        CancellationToken cancellationToken);
}

public sealed record ContractSchemaFetchResult(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    DateTimeOffset FetchedAt);

public sealed class ContractRegistryModule(
    IApiDestinationProfileCatalog profiles,
    IContractSchemaFetcher fetcher,
    ContractSchemaModule? schemas = null,
    FileDestinationProfileCatalog? fileProfiles = null)
{
    public const int MaxSchemaBytes = 1024 * 1024;

    private readonly ContractSchemaModule _schemas = schemas ?? new ContractSchemaModule();

    public async ValueTask<ContractSnapshotResult> ResolveAsync(
        string profileId,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        var profileResult = await profiles.ResolveActiveAsync(
            profileId,
            cancellationToken);
        if (!profileResult.IsSuccess)
        {
            return ContractSnapshotResult.Failure(profileResult.Diagnostics);
        }

        return await ResolveProfileAsync(
            profileResult.Profile!,
            contract,
            cancellationToken);
    }

    public async ValueTask<ContractSnapshotResult> ResolveAsync(
        DestinationDefinition destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination is ApiContractDestination api)
        {
            return await ResolveAsync(
                api.ApiDestinationProfileId,
                api.Contract,
                cancellationToken);
        }
        if (destination is not JsonFileDestination jsonFile)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic(
                    "destination.kind",
                    "ContractDestinationRequired",
                    "Destination does not use a Contract Snapshot.")
            ]);
        }
        return await ResolveFileAsync(
            jsonFile.FileDestinationProfileId,
            jsonFile.Contract,
            cancellationToken);
    }

    public async ValueTask<ContractSnapshotResult> ResolveFileAsync(
        string profileId,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        if (fileProfiles is null)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic(
                    "profileId",
                    "UnknownProfile",
                    $"File destination profile '{profileId}' does not exist.")
            ]);
        }

        var profileResult = fileProfiles.ResolveForCreation(profileId);
        if (!profileResult.IsSuccess)
        {
            return ContractSnapshotResult.Failure(profileResult.Diagnostics);
        }

        return await ResolveProfileAsync(
            profileResult.Profile!,
            contract,
            cancellationToken);
    }

    private async ValueTask<ContractSnapshotResult> ResolveProfileAsync(
        IContractProfile profile,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        var supported = profile.SupportedContracts.SingleOrDefault(item =>
            item.Id == contract.Id && item.Version == contract.Version &&
            item.Reference == contract.Reference);
        if (supported is null)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic("contract", "UnsupportedContract", "The contract is not supported by this profile.")
            ]);
        }

        ContractSchemaFetchResult fetch;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
            fetch = await fetcher.FetchAsync(profile, contract, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic("contract", "ContractFetchTimeout", "Contract schema fetch timed out.")
            ]);
        }
        catch (ContractFetchException exception)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic("contract", exception.Code, exception.Message)
            ]);
        }

        if (fetch.Content.Length > MaxSchemaBytes)
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic("contract", "SchemaTooLarge", "Contract schema exceeds the size limit.")
            ]);
        }

        if (!fetch.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !fetch.ContentType.Equals("application/schema+json", StringComparison.OrdinalIgnoreCase))
        {
            return ContractSnapshotResult.Failure([
                new ContractDiagnostic("contract", "UnsupportedContentType", "Contract schema content type must be JSON.")
            ]);
        }

        var compilation = _schemas.Compile(Encoding.UTF8.GetString(fetch.Content.Span));
        if (!compilation.IsSuccess)
        {
            return ContractSnapshotResult.Failure(compilation.Diagnostics);
        }

        var bindingDiagnostics = ValidateBindings(
            compilation.TargetSchema!,
            profile.PayloadBindings);
        if (bindingDiagnostics.Count > 0)
        {
            return ContractSnapshotResult.Failure(bindingDiagnostics);
        }

        var authoritative = contract with { SchemaHash = compilation.SchemaHash! };
        return new ContractSnapshotResult(
            authoritative,
            compilation.CanonicalSchema,
            compilation.SchemaHash,
            fetch.FetchedAt,
            compilation.TargetSchema,
            compilation.PathCatalog,
            profile.PayloadBindings.ToArray(),
            profile as ApiDestinationProfile,
            []);
    }

    private static IReadOnlyList<ContractDiagnostic> ValidateBindings(
        TargetSchema schema,
        IReadOnlyList<PayloadBinding> bindings)
    {
        var diagnostics = new List<ContractDiagnostic>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            var resolved = TargetPath.Resolve(schema, binding.TargetPath);
            if (resolved is null)
            {
                diagnostics.Add(new(binding.TargetPath, "UnknownBindingPath", "Payload binding target path does not exist."));
                continue;
            }
            if (resolved.Node.Kind != TargetNodeKind.Scalar)
            {
                diagnostics.Add(new(binding.TargetPath, "BindingTargetNotScalar", "Payload binding target must be a scalar leaf."));
                continue;
            }
            if (!paths.Add(binding.TargetPath))
            {
                diagnostics.Add(new(binding.TargetPath, "DuplicatePayloadBinding", "Payload binding target is duplicated."));
                continue;
            }
            var expectedInteger = binding.Source is RuntimeValueSource.FileJobId
                or RuntimeValueSource.SourceRowId
                or RuntimeValueSource.SourceRowNumber;
            var valid = expectedInteger
                ? resolved.Node.ScalarType is TargetScalarType.Integer or TargetScalarType.Text
                : resolved.Node.ScalarType == TargetScalarType.Text;
            if (!valid)
            {
                diagnostics.Add(new(binding.TargetPath, "BindingTypeMismatch", $"Runtime source '{binding.Source}' is incompatible with the target type."));
            }
        }
        return diagnostics;
    }
}

public sealed record ContractSnapshotResult(
    ContractReference? Contract,
    string? CanonicalSchema,
    string? SchemaHash,
    DateTimeOffset? FetchedAt,
    TargetSchema? TargetSchema,
    IReadOnlyList<ContractPathDescriptor> PathCatalog,
    IReadOnlyList<PayloadBinding> PayloadBindings,
    ApiDestinationProfile? Profile,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => TargetSchema is not null;

    public static ContractSnapshotResult Failure(IReadOnlyList<ContractDiagnostic> diagnostics) =>
        new(null, null, null, null, null, [], [], null, diagnostics);
}

public sealed class ContractFetchException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}
