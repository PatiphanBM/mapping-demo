namespace MappingDemo.Shared.Transformation;

public sealed class TransformationCompilationResult
{
    internal TransformationCompilationResult(
        CompiledTransformationProgram? compiledProgram,
        IReadOnlyList<TransformationDiagnostic> diagnostics)
    {
        CompiledProgram = compiledProgram;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess => CompiledProgram is not null;

    public CompiledTransformationProgram? CompiledProgram { get; }

    public IReadOnlyList<TransformationDiagnostic> Diagnostics { get; }
}

public sealed record TransformationDiagnostic(
    string Path,
    string Code,
    string Message,
    TransformationDiagnosticSeverity Severity);

public enum TransformationDiagnosticSeverity
{
    Error,
    Warning
}

public sealed class CompiledTransformationProgram
{
    internal CompiledTransformationProgram(
        TransformationProgram definition,
        TransformationSchema schema,
        int languageVersion,
        string semanticsProfile,
        IReadOnlyList<CompiledTransformationOutput> outputs,
        IReadOnlySet<string> sourceDependencies,
        IReadOnlyList<CompiledLookupDependency> lookupDependencies)
    {
        Definition = definition;
        Schema = schema;
        LanguageVersion = languageVersion;
        SemanticsProfile = semanticsProfile;
        Outputs = outputs;
        SourceDependencies = sourceDependencies;
        LookupDependencies = lookupDependencies;
    }

    internal TransformationProgram Definition { get; }

    internal TransformationSchema Schema { get; }

    internal int LanguageVersion { get; }

    internal string SemanticsProfile { get; }

    internal IReadOnlyList<CompiledTransformationOutput> Outputs { get; }

    internal IReadOnlySet<string> SourceDependencies { get; }

    internal IReadOnlyList<CompiledLookupDependency> LookupDependencies { get; }
}

internal sealed record CompiledLookupDependency(
    string Capability,
    string Path,
    IReadOnlyDictionary<string, ValueExpression> Arguments);

internal sealed record CompiledTransformationOutput(
    TransformationOutput Definition,
    IReadOnlyList<CompiledFieldAssignment> Fields);

internal sealed record CompiledFieldAssignment(
    FieldAssignment Definition,
    MappingDemo.Shared.Tables.ColumnDefinition TargetColumn,
    InferredExpressionType ValueType,
    TargetAssignmentCoercion Coercion);

internal readonly record struct InferredExpressionType(
    ExpressionValueType Type,
    bool IsNullable);

internal enum TargetAssignmentCoercion
{
    None,
    TrimText,
    TrimAndConvert
}
