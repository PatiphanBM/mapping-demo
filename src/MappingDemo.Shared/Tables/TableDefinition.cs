namespace MappingDemo.Shared.Tables;

public sealed record TableDefinition(
    string Name,
    TableKind Kind,
    IReadOnlyList<ColumnDefinition> Columns);
