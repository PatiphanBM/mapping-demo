using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Worker;

namespace MappingDemo.Tests.Csv;

public sealed class SourceRowWriterTests
{
    [Fact]
    public void BuildInsertSql_QuotesIdentifiersAndUsesValueParameters()
    {
        var rules = new[]
        {
            new FileToSourceRule("Order No", "order_no"),
            new FileToSourceRule("Customer", "customer_name")
        };

        var sql = SourceRowWriter.BuildInsertSql("src_orders", rules);

        var expected = """
            INSERT INTO "src_orders" (file_job_id, row_number, "order_no", "customer_name")
            VALUES (@fileJobId, @rowNumber, @p0, @p1)
            ON CONFLICT (file_job_id, row_number) DO NOTHING
            RETURNING id;
            """;
        Assert.Equal(expected, sql);
    }
}
