using Dapper;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Worker;

public static class SourceRowWriter
{
    public static string BuildInsertSql(
        string sourceTableName,
        IReadOnlyList<string> sourceColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentNullException.ThrowIfNull(sourceColumns);

        var columns = new List<string>
        {
            "file_job_id",
            "row_number",
            "physical_line_number"
        };
        columns.AddRange(
            sourceColumns.Select(SqlIdentifier.Quote));

        var values = new List<string>
        {
            "@fileJobId",
            "@rowNumber",
            "@physicalLineNumber"
        };
        values.AddRange(
            sourceColumns.Select((_, index) => $"@p{index}"));
        //Insert source_orders
        return $"""
            INSERT INTO {SqlIdentifier.Quote(sourceTableName)} ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            ON CONFLICT (file_job_id, row_number) DO NOTHING
            RETURNING id;
            """;
    }

    public static async Task<long?> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceTableName,
        long fileJobId,
        InputRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(record);
        if (record.RowNumber is null)
        {
            throw new ArgumentException(
                "Only imported input records can be written.",
                nameof(record));
        }

        var sourceColumns = record.Values.Keys.ToArray();

        var parameters = new DynamicParameters();
        parameters.Add("fileJobId", fileJobId);
        parameters.Add("rowNumber", record.RowNumber);
        parameters.Add("physicalLineNumber", record.PhysicalLineNumber);

        for (var index = 0; index < sourceColumns.Length; index++)
        {
            parameters.Add(
                $"p{index}",
                record.Values[sourceColumns[index]]);
        }
        //Insert source_orders
        var command = new CommandDefinition(
            BuildInsertSql(sourceTableName, sourceColumns),
            parameters,
            transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long?>(command);
    }
}
