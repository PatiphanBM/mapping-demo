using MappingDemo.Shared.Tables;

namespace MappingDemo.Api.Contracts.Tables;

public sealed record TableResponse(
    long Id,
    string Name,
    TableKind Kind,
    IReadOnlyList<ColumnRequest> Columns);
