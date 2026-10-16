using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Transformation;

public sealed record TransformationSchema(
    TableDefinition SourceTable,
    TableDefinition NormalizedTable);
