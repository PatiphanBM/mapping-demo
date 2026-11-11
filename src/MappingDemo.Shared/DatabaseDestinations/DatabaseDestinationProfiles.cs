using System.Security.Cryptography;
using System.Text;

namespace MappingDemo.Shared.DatabaseDestinations;

public enum DatabaseProvider
{
    PostgreSql,
    SqlServer
}

public sealed record DatabaseDestinationProfile(
    string Id,
    DatabaseProvider Provider,
    string ConnectionStringName,
    IReadOnlyList<string> AllowedSchemas,
    int CommandTimeoutSeconds,
    int SchemaCacheSeconds,
    bool WriteEnabled,
    string ConfigurationHash)
{
    internal static string ComputeConfigurationHash(
        string id,
        DatabaseProvider provider,
        string connectionStringName,
        IReadOnlyList<string> allowedSchemas,
        int commandTimeoutSeconds,
        int schemaCacheSeconds,
        bool writeEnabled)
    {
        var canonical = string.Join(
            '\n',
            id,
            provider,
            connectionStringName,
            string.Join('\n', allowedSchemas.Order(StringComparer.Ordinal)),
            commandTimeoutSeconds,
            schemaCacheSeconds,
            writeEnabled);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

public interface IDatabaseDestinationProfileCatalog
{
    IReadOnlyList<DatabaseDestinationProfile> GetAll();

    DatabaseDestinationProfile GetRequired(string profileId);
}

public sealed class DatabaseDestinationProfileCatalog
    : IDatabaseDestinationProfileCatalog
{
    private readonly IReadOnlyDictionary<string, DatabaseDestinationProfile>
        _profiles;
    private readonly IReadOnlyList<DatabaseDestinationProfile> _ordered;

    public DatabaseDestinationProfileCatalog(
        IEnumerable<DatabaseDestinationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var materialized = profiles.ToArray();
        var duplicate = materialized
            .GroupBy(profile => profile.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new DatabaseDestinationProfileValidationException(
                $"Database destination profile ID '{duplicate.Key}' is duplicated.");
        }

        _ordered = Array.AsReadOnly(materialized);
        _profiles = materialized.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<DatabaseDestinationProfile> GetAll() => _ordered;

    public DatabaseDestinationProfile GetRequired(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        return _profiles.TryGetValue(profileId, out var profile)
            ? profile
            : throw new KeyNotFoundException(
                $"Database destination profile '{profileId}' was not found.");
    }
}

public sealed class DatabaseDestinationProfileValidationException
    : Exception
{
    public DatabaseDestinationProfileValidationException(string message)
        : base(message)
    {
    }

}
