using System.Text.Json;
using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class MappingRuleSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void FileToSourceRule_round_trips_with_camel_case_properties()
    {
        var rule = new FileToSourceRule("Order No", "order_no");

        var json = JsonSerializer.Serialize(rule, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<FileToSourceRule>(
            json,
            JsonOptions);

        Assert.Equal(
            "{\"csvHeader\":\"Order No\",\"sourceColumn\":\"order_no\"}",
            json);
        Assert.Equal(rule, deserialized);
    }

    [Fact]
    public void SourceToNormalizedRule_round_trips_with_camel_case_properties()
    {
        var rule = new SourceToNormalizedRule(
            "order_date",
            "order_date",
            "yyyy-MM-dd");

        var json = JsonSerializer.Serialize(rule, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SourceToNormalizedRule>(
            json,
            JsonOptions);

        Assert.Equal(
            "{\"sourceColumn\":\"order_date\",\"normalizedColumn\":" +
            "\"order_date\",\"format\":\"yyyy-MM-dd\"}",
            json);
        Assert.Equal(rule, deserialized);
    }

    [Fact]
    public void SourceToNormalizedRule_defaults_format_to_null()
    {
        var rule = new SourceToNormalizedRule("note", "note");

        Assert.Null(rule.Format);
    }
}
