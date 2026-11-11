using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MappingDemo.Shared.DatabaseDestinations;

public interface IDatabaseDestinationConnectionFactory
{
    /// <summary>
    /// Creates a closed, caller-owned connection. The caller opens and disposes
    /// it; provider connection pooling remains enabled by the connection string.
    /// </summary>
    DbConnection CreateConnection(string profileId);
}

public sealed class DatabaseDestinationConnectionFactory
    : IDatabaseDestinationConnectionFactory
{
    private readonly IDatabaseDestinationProfileCatalog _catalog;
    private readonly IReadOnlyDictionary<string, string> _connectionStrings;

    public DatabaseDestinationConnectionFactory(
        IDatabaseDestinationProfileCatalog catalog,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(configuration);
        _catalog = catalog;
        _connectionStrings = catalog.GetAll().ToDictionary(
            profile => profile.Id,
            profile => ValidateConnectionString(profile, configuration),
            StringComparer.Ordinal);
    }

    public DbConnection CreateConnection(string profileId)
    {
        var profile = _catalog.GetRequired(profileId);
        var connectionString = _connectionStrings[profile.Id];
        return profile.Provider switch
        {
            DatabaseProvider.PostgreSql =>
                new NpgsqlConnection(connectionString),
            DatabaseProvider.SqlServer =>
                new SqlConnection(connectionString),
            _ => throw new InvalidOperationException(
                $"Database provider '{profile.Provider}' is not supported.")
        };
    }

    private static string ValidateConnectionString(
        DatabaseDestinationProfile profile,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(
            profile.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DatabaseDestinationProfileValidationException(
                $"Connection string '{profile.ConnectionStringName}' for database destination profile '{profile.Id}' is not configured.");
        }

        try
        {
            switch (profile.Provider)
            {
                case DatabaseProvider.PostgreSql:
                    _ = new NpgsqlConnectionStringBuilder(connectionString);
                    break;
                case DatabaseProvider.SqlServer:
                    _ = new SqlConnectionStringBuilder(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Database provider '{profile.Provider}' is not supported.");
            }
        }
        catch (ArgumentException)
        {
            throw new DatabaseDestinationProfileValidationException(
                $"Connection string '{profile.ConnectionStringName}' for database destination profile '{profile.Id}' is invalid.");
        }

        return connectionString;
    }
}
