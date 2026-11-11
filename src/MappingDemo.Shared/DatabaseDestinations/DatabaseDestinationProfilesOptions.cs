using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace MappingDemo.Shared.DatabaseDestinations;

public sealed class DatabaseDestinationProfilesOptions
{
    public const string SectionName = "DestinationDatabases";

    public List<DatabaseDestinationProfileOptions> Profiles { get; init; } = [];

    public static DatabaseDestinationProfilesOptions Bind(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var profiles = configuration
            .GetSection(SectionName)
            .GetSection("Profiles")
            .GetChildren()
            .Select(DatabaseDestinationProfileOptions.Bind)
            .ToList();
        return new DatabaseDestinationProfilesOptions { Profiles = profiles };
    }

    public DatabaseDestinationProfileCatalog BuildCatalog() =>
        new(Profiles.Select(profile => profile.ToDomain()));
}

public sealed partial class DatabaseDestinationProfileOptions
{
    private const int MaximumCommandTimeoutSeconds = 300;
    private const int MaximumSchemaCacheSeconds = 3600;

    public string Id { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string ConnectionStringName { get; init; } = string.Empty;

    public List<string> AllowedSchemas { get; init; } = [];

    public int CommandTimeoutSeconds { get; init; } = 15;

    public int SchemaCacheSeconds { get; init; } = 60;

    public bool WriteEnabled { get; init; }

    internal static DatabaseDestinationProfileOptions Bind(
        IConfigurationSection section)
    {
        return new DatabaseDestinationProfileOptions
        {
            Id = section["Id"] ?? string.Empty,
            Provider = section["Provider"] ?? string.Empty,
            ConnectionStringName =
                section["ConnectionStringName"] ?? string.Empty,
            AllowedSchemas = section.GetSection("AllowedSchemas")
                .GetChildren()
                .Select(item => item.Value ?? string.Empty)
                .ToList(),
            CommandTimeoutSeconds = ParseInteger(
                section,
                "CommandTimeoutSeconds",
                defaultValue: 15),
            SchemaCacheSeconds = ParseInteger(
                section,
                "SchemaCacheSeconds",
                defaultValue: 60),
            WriteEnabled = ParseBoolean(
                section,
                "WriteEnabled",
                defaultValue: false)
        };
    }

    public DatabaseDestinationProfile ToDomain()
    {
        if (!ProfileIdPattern().IsMatch(Id))
        {
            throw Invalid("Id must match ^[a-z][a-z0-9-]{0,62}$.");
        }
        if (!Enum.TryParse<DatabaseProvider>(
                Provider,
                ignoreCase: false,
                out var provider)
            || !Enum.IsDefined(provider))
        {
            throw Invalid("Provider must be PostgreSql or SqlServer.");
        }
        if (!ConnectionStringNamePattern().IsMatch(ConnectionStringName))
        {
            throw Invalid("ConnectionStringName is invalid.");
        }
        if (AllowedSchemas.Count == 0
            || AllowedSchemas.Any(string.IsNullOrWhiteSpace))
        {
            throw Invalid("AllowedSchemas must contain at least one name.");
        }
        if (AllowedSchemas.Distinct(StringComparer.Ordinal).Count()
            != AllowedSchemas.Count)
        {
            throw Invalid("AllowedSchemas contains a duplicate name.");
        }
        if (CommandTimeoutSeconds is < 1 or > MaximumCommandTimeoutSeconds)
        {
            throw Invalid(
                $"CommandTimeoutSeconds must be between 1 and {MaximumCommandTimeoutSeconds}.");
        }
        if (SchemaCacheSeconds is < 0 or > MaximumSchemaCacheSeconds)
        {
            throw Invalid(
                $"SchemaCacheSeconds must be between 0 and {MaximumSchemaCacheSeconds}.");
        }

        var schemas = Array.AsReadOnly(AllowedSchemas.ToArray());
        return new DatabaseDestinationProfile(
            Id,
            provider,
            ConnectionStringName,
            schemas,
            CommandTimeoutSeconds,
            SchemaCacheSeconds,
            WriteEnabled,
            DatabaseDestinationProfile.ComputeConfigurationHash(
                Id,
                provider,
                ConnectionStringName,
                schemas,
                CommandTimeoutSeconds,
                SchemaCacheSeconds,
                WriteEnabled));
    }

    private DatabaseDestinationProfileValidationException Invalid(
        string detail) =>
        new($"Database destination profile '{SafeId()}' is invalid. {detail}");

    private string SafeId() => string.IsNullOrWhiteSpace(Id) ? "<empty>" : Id;

    private static int ParseInteger(
        IConfigurationSection section,
        string key,
        int defaultValue)
    {
        var value = section[key];
        if (value is null)
        {
            return defaultValue;
        }
        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result))
        {
            return result;
        }

        throw new DatabaseDestinationProfileValidationException(
            $"Database destination profile option '{key}' must be an integer.");
    }

    private static bool ParseBoolean(
        IConfigurationSection section,
        string key,
        bool defaultValue)
    {
        var value = section[key];
        if (value is null)
        {
            return defaultValue;
        }
        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        throw new DatabaseDestinationProfileValidationException(
            $"Database destination profile option '{key}' must be true or false.");
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();

    [GeneratedRegex(
        "^[A-Za-z][A-Za-z0-9_.-]{0,127}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringNamePattern();
}
