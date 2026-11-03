namespace MappingDemo.Shared.Input;

public static class InputCapabilities
{
    public const string DelimitedFormat = "delimited";

    public const string FixedWidthFormat = "fixedWidth";

    public const string ExcelFormat = "excel";

    public const string Utf8Encoding = "utf-8";

    public const string Utf16CodeUnitPosition = "utf16CodeUnit";

    public static IReadOnlyList<string> Formats { get; } =
        Array.AsReadOnly([DelimitedFormat, FixedWidthFormat, ExcelFormat]);

    public static IReadOnlyList<string> Encodings { get; } =
        Array.AsReadOnly([Utf8Encoding]);

    public static IReadOnlyList<string> PositionUnits { get; } =
        Array.AsReadOnly([Utf16CodeUnitPosition]);
}
