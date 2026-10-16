using System.Text.Json;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Tables;

public sealed class TableContractTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Column_request_round_trips_length_precision_and_scale()
    {
        var column = new ColumnRequest(
            "amount",
            ColumnDataType.Decimal,
            IsRequired: true,
            Length: null,
            Precision: 18,
            Scale: 2);

        var json = JsonSerializer.Serialize(column, JsonOptions);
        var copy = JsonSerializer.Deserialize<ColumnRequest>(json, JsonOptions);

        Assert.Equal(column, copy);
        Assert.Contains("\"precision\":18", json);
        Assert.Contains("\"scale\":2", json);
    }
}
