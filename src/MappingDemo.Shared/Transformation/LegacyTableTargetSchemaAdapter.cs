using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Transformation;

public sealed class LegacyTableTargetSchemaAdapter
{
    public TargetSchema Adapt(TableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var properties = table.Columns
            .OrderBy(column => column.Ordinal)
            .Select(ToTargetNode)
            .ToArray();

        return new TargetSchema(
            new TargetNode(
                table.Name,
                TargetNodeKind.Object,
                isRequired: true,
                pathTokens: [],
                properties: properties));
    }

    private static TargetNode ToTargetNode(ColumnDefinition column)
    {
        return new TargetNode(
            column.Name,
            TargetNodeKind.Scalar,
            column.IsRequired,
            [column.Name],
            ToTargetScalarType(column.DataType),
            new TargetConstraints(
                column.Length,
                column.Precision,
                column.Scale));
    }

    private static TargetScalarType ToTargetScalarType(ColumnDataType dataType)
    {
        return dataType switch
        {
            ColumnDataType.Text => TargetScalarType.Text,
            ColumnDataType.Date => TargetScalarType.Date,
            ColumnDataType.Decimal => TargetScalarType.Decimal,
            ColumnDataType.Boolean => TargetScalarType.Boolean,
            ColumnDataType.Integer => TargetScalarType.Integer,
            ColumnDataType.DateTime => TargetScalarType.DateTime,
            ColumnDataType.Guid => TargetScalarType.Guid,
            _ => throw new ArgumentOutOfRangeException(
                nameof(dataType),
                dataType,
                "Unsupported legacy column type.")
        };
    }
}
