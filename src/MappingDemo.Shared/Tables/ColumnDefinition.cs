namespace MappingDemo.Shared.Tables;

public sealed record ColumnDefinition(
    string Name,
    ColumnDataType DataType,
    bool IsRequired,
    int Ordinal,
    int? Length = null,
    int? Precision = null,
    int? Scale = null);
