using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MappingDemo.Shared.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddMappingDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Mapping")
            ?? throw new InvalidOperationException(
                "Connection string 'Mapping' is not configured.");

        var dataSource = NpgsqlDataSource.Create(connectionString);
        services.AddSingleton(dataSource);
        services.AddSingleton<MigrationRunner>();

        return services;
    }
}
