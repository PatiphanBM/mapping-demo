using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MappingDemo.Shared.DatabaseDestinations;

public static class DatabaseDestinationServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseDestinations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = DatabaseDestinationProfilesOptions.Bind(configuration);
        var catalog = options.BuildCatalog();
        var factory = new DatabaseDestinationConnectionFactory(
            catalog,
            configuration);

        services.AddSingleton(catalog);
        services.AddSingleton<IDatabaseDestinationProfileCatalog>(catalog);
        services.AddSingleton<IDatabaseDestinationConnectionFactory>(factory);
        services.AddSingleton<IDatabaseTargetSchemaInspector,
            DatabaseTargetSchemaInspector>();
        return services;
    }
}
