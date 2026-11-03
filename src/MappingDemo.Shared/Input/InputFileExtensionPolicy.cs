namespace MappingDemo.Shared.Input;

public static class InputFileExtensionPolicy
{
    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".xlsx", ".xls"
        };

    public static IReadOnlyList<string> DefaultsFor(InputLayoutDefinition layout) =>
        layout switch
        {
            DelimitedInputLayout => [".txt", ".csv"],
            FixedWidthInputLayout => [".txt"],
            ExcelInputLayout excel when excel.WorkbookKind == ExcelWorkbookKind.Xlsx => [".xlsx"],
            ExcelInputLayout excel when excel.WorkbookKind == ExcelWorkbookKind.Xls => [".xls"],
            ExcelInputLayout => [".xlsx", ".xls"],
            _ => []
        };

    public static string Normalize(string extension)
    {
        var value = extension.Trim().ToLowerInvariant();
        return value.StartsWith('.') ? value : $".{value}";
    }

    public static bool IsSupported(string extension) => Supported.Contains(extension);

    public static bool IsCompatible(string extension, InputLayoutDefinition layout) =>
        layout switch
        {
            DelimitedInputLayout => extension is ".txt" or ".csv",
            FixedWidthInputLayout => extension == ".txt",
            ExcelInputLayout excel => extension switch
            {
                ".xlsx" => excel.WorkbookKind is ExcelWorkbookKind.AutoDetect or ExcelWorkbookKind.Xlsx,
                ".xls" => excel.WorkbookKind is ExcelWorkbookKind.AutoDetect or ExcelWorkbookKind.Xls,
                _ => false
            },
            _ => false
        };
}
