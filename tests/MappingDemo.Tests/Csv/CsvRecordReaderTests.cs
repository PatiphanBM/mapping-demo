using MappingDemo.Shared.Csv;

namespace MappingDemo.Tests.Csv;

public sealed class CsvRecordReaderTests
{
    [Fact]
    public async Task ReadAsync_returns_logical_records_with_one_based_numbers()
    {
        const string contents =
            "Name,Note\r\n" +
            "First,\"line one\r\nline two\"\r\n" +
            "Second,value\r\n" +
            "\r\n";
        using var textReader = new StringReader(contents);
        var reader = new CsvRecordReader();
        var records = new List<CsvRecord>();

        await foreach (var record in reader.ReadAsync(textReader))
        {
            records.Add(record);
        }

        Assert.Collection(
            records,
            first =>
            {
                Assert.Equal(1, first.RowNumber);
                Assert.Equal("First", first.Values["Name"]);
                Assert.Equal("line one\r\nline two", first.Values["Note"]);
            },
            second =>
            {
                Assert.Equal(2, second.RowNumber);
                Assert.Equal("Second", second.Values["Name"]);
                Assert.Equal("value", second.Values["Note"]);
            });
    }
}
