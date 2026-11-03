namespace MappingDemo.Shared.Tables;

public static class TablePhysicalSchema
{
    public static TableSchemaSnapshot Expected(TableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var columns = definition.Kind switch
        {
            TableKind.Source => SourceSystemColumns(),
            TableKind.Normalized => NormalizedSystemColumns(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.Kind,
                "Unsupported table kind.")
        };
        columns.AddRange(definition.Columns
            .OrderBy(column => column.Ordinal)
            .Select(column => new TableSchemaColumn(
                column.Name,
                DataType(column),
                IsNullable: true,
                IsIdentity: false)));

        var uniqueColumns = definition.Kind == TableKind.Source
            ? new[] { "file_job_id", "row_number" }
            : new[] { "source_row_id", "output_key" };

        return new TableSchemaSnapshot(
            columns,
            new TableSchemaConstraint("PrimaryKey", ["id"]),
            [new TableSchemaConstraint("Unique", uniqueColumns)]);
    }

    public static string DataType(ColumnDefinition column) =>
        column.DataType switch
        {
            ColumnDataType.Text when column.Length is not null =>
                $"character varying({column.Length})",
            ColumnDataType.Text => "text",
            ColumnDataType.Date => "date",
            ColumnDataType.Decimal when column.Precision is not null &&
                                        column.Scale is not null =>
                $"numeric({column.Precision},{column.Scale})",
            ColumnDataType.Decimal when column.Precision is not null =>
                $"numeric({column.Precision},0)",
            ColumnDataType.Decimal => "numeric",
            ColumnDataType.Boolean => "boolean",
            ColumnDataType.Integer => "integer",
            ColumnDataType.DateTime => "timestamp without time zone",
            ColumnDataType.Guid => "uuid",
            _ => throw new ArgumentOutOfRangeException(
                nameof(column),
                column.DataType,
                "Unsupported column data type.")
        };

    private static List<TableSchemaColumn> SourceSystemColumns() =>
    [
        Column("id", "bigint", isIdentity: true),
        Column("file_job_id", "bigint"),
        Column("row_number", "integer"),
        Column("physical_line_number", "integer"),
        Column("imported_at", "timestamp with time zone")
    ];

    private static List<TableSchemaColumn> NormalizedSystemColumns() =>
    [
        Column("id", "bigint", isIdentity: true),
        Column("source_row_id", "bigint"),
        Column("output_key", "text"),
        Column("file_job_id", "bigint"),
        Column("row_number", "integer"),
        Column("row_job_id", "bigint"),
        Column("config_version_id", "bigint"),
        Column("normalized_at", "timestamp with time zone")
    ];

    private static TableSchemaColumn Column(
        string name,
        string dataType,
        bool isIdentity = false) =>
        new(name, dataType, IsNullable: false, isIdentity);
}
