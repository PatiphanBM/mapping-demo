using Dapper;
using MappingDemo.Shared.Csv;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Worker;

public static class SourceRowWriter
{
    public static string BuildInsertSql(
        string sourceTableName,
        IReadOnlyList<FileToSourceRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentNullException.ThrowIfNull(rules);

        var columns = new List<string>
        {
            "file_job_id",
            "row_number"
        };
        columns.AddRange(
            rules.Select(rule => SqlIdentifier.Quote(rule.SourceColumn)));

        var values = new List<string>
        {
            "@fileJobId",
            "@rowNumber"
        };
        values.AddRange(
            rules.Select((_, index) => $"@p{index}"));
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
        IReadOnlyList<FileToSourceRule> rules,
        long fileJobId,
        CsvRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(record);

        var parameters = new DynamicParameters();
        parameters.Add("fileJobId", fileJobId);
        parameters.Add("rowNumber", record.RowNumber);

        for (var index = 0; index < rules.Count; index++)
        {
            parameters.Add(
                $"p{index}",
                record.Values[rules[index].CsvHeader]);
        }
        //Insert source_orders
        var command = new CommandDefinition(
            BuildInsertSql(sourceTableName, rules),
            parameters,
            transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long?>(command);
    }
}
