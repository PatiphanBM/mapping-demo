using System.Buffers;
using System.Text.Json;

namespace MappingDemo.Shared.Serialization;

public static class PolymorphicJson
{
    public static T? Deserialize<T>(
        string json,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(options);

        using var document = JsonDocument.Parse(json);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteElement(document.RootElement, writer);
        }

        return JsonSerializer.Deserialize<T>(buffer.WrittenSpan, options);
    }

    private static void WriteElement(
        JsonElement element,
        Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, writer);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void WriteObject(
        JsonElement element,
        Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        if (element.TryGetProperty("kind", out var discriminator))
        {
            writer.WritePropertyName("kind");
            WriteElement(discriminator, writer);
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("kind"))
            {
                continue;
            }

            writer.WritePropertyName(property.Name);
            WriteElement(property.Value, writer);
        }

        writer.WriteEndObject();
    }
}
