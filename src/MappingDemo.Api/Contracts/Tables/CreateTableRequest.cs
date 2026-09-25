using MappingDemo.Shared.Tables;

namespace MappingDemo.Api.Contracts.Tables;

public sealed record CreateTableRequest(
    string Name,
    TableKind Kind,
    IReadOnlyList<ColumnRequest> Columns);
