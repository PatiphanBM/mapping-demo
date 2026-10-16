using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Services;

public sealed partial class MappingDraftService
{
    public const int MaxSampleBytes = 1024 * 1024;

    public const int MaxSampleRecords = 50;

    public const int MaxRequestBodyBytes = 3 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly InputRecordModule _inputModule;
    private readonly TransformationModule _transformationModule;

    public MappingDraftService(
        InputRecordModule inputModule,
        TransformationModule transformationModule)
    {
        _inputModule = inputModule;
        _transformationModule = transformationModule;
    }

    public async Task<ValidateMappingConfigVersionResponse> ValidateAsync(
        InputLayoutDefinition inputLayout,
        TransformationProgram transformationProgram,
        TransformationSchema schema,
        CancellationToken cancellationToken = default)
    {
        var compilation = await CompileAsync(
            inputLayout,
            transformationProgram,
            schema,
            cancellationToken);

        return new ValidateMappingConfigVersionResponse(
            compilation.Diagnostics.All(diagnostic =>
                diagnostic.Severity != MappingDraftDiagnosticSeverity.Error),
            compilation.Diagnostics);
    }

    public async Task<MappingPreviewResult> PreviewAsync(
        PreviewMappingConfigVersionRequest request,
        TransformationSchema schema,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryParseEvaluationAt(request.EvaluationAt, out var evaluationAt))
        {
            return MappingPreviewResult.Failure(
                Error(
                    "evaluationAt",
                    "InvalidEvaluationAt",
                    "evaluationAt must be ISO 8601 with Z or a UTC offset."));
        }

        var sampleLimitError = ValidateSampleLimits(request.Sample);
        if (sampleLimitError is not null)
        {
            return MappingPreviewResult.Failure(sampleLimitError);
        }

        var compilation = await CompileAsync(
            request.InputLayout,
            request.TransformationProgram,
            schema,
            cancellationToken);
        var errors = compilation.Diagnostics
            .Where(diagnostic =>
                diagnostic.Severity == MappingDraftDiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            return MappingPreviewResult.Failure(errors);
        }

        return request.Sample switch
        {
            TextFilePreviewSample textFile => await PreviewTextFileAsync(
                textFile,
                evaluationAt,
                compilation.InputLayout!,
                compilation.TransformationProgram!,
                cancellationToken),
            SourceRowsPreviewSample sourceRows => await PreviewSourceRowsAsync(
                sourceRows,
                evaluationAt,
                compilation.TransformationProgram!,
                cancellationToken),
            _ => MappingPreviewResult.Failure(
                Error("sample", "UnknownSampleKind", "Sample kind is not supported."))
        };
    }

    private async Task<DraftCompilation> CompileAsync(
        InputLayoutDefinition inputLayout,
        TransformationProgram transformationProgram,
        TransformationSchema schema,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<MappingDraftDiagnostic>();
        var input = _inputModule.Compile(inputLayout, schema.SourceTable);
        diagnostics.AddRange(input.Diagnostics.Select(diagnostic =>
            new MappingDraftDiagnostic(
                $"inputLayout.{diagnostic.Path}",
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Severity == InputLayoutDiagnosticSeverity.Error
                    ? MappingDraftDiagnosticSeverity.Error
                    : MappingDraftDiagnosticSeverity.Warning)));

        var transformation = _transformationModule.Compile(
            transformationProgram,
            schema);
        diagnostics.AddRange(transformation.Diagnostics.Select(diagnostic =>
            new MappingDraftDiagnostic(
                $"transformationProgram.{diagnostic.Path}",
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Severity == TransformationDiagnosticSeverity.Error
                    ? MappingDraftDiagnosticSeverity.Error
                    : MappingDraftDiagnosticSeverity.Warning)));

        if (transformation.CompiledProgram is not null)
        {
            var dependencyDiagnostics =
                await _transformationModule.ValidateDependenciesAsync(
                    transformation.CompiledProgram,
                    cancellationToken);
            diagnostics.AddRange(dependencyDiagnostics.Select(diagnostic =>
                new MappingDraftDiagnostic(
                    $"transformationProgram.{diagnostic.Path}",
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.Severity ==
                    TransformationDiagnosticSeverity.Error
                        ? MappingDraftDiagnosticSeverity.Error
                        : MappingDraftDiagnosticSeverity.Warning)));
        }

        return new DraftCompilation(
            input.CompiledLayout,
            transformation.CompiledProgram,
            diagnostics);
    }

    private async Task<MappingPreviewResult> PreviewTextFileAsync(
        TextFilePreviewSample sample,
        DateTimeOffset evaluationAt,
        CompiledInputLayout inputLayout,
        CompiledTransformationProgram transformationProgram,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(sample.Content);
        }
        catch (EncoderFallbackException)
        {
            return MappingPreviewResult.Failure(
                Error(
                    "sample.content",
                    "InvalidSampleEncoding",
                    "Sample content is not valid UTF-8 text."));
        }

        await using var stream = new MemoryStream(bytes, writable: false);
        var read = await _inputModule.ReadAsync(
            stream,
            inputLayout,
            cancellationToken);
        if (!read.IsSuccess)
        {
            return MappingPreviewResult.Failure(
                Error(
                    "sample.content",
                    read.Error!.Code,
                    FormatInputError(read.Error)));
        }

        if (read.Records.Count > MaxSampleRecords)
        {
            return MappingPreviewResult.Failure(
                Error(
                    "sample.content",
                    "TooManySampleRecords",
                    $"A sample may contain at most {MaxSampleRecords} parsed records."));
        }

        var imported = read.Records
            .Where(record => record.Action == InputRecordAction.Import)
            .Select((record, index) => ToTransformationRow(
                $"record-{record.PhysicalLineNumber}",
                record.Values.ToDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                sample.FileName,
                record.RowNumber!.Value,
                record.PhysicalLineNumber,
                evaluationAt,
                PreviewSourceRowId(index)))
            .ToArray();
        var evaluated = await _transformationModule.EvaluateAsync(
            transformationProgram,
            new TransformationBatch(imported),
            cancellationToken);

        var evaluationIndex = 0;
        var records = new List<MappingPreviewRecordResponse>(read.Records.Count);
        foreach (var record in read.Records)
        {
            var transformation = record.Action == InputRecordAction.Import
                ? evaluated.Rows[evaluationIndex++]
                : null;
            records.Add(new MappingPreviewRecordResponse(
                transformation?.RowKey ?? $"record-{record.PhysicalLineNumber}",
                record.RawRecord,
                record.RowNumber,
                record.PhysicalLineNumber,
                record.Action,
                record.RecordRule,
                record.Values.ToDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                transformation));
        }

        return MappingPreviewResult.Success(
            new PreviewMappingConfigVersionResponse(evaluationAt, records));
    }

    private async Task<MappingPreviewResult> PreviewSourceRowsAsync(
        SourceRowsPreviewSample sample,
        DateTimeOffset evaluationAt,
        CompiledTransformationProgram transformationProgram,
        CancellationToken cancellationToken)
    {
        var rows = sample.Rows.Select((row, index) =>
        {
            var rowNumber = row.RowNumber ?? index + 1;
            var physicalLineNumber = row.PhysicalLineNumber ?? rowNumber;
            var rowKey = string.IsNullOrWhiteSpace(row.RowKey)
                ? $"source-{index + 1}"
                : row.RowKey;
            return ToTransformationRow(
                rowKey,
                row.Values.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                row.FileName ?? "source-row-preview",
                rowNumber,
                physicalLineNumber,
                evaluationAt,
                row.SourceRowId ?? PreviewSourceRowId(index));
        }).ToArray();
        var evaluated = await _transformationModule.EvaluateAsync(
            transformationProgram,
            new TransformationBatch(rows),
            cancellationToken);
        var records = evaluated.Rows.Select((result, index) =>
        {
            var source = sample.Rows[index];
            return new MappingPreviewRecordResponse(
                result.RowKey,
                RawRecord: null,
                source.RowNumber ?? index + 1,
                source.PhysicalLineNumber ?? source.RowNumber ?? index + 1,
                InputRecordAction.Import,
                RecordRule: null,
                source.Values,
                result);
        }).ToArray();

        return MappingPreviewResult.Success(
            new PreviewMappingConfigVersionResponse(evaluationAt, records));
    }

    private static TransformationSourceRow ToTransformationRow(
        string rowKey,
        IReadOnlyDictionary<string, string?> values,
        string fileName,
        int rowNumber,
        int physicalLineNumber,
        DateTimeOffset evaluationAt,
        long? sourceRowId)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fileName"] = fileName,
            ["rowNumber"] = rowNumber,
            ["physicalLineNumber"] = physicalLineNumber,
            ["evaluationTime"] = evaluationAt
        };
        return new TransformationSourceRow(
            rowKey,
            values,
            metadata,
            evaluationAt,
            sourceRowId);
    }

    private static MappingDraftDiagnostic? ValidateSampleLimits(
        MappingPreviewSample? sample)
    {
        if (sample is null)
        {
            return Error("sample", "SampleRequired", "A preview sample is required.");
        }

        if (sample is SourceRowsPreviewSample sourceRows)
        {
            if (sourceRows.Rows is null)
            {
                return Error(
                    "sample.rows",
                    "SampleRowsRequired",
                    "Source rows are required.");
            }

            return sourceRows.Rows.Count > MaxSampleRecords
                ? Error(
                    "sample.rows",
                    "TooManySampleRecords",
                    $"A sample may contain at most {MaxSampleRecords} records.")
                : null;
        }

        if (sample is not TextFilePreviewSample textFile)
        {
            return null;
        }

        if (textFile.Content is null)
        {
            return Error(
                "sample.content",
                "SampleContentRequired",
                "Sample content is required.");
        }

        try
        {
            return StrictUtf8.GetByteCount(textFile.Content) > MaxSampleBytes
                ? Error(
                    "sample.content",
                    "SampleTooLarge",
                    $"Sample content must not exceed {MaxSampleBytes} UTF-8 bytes.")
                : null;
        }
        catch (EncoderFallbackException)
        {
            return Error(
                "sample.content",
                "InvalidSampleEncoding",
                "Sample content is not valid UTF-8 text.");
        }
    }

    private static bool TryParseEvaluationAt(
        string? value,
        out DateTimeOffset evaluationAt)
    {
        evaluationAt = default;
        return value is not null &&
               OffsetSuffixRegex().IsMatch(value) &&
               DateTimeOffset.TryParse(
                   value,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out evaluationAt);
    }

    private static string FormatInputError(InputRecordReadError error)
    {
        return $"{error.Code} at physical line {error.PhysicalLineNumber}.";
    }

    private static long PreviewSourceRowId(int index)
    {
        return long.MinValue + index;
    }

    private static MappingDraftDiagnostic Error(
        string path,
        string code,
        string message)
    {
        return new MappingDraftDiagnostic(
            path,
            code,
            message,
            MappingDraftDiagnosticSeverity.Error);
    }

    [GeneratedRegex(
        "^\\d{4}-\\d{2}-\\d{2}T.+(?:Z|[+-]\\d{2}:\\d{2})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex OffsetSuffixRegex();

    private sealed record DraftCompilation(
        CompiledInputLayout? InputLayout,
        CompiledTransformationProgram? TransformationProgram,
        IReadOnlyList<MappingDraftDiagnostic> Diagnostics);
}

public sealed class MappingPreviewResult
{
    private MappingPreviewResult(
        PreviewMappingConfigVersionResponse? response,
        IReadOnlyList<MappingDraftDiagnostic> diagnostics)
    {
        Response = response;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess => Response is not null;

    public PreviewMappingConfigVersionResponse? Response { get; }

    public IReadOnlyList<MappingDraftDiagnostic> Diagnostics { get; }

    public static MappingPreviewResult Success(
        PreviewMappingConfigVersionResponse response)
    {
        return new MappingPreviewResult(response, []);
    }

    public static MappingPreviewResult Failure(
        params MappingDraftDiagnostic[] diagnostics)
    {
        return new MappingPreviewResult(null, diagnostics);
    }
}
