using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Input;

public sealed class InputRecordModule
{
    public InputLayoutCompilationResult Compile(
        InputLayoutDefinition layout,
        TableDefinition sourceTable)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(sourceTable);

        return InputLayoutCompiler.Compile(layout, sourceTable);
    }

    public async Task<InputRecordReadResult> ReadAsync(
        Stream input,
        CompiledInputLayout layout,
        CancellationToken cancellationToken = default)
    {
        return await ReadAsync(
            input,
            layout,
            fileExtension: null,
            InputReadLimits.Default,
            cancellationToken);
    }

    public async Task<InputRecordReadResult> ReadAsync(
        Stream input,
        CompiledInputLayout layout,
        string? fileExtension,
        InputReadLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Definition is ExcelInputLayout excel)
        {
            return await ExcelWorkbookReader.ReadAsync(
                input,
                excel,
                fileExtension,
                limits,
                cancellationToken);
        }

        var decoded = await Utf8InputDecoder.DecodeAsync(input, cancellationToken);
        if (decoded.Error is not null)
        {
            return InputRecordReadResult.Failure(decoded.Error);
        }

        return layout.Definition switch
        {
            DelimitedInputLayout delimited =>
                await DelimitedInputReader.ReadAsync(
                    decoded.Text!,
                    delimited,
                    cancellationToken),
            FixedWidthInputLayout fixedWidth =>
                FixedWidthInputReader.Read(decoded.Text!, fixedWidth),
            _ => throw new InvalidOperationException(
                "Compiled input layout kind is not supported.")
        };
    }
}
