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
            "    imported_at timestamptz NOT NULL DEFAULT now(),"
        };

        foreach (var column in definition.Columns.OrderBy(column => column.Ordinal))
        {
            lines.Add($"    {SqlIdentifier.Quote(column.Name)} text,");
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
            "    source_row_id bigint NOT NULL UNIQUE,",
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
            var suffix = index < orderedColumns.Length - 1 ? "," : string.Empty;
            lines.Add(
                $"    {SqlIdentifier.Quote(column.Name)} " +
                $"{GetPostgresDataType(column.DataType)}{suffix}");
        }

        lines.Add(");");

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetPostgresDataType(ColumnDataType dataType)
    {
        return dataType switch
        {
            ColumnDataType.Text => "text",
            ColumnDataType.Date => "date",
            ColumnDataType.Decimal => "numeric",
            ColumnDataType.Boolean => "boolean",
            _ => throw new ArgumentOutOfRangeException(
                nameof(dataType),
                dataType,
                "Unsupported column data type.")
        };
    }
}
