using Swashbuckle.AspNetCore.SwaggerGen;

namespace MappingDemo.Api;

public static class OpenApiConfiguration
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.UseAllOfForInheritance();
        options.UseOneOfForPolymorphism();
    }
}
