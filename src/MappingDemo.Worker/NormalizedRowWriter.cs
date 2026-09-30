using Dapper;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Worker;

public static class NormalizedRowWriter
{
    public static string BuildUpsertSql(
        string normalizedTableName,
        IReadOnlyList<string> columnNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTableName);
        ArgumentNullException.ThrowIfNull(columnNames);

        var quotedColumns = columnNames
            .Select(SqlIdentifier.Quote)
            .ToArray();
        var insertColumns = new List<string>
        {
            "source_row_id",
            "file_job_id",
            "row_number",
            "row_job_id",
            "config_version_id"
        };
        insertColumns.AddRange(quotedColumns);

        var valueParameters = new List<string>
        {
            "@sourceRowId",
            "@fileJobId",
            "@rowNumber",
            "@rowJobId",
            "@configVersionId"
        };
        valueParameters.AddRange(
            columnNames.Select((_, index) => $"@p{index}"));

        var updateAssignments = quotedColumns
            .Select(column => $"{column} = EXCLUDED.{column}")
            .ToList();
        updateAssignments.Add("row_job_id = EXCLUDED.row_job_id");
        updateAssignments.Add(
            "config_version_id = EXCLUDED.config_version_id");
        updateAssignments.Add("normalized_at = now()");

        return $"""
            INSERT INTO {SqlIdentifier.Quote(normalizedTableName)} ({string.Join(", ", insertColumns)})
            VALUES ({string.Join(", ", valueParameters)})
            ON CONFLICT (source_row_id) DO UPDATE SET {string.Join(", ", updateAssignments)};
            """;
    }

    public static async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedTableName,
        long sourceRowId,
        long fileJobId,
        int rowNumber,
        long rowJobId,
        long configVersionId,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(values);

        var columnNames = values.Keys.ToArray();
        var parameters = new DynamicParameters();
        parameters.Add("sourceRowId", sourceRowId);
        parameters.Add("fileJobId", fileJobId);
        parameters.Add("rowNumber", rowNumber);
        parameters.Add("rowJobId", rowJobId);
        parameters.Add("configVersionId", configVersionId);

        for (var index = 0; index < columnNames.Length; index++)
        {
            parameters.Add($"p{index}", values[columnNames[index]]);
        }

        var command = new CommandDefinition(
            BuildUpsertSql(normalizedTableName, columnNames),
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
