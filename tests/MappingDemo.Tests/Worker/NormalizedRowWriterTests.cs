using MappingDemo.Worker;

namespace MappingDemo.Tests.Worker;

public sealed class NormalizedRowWriterTests
{
    [Fact]
    public void BuildUpsertSql_quotes_identifiers_and_updates_current_result()
    {
        var sql = NormalizedRowWriter.BuildUpsertSql(
            "norm_orders",
            ["order_no", "amount"]);

        var expected = """
            INSERT INTO "norm_orders" (source_row_id, output_key, file_job_id, row_number, row_job_id, config_version_id, "order_no", "amount")
            VALUES (@sourceRowId, @outputKey, @fileJobId, @rowNumber, @rowJobId, @configVersionId, @p0, @p1)
            ON CONFLICT (source_row_id, output_key) DO UPDATE SET "order_no" = EXCLUDED."order_no", "amount" = EXCLUDED."amount", row_job_id = EXCLUDED.row_job_id, config_version_id = EXCLUDED.config_version_id, normalized_at = now();
            """;

        Assert.Equal(expected, sql);
    }

    [Fact]
    public void BuildDeleteStaleSql_deletes_outputs_outside_the_desired_set()
    {
        var sql = NormalizedRowWriter.BuildDeleteStaleSql("norm_orders");

        var expected = """
            DELETE FROM "norm_orders"
            WHERE source_row_id = @sourceRowId
              AND NOT (output_key = ANY(@outputKeys));
            """;

        Assert.Equal(expected, sql);
    }
}
