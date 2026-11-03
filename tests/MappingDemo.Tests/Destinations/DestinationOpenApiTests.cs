using MappingDemo.Api;
using MappingDemo.Shared.Destinations;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MappingDemo.Tests.Destinations;

public sealed class DestinationOpenApiTests
{
    [Fact]
    public void Destination_schema_exposes_all_three_derived_types()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSwaggerGen(OpenApiConfiguration.Configure);
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();

        var schema = generator.GenerateSchema(
            typeof(DestinationDefinition),
            repository);

        Assert.NotNull(schema.OneOf);
        Assert.Equal(3, schema.OneOf!.Count);
        Assert.Contains("LegacyTableDestination", repository.Schemas.Keys);
        Assert.Contains("ApiContractDestination", repository.Schemas.Keys);
        Assert.Contains("JsonFileDestination", repository.Schemas.Keys);
    }
}
