using MappingDemo.Shared.Tables;

namespace MappingDemo.Api.Contracts.Tables;

public sealed record ColumnRequest(
    string Name,
    ColumnDataType DataType,
    bool IsRequired,
    int? Length = null,
    int? Precision = null,
    int? Scale = null);
