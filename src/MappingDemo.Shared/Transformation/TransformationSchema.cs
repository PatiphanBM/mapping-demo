using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Transformation;

public sealed record TransformationSchema(
    TableDefinition SourceTable,
    TargetSchema TargetSchema,
    IReadOnlySet<string>? SystemBoundPaths = null)
{
    public static TransformationSchema FromLegacyTable(
        TableDefinition sourceTable,
        TableDefinition normalizedTable)
    {
        return new TransformationSchema(
            sourceTable,
            new LegacyTableTargetSchemaAdapter().Adapt(normalizedTable));
    }
}
