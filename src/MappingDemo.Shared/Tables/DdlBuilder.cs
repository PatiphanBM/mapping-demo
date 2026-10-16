namespace MappingDemo.Shared.Tables;

public static class DdlBuilder
{
    public static string CreateSourceTable(TableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var lines = new List<string>
        {
            $"CREATE TABLE {SqlIdentifier.Quote(definition.Name)} (",
            "    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,",
            "    file_job_id bigint NOT NULL,",
            "    row_number int NOT NULL,",
            "    physical_line_number int NOT NULL,",
            "    imported_at timestamptz NOT NULL DEFAULT now(),"
        };

        foreach (var column in definition.Columns.OrderBy(column => column.Ordinal))
        {
            lines.Add(
                $"    {SqlIdentifier.Quote(column.Name)} " +
                $"{GetPostgresDataType(column)},");
        }

        lines.Add("    UNIQUE (file_job_id, row_number)");
        lines.Add(");");

        return string.Join(Environment.NewLine, lines);
    }

    public static string CreateNormalizedTable(TableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var lines = new List<string>
        {
            $"CREATE TABLE {SqlIdentifier.Quote(definition.Name)} (",
            "    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,",
            "    source_row_id bigint NOT NULL,",
            "    output_key text NOT NULL,",
            "    file_job_id bigint NOT NULL,",
            "    row_number int NOT NULL,",
            "    row_job_id bigint NOT NULL,",
            "    config_version_id bigint NOT NULL,",
            "    normalized_at timestamptz NOT NULL DEFAULT now(),"
        };

        var orderedColumns = definition.Columns
            .OrderBy(column => column.Ordinal)
            .ToArray();

        for (var index = 0; index < orderedColumns.Length; index++)
        {
            var column = orderedColumns[index];
            lines.Add(
                $"    {SqlIdentifier.Quote(column.Name)} " +
                $"{GetPostgresDataType(column)},");
        }

        lines.Add("    UNIQUE (source_row_id, output_key)");
        lines.Add(");");

        return string.Join(Environment.NewLine, lines);
    }

    public static string AddColumn(
        string tableName,
        ColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return $"ALTER TABLE {SqlIdentifier.Quote(tableName)} " +
               $"ADD COLUMN {SqlIdentifier.Quote(column.Name)} " +
               $"{GetPostgresDataType(column)};";
    }

    private static string GetPostgresDataType(ColumnDefinition column)
    {
        return column.DataType switch
        {
            ColumnDataType.Text when column.Length is not null =>
                $"varchar({column.Length})",
            ColumnDataType.Text => "text",
            ColumnDataType.Date => "date",
            ColumnDataType.Decimal when column.Precision is not null &&
                                        column.Scale is not null =>
                $"numeric({column.Precision}, {column.Scale})",
            ColumnDataType.Decimal when column.Precision is not null =>
                $"numeric({column.Precision})",
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
    }
}
