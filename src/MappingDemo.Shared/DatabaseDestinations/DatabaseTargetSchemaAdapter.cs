using MappingDemo.Shared.Transformation;

namespace MappingDemo.Shared.DatabaseDestinations;

public static class DatabaseTargetSchemaAdapter
{
    public static TargetSchema Adapt(
        TargetSchemaSnapshot snapshot,
        IReadOnlySet<string>? requiredKeyColumns = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var properties = snapshot.Columns
            .Where(column => column.IsWritable &&
                column.ScalarType is not DatabaseScalarType.Binary and
                    not DatabaseScalarType.Unsupported)
            .OrderBy(column => column.Ordinal)
            .Select(column => new TargetNode(
                column.Name,
                TargetNodeKind.Scalar,
                column.IsRequired ||
                    (requiredKeyColumns?.Contains(column.Name) ?? false),
                [column.Name],
                ToTargetType(column.ScalarType),
                new TargetConstraints(
                    Length: column.Length,
                    Precision: column.Precision,
                    Scale: column.Scale),
                isNullable: column.IsNullable))
            .ToArray();
        return new TargetSchema(new TargetNode(
            snapshot.Table,
            TargetNodeKind.Object,
            isRequired: true,
            [],
            properties: properties));
    }

    private static TargetScalarType ToTargetType(DatabaseScalarType type) =>
        type switch
        {
            DatabaseScalarType.Text => TargetScalarType.Text,
            DatabaseScalarType.Integer => TargetScalarType.Integer,
            DatabaseScalarType.Decimal => TargetScalarType.Decimal,
            DatabaseScalarType.Boolean => TargetScalarType.Boolean,
            DatabaseScalarType.Date => TargetScalarType.Date,
            DatabaseScalarType.Time => TargetScalarType.Time,
            DatabaseScalarType.DateTime => TargetScalarType.DateTime,
            DatabaseScalarType.Guid => TargetScalarType.Guid,
            _ => throw new InvalidOperationException(
                $"Database scalar type '{type}' is not assignable.")
        };
}
