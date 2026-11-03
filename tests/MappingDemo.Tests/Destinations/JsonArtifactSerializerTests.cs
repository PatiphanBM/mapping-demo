using System.Text;
using MappingDemo.Shared.Destinations;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonArtifactSerializerTests
{
    [Theory]
    [InlineData(
        "compact",
        "[{\"name\":\"หนึ่ง\",\"active\":true},{\"name\":\"สอง\",\"active\":false}]")]
    [InlineData(
        "indented",
        "[\n  {\n    \"name\": \"หนึ่ง\",\n    \"active\": true\n  },\n  {\n    \"name\": \"สอง\",\n    \"active\": false\n  }\n]")]
    public async Task Writes_runtime_document_bytes_deterministically(
        string formatting,
        string expected)
    {
        var result = await JsonArtifactSerializer.SerializeAsync(
            [
                "{\"name\":\"หนึ่ง\",\"active\":true}",
                "{\"name\":\"สอง\",\"active\":false}"
            ],
            formatting);

        Assert.Equal(expected, Encoding.UTF8.GetString(result.Bytes));
        Assert.Equal(2, result.Metadata.ObjectCount);
        Assert.Equal(result.Bytes.Length, result.Metadata.ByteCount);
    }
}
