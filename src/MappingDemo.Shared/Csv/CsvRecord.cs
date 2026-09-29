namespace MappingDemo.Shared.Csv;

public sealed record CsvRecord(
    int RowNumber,
    IReadOnlyDictionary<string, string> Values);
