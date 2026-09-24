namespace MappingDemo.Shared.Tables;

public sealed record ColumnDefinition(
    string Name,
    ColumnDataType DataType,
    bool IsRequired,
    int Ordinal);
