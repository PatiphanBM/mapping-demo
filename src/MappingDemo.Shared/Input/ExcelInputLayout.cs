namespace MappingDemo.Shared.Input;

public sealed record ExcelInputLayout(
    ExcelWorkbookKind WorkbookKind,
    string Sheet,
    int HeaderRow,
    int DataStartRow,
    IReadOnlyList<ExcelInputField> Fields,
    ExcelBlankRowPolicy BlankRowPolicy) : InputLayoutDefinition;

public sealed record ExcelInputField(
    string InputHeader,
    string SourceColumn);

public enum ExcelWorkbookKind
{
    AutoDetect,
    Xlsx,
    Xls
}

public enum ExcelBlankRowPolicy
{
    Skip,
    Error
}
