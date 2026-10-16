using MappingDemo.Worker;

namespace MappingDemo.Tests.Csv;

public sealed class SourceRowWriterTests
{
    [Fact]
    public void BuildInsertSql_QuotesIdentifiersAndUsesValueParameters()
    {
        string[] sourceColumns = ["order_no", "customer_name"];

        var sql = SourceRowWriter.BuildInsertSql(
            "src_orders",
            sourceColumns);

        var expected = """
            INSERT INTO "src_orders" (file_job_id, row_number, physical_line_number, "order_no", "customer_name")
            VALUES (@fileJobId, @rowNumber, @physicalLineNumber, @p0, @p1)
            ON CONFLICT (file_job_id, row_number) DO NOTHING
            RETURNING id;
            """;
        Assert.Equal(expected, sql);
    }
}
