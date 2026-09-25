namespace MappingDemo.Shared.MappingConfigs;

public sealed record FileToSourceRule(
    string CsvHeader,
    string SourceColumn);
