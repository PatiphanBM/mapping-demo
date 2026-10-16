using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api;

public static class ApiJsonOptions
{
    public static void Configure(JsonOptions options)
    {
        options.JsonSerializerOptions.Converters.Add(
            new UnattributedStringEnumConverter());
    }

    private sealed class UnattributedStringEnumConverter : JsonConverterFactory
    {
        private static readonly JsonStringEnumConverter StringEnums = new();

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum &&
                   typeToConvert.GetCustomAttribute<JsonConverterAttribute>()
                       is null;
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return StringEnums.CreateConverter(typeToConvert, options);
        }
    }
}
