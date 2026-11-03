namespace MappingDemo.Shared.Contracts;

public sealed record BundledContractDefinition(
    string ContractKey,
    string DisplayName,
    string Version,
    string Schema,
    DateTimeOffset PublishedAt);

public sealed class BundledVersionedContractRegistry :
    IVersionedContractRegistrySource
{
    private readonly IReadOnlyDictionary<string, BundledEntry> _byReference;
    private readonly IReadOnlyDictionary<long, BundledEntry> _byId;
    private readonly IReadOnlyDictionary<string, ContractAggregate> _aggregates;

    public BundledVersionedContractRegistry(
        IEnumerable<BundledContractDefinition> definitions,
        ContractSchemaModule? schemas = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var compiler = schemas ?? new ContractSchemaModule();
        var entries = definitions
            .OrderBy(item => item.ContractKey, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal)
            .Select((definition, index) => Build(
                definition,
                index + 1,
                compiler))
            .ToArray();
        if (entries.Select(item => item.Details.Reference)
            .Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new ArgumentException(
                "Bundled Contract references must be unique.",
                nameof(definitions));
        }
        _byReference = entries.ToDictionary(
            item => item.Details.Reference,
            StringComparer.Ordinal);
        _byId = entries.ToDictionary(item => item.Details.ContractVersionId);
        _aggregates = entries
            .GroupBy(item => item.Details.ContractKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ContractAggregate(
                    group.First().Details.ContractId,
                    group.Key,
                    group.First().Details.DisplayName,
                    1,
                    group.Select(item => item.Details).ToArray()),
                StringComparer.Ordinal);
    }

    public ValueTask<IReadOnlyList<ContractVersionSummary>>
        ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ContractVersionSummary> values = _byReference.Values
            .OrderBy(item => item.Details.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Details.Reference, StringComparer.Ordinal)
            .Select(item => ToSummary(item.Details))
            .ToArray();
        return ValueTask.FromResult(values);
    }

    public ValueTask<IReadOnlyList<ContractVersionSummary>>
        ListPublishedAsync(CancellationToken cancellationToken = default) =>
        ListAsync(cancellationToken);

    public ValueTask<ContractAggregateResult> GetAsync(
        string contractKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _aggregates.TryGetValue(contractKey, out var aggregate)
                ? new ContractAggregateResult(aggregate, [])
                : VersionedContractRegistryModule.AggregateFailure(
                    "ContractNotFound",
                    $"Contract '{contractKey}' does not exist."));
    }

    public ValueTask<ContractVersionResult> ResolveExactAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _byReference.TryGetValue(reference, out var entry)
                ? new ContractVersionResult(
                    new ResolvedContractVersion(
                        entry.Details,
                        entry.Compilation.TargetSchema!,
                        entry.Compilation.PathCatalog),
                    [])
                : VersionedContractRegistryModule.VersionFailure(
                    "ContractNotFound",
                    $"Contract Version '{reference}' does not exist."));
    }

    public ValueTask<ContractValidationResult> ValidateAsync(
        long contractVersionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byId.TryGetValue(contractVersionId, out var entry))
        {
            return ValueTask.FromResult(new ContractValidationResult(
                false,
                null,
                null,
                null,
                [],
                null,
                null,
                [new ContractDiagnostic(
                    "contract",
                    "ContractNotFound",
                    $"Contract Version '{contractVersionId}' does not exist.")]));
        }
        var compilation = entry.Compilation;
        return ValueTask.FromResult(new ContractValidationResult(
            true,
            compilation.CanonicalSchema,
            compilation.SchemaHash,
            compilation.TargetSchema,
            compilation.PathCatalog,
            ContractCompatibility.Initial,
            null,
            []));
    }

    public ValueTask<ContractMutationResult> ExecuteAsync(
        ContractRegistryCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            VersionedContractRegistryModule.MutationFailure(
                "RegistryReadOnly",
                "The bundled Contract Registry is read-only."));
    }

    private static BundledEntry Build(
        BundledContractDefinition definition,
        long id,
        ContractSchemaModule compiler)
    {
        ValidateIdentity(definition.ContractKey, definition.Version);
        var compilation = compiler.Compile(definition.Schema);
        if (!compilation.IsSuccess)
        {
            var diagnosticSummary = string.Join(
                "; ",
                compilation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
            throw new InvalidOperationException(
                $"Bundled Contract '{definition.ContractKey}/{definition.Version}' is invalid: {diagnosticSummary}");
        }
        var details = new ContractVersionDetails(
            id,
            id,
            definition.ContractKey,
            definition.DisplayName,
            definition.Version,
            $"{definition.ContractKey}/{definition.Version}",
            ContractVersionLifecycle.Published,
            definition.Schema,
            compilation.CanonicalSchema,
            compilation.SchemaHash,
            ContractCompatibility.Initial,
            null,
            1,
            definition.PublishedAt,
            definition.PublishedAt,
            null);
        return new BundledEntry(details, compilation);
    }

    internal static void ValidateIdentity(string contractKey, string version)
    {
        if (string.IsNullOrWhiteSpace(contractKey) ||
            contractKey.Length is < 2 or > 63 ||
            !char.IsAsciiLetterLower(contractKey[0]) ||
            contractKey.Any(character =>
                !(char.IsAsciiLetterLower(character) ||
                  char.IsAsciiDigit(character) ||
                  character == '-')))
        {
            throw new ArgumentException("Contract key is invalid.");
        }
        if (string.IsNullOrWhiteSpace(version) ||
            version.Length > 64 ||
            !char.IsAsciiLetterOrDigit(version[0]) ||
            version.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '.' or '_' or '-')))
        {
            throw new ArgumentException("Contract version is invalid.");
        }
    }

    private static ContractVersionSummary ToSummary(
        ContractVersionDetails value) => new(
        value.ContractVersionId,
        value.ContractKey,
        value.DisplayName,
        value.Version,
        value.Reference,
        value.Lifecycle,
        value.SchemaHash,
        value.ConcurrencyToken);

    private sealed record BundledEntry(
        ContractVersionDetails Details,
        ContractSchemaCompilationResult Compilation);
}
