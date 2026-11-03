namespace MappingDemo.Shared.Input;

public sealed class InputRecordReadResult
{
    private InputRecordReadResult(
        IReadOnlyList<InputRecord> records,
        InputRecordReadError? error,
        WorkbookReadMetadata? workbook = null)
    {
        Records = records;
        Error = error;
        Workbook = workbook;
    }

    public bool IsSuccess => Error is null;

    public IReadOnlyList<InputRecord> Records { get; }

    public InputRecordReadError? Error { get; }

    public WorkbookReadMetadata? Workbook { get; }

    internal static InputRecordReadResult Success(
        IReadOnlyList<InputRecord> records,
        WorkbookReadMetadata? workbook = null)
    {
        return new InputRecordReadResult(records, null, workbook);
    }

    internal static InputRecordReadResult Failure(InputRecordReadError error)
    {
        return new InputRecordReadResult([], error);
    }
}

public sealed record InputRecord(
    int? RowNumber,
    int PhysicalLineNumber,
    InputRecordAction Action,
    string? RecordRule,
    IReadOnlyDictionary<string, string> Values,
    string RawRecord,
    int? WorksheetRowNumber = null);

public sealed record WorkbookReadMetadata(
    IReadOnlyList<string> Sheets,
    string SelectedSheet,
    IReadOnlyList<string> Headers);

public sealed record InputReadLimits(
    long MaxBytes = 10 * 1024 * 1024,
    int MaxRows = 10_000,
    int MaxColumns = 256,
    int MaxCellCharacters = 32_768,
    TimeSpan? MaxElapsed = null)
{
    public static InputReadLimits Default { get; } = new();
}

public sealed record InputRecordReadError(
    string Code,
    int PhysicalLineNumber,
    string? RecordRule = null,
    string? ExpectedNext = null,
    int? Expected = null,
    int? Actual = null,
    int? MinOccurs = null,
    int? MaxOccurs = null,
    int? Position = null,
    string? Scalar = null,
    string? Discriminator = null,
    long? ByteOffset = null);
