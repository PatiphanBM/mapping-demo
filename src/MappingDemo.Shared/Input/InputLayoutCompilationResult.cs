namespace MappingDemo.Shared.Input;

public sealed class InputLayoutCompilationResult
{
    internal InputLayoutCompilationResult(
        CompiledInputLayout? compiledLayout,
        IReadOnlyList<InputLayoutDiagnostic> diagnostics)
    {
        CompiledLayout = compiledLayout;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess => CompiledLayout is not null;

    public CompiledInputLayout? CompiledLayout { get; }

    public IReadOnlyList<InputLayoutDiagnostic> Diagnostics { get; }
}

public sealed class CompiledInputLayout
{
    internal CompiledInputLayout(InputLayoutDefinition definition)
    {
        Definition = definition;
    }

    internal InputLayoutDefinition Definition { get; }
}

public sealed record InputLayoutDiagnostic(
    string Path,
    string Code,
    string Message,
    InputLayoutDiagnosticSeverity Severity);

public enum InputLayoutDiagnosticSeverity
{
    Error,
    Warning
}
