namespace MappingDemo.Shared.Transformation;

public sealed class TransformationModule
{
    private readonly ITransformationLookupResolver? _lookupResolver;

    public TransformationModule(
        ITransformationLookupResolver? lookupResolver = null)
    {
        _lookupResolver = lookupResolver;
    }

    public TransformationCompilationResult Compile(
        TransformationProgram program,
        TransformationSchema schema)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(schema);

        return TransformationCompiler.Compile(program, schema);
    }

    public ValueTask<TransformationBatchResult> EvaluateAsync(
        CompiledTransformationProgram program,
        TransformationBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(batch);

        return TransformationEvaluator.EvaluateAsync(
            program,
            batch,
            _lookupResolver,
            cancellationToken);
    }

    public IReadOnlyList<string> GetSourceDependencies(
        CompiledTransformationProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        return program.SourceDependencies
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<TransformationDiagnostic>>
        ValidateDependenciesAsync(
            CompiledTransformationProgram program,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (program.LookupDependencies.Count == 0)
        {
            return [];
        }

        if (_lookupResolver is null)
        {
            return program.LookupDependencies
                .Select(dependency => Unavailable(
                    dependency,
                    $"Lookup '{dependency.Capability}' is not configured."))
                .ToArray();
        }

        var results = new Dictionary<
            string,
            TransformationLookupValidationResult>(StringComparer.Ordinal);
        foreach (var capability in program.LookupDependencies
            .Select(dependency => dependency.Capability)
            .Distinct(StringComparer.Ordinal))
        {
            results[capability] = await _lookupResolver.ValidateAsync(
                capability,
                cancellationToken);
        }

        return program.LookupDependencies
            .Where(dependency => !results[dependency.Capability].IsValid)
            .Select(dependency => Unavailable(
                dependency,
                results[dependency.Capability].Error
                    ?? $"Lookup '{dependency.Capability}' is unavailable."))
            .ToArray();
    }

    private static TransformationDiagnostic Unavailable(
        CompiledLookupDependency dependency,
        string message)
    {
        return new TransformationDiagnostic(
            $"{dependency.Path}.lookup",
            "LookupDependencyUnavailable",
            message,
            TransformationDiagnosticSeverity.Error);
    }
}
