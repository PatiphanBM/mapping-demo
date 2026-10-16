namespace MappingDemo.Shared.Input;

public sealed class InputRecordReadResult
{
    private InputRecordReadResult(
        IReadOnlyList<InputRecord> records,
        InputRecordReadError? error)
    {
        Records = records;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public IReadOnlyList<InputRecord> Records { get; }

    public InputRecordReadError? Error { get; }

    internal static InputRecordReadResult Success(
        IReadOnlyList<InputRecord> records)
    {
        return new InputRecordReadResult(records, null);
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
    string RawRecord);

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
