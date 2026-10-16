using Dapper;
using MappingDemo.Shared.Jobs;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Npgsql;

namespace MappingDemo.Worker;

public static class NormalizedRowWriter
{
    private const string CompleteRowJobSql = """
        UPDATE row_jobs
        SET
            status = @Status,
            attempts = @Attempts,
            last_error = NULL,
            finished_at = now()
        WHERE id = @RowJobId;
        """;

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
            "output_key",
            "file_job_id",
            "row_number",
            "row_job_id",
            "config_version_id"
        };
        insertColumns.AddRange(quotedColumns);

        var valueParameters = new List<string>
        {
            "@sourceRowId",
            "@outputKey",
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
        //Insert normalized_orders
        return $"""
            INSERT INTO {SqlIdentifier.Quote(normalizedTableName)} ({string.Join(", ", insertColumns)})
            VALUES ({string.Join(", ", valueParameters)})
            ON CONFLICT (source_row_id, output_key) DO UPDATE SET {string.Join(", ", updateAssignments)};
            """;
    }

    public static async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedTableName,
        long sourceRowId,
        string outputKey,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(outputKey);

        var columnNames = values.Keys.ToArray();
        var parameters = new DynamicParameters();
        parameters.Add("sourceRowId", sourceRowId);
        parameters.Add("outputKey", outputKey);
        parameters.Add("fileJobId", fileJobId);
        parameters.Add("rowNumber", rowNumber);
        parameters.Add("rowJobId", rowJobId);
        parameters.Add("configVersionId", configVersionId);

        for (var index = 0; index < columnNames.Length; index++)
        {
            parameters.Add($"p{index}", values[columnNames[index]]);
        }
        //Insert normalized_orders
        var command = new CommandDefinition(
            BuildUpsertSql(normalizedTableName, columnNames),
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public static string BuildDeleteStaleSql(string normalizedTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTableName);

        return $"""
            DELETE FROM {SqlIdentifier.Quote(normalizedTableName)}
            WHERE source_row_id = @sourceRowId
              AND NOT (output_key = ANY(@outputKeys));
            """;
    }

    public static async Task<string> ReconcileAndCompleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedTableName,
        long sourceRowId,
        long fileJobId,
        int rowNumber,
        long rowJobId,
        long configVersionId,
        IReadOnlyList<string> normalizedColumnNames,
        IReadOnlyList<TransformationOutputResult> outputs,
        int attempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(normalizedColumnNames);
        ArgumentNullException.ThrowIfNull(outputs);

        foreach (var output in outputs)
        {
            var values = normalizedColumnNames.ToDictionary(
                columnName => columnName,
                columnName => output.Values.TryGetValue(
                    columnName,
                    out var value)
                        ? value
                        : null,
                StringComparer.Ordinal);
            await UpsertAsync(
                connection,
                transaction,
                normalizedTableName,
                sourceRowId,
                output.OutputKey,
                fileJobId,
                rowNumber,
                rowJobId,
                configVersionId,
                values,
                cancellationToken);
        }

        var deleteCommand = new CommandDefinition(
            BuildDeleteStaleSql(normalizedTableName),
            new
            {
                SourceRowId = sourceRowId,
                OutputKeys = outputs
                    .Select(output => output.OutputKey)
                    .ToArray()
            },
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(deleteCommand);

        var status = outputs.Count == 0
            ? RowJobStatus.Filtered
            : RowJobStatus.Done;
        var completeCommand = new CommandDefinition(
            CompleteRowJobSql,
            new
            {
                RowJobId = rowJobId,
                Status = status,
                Attempts = attempts
            },
            transaction,
            cancellationToken: cancellationToken);
        var updatedRows = await connection.ExecuteAsync(completeCommand);
        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Row job {rowJobId} was not found while recording successful reconciliation.");
        }

        return status;
    }
}
