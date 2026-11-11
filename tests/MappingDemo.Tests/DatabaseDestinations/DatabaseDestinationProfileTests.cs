using MappingDemo.Shared.DatabaseDestinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MappingDemo.Tests.DatabaseDestinations;

public sealed class DatabaseDestinationProfileTests
{
    [Fact]
    public void Valid_profiles_resolve_typed_connections_by_logical_id()
    {
        var configuration = Configuration(
            ("ConnectionStrings:TargetPostgres",
                "Host=localhost;Database=target;Username=test;Password=secret"),
            ("ConnectionStrings:TargetSqlServer",
                "Server=localhost;Database=target;User Id=test;Password=secret;TrustServerCertificate=true"),
            ("DestinationDatabases:Profiles:0:Id", "customer-postgres"),
            ("DestinationDatabases:Profiles:0:Provider", "PostgreSql"),
            ("DestinationDatabases:Profiles:0:ConnectionStringName", "TargetPostgres"),
            ("DestinationDatabases:Profiles:0:AllowedSchemas:0", "public"),
            ("DestinationDatabases:Profiles:0:CommandTimeoutSeconds", "15"),
            ("DestinationDatabases:Profiles:0:SchemaCacheSeconds", "60"),
            ("DestinationDatabases:Profiles:0:WriteEnabled", "false"),
            ("DestinationDatabases:Profiles:1:Id", "legacy-sqlserver"),
            ("DestinationDatabases:Profiles:1:Provider", "SqlServer"),
            ("DestinationDatabases:Profiles:1:ConnectionStringName", "TargetSqlServer"),
            ("DestinationDatabases:Profiles:1:AllowedSchemas:0", "dbo"),
            ("DestinationDatabases:Profiles:1:CommandTimeoutSeconds", "20"),
            ("DestinationDatabases:Profiles:1:SchemaCacheSeconds", "120"),
            ("DestinationDatabases:Profiles:1:WriteEnabled", "true"));
        var services = new ServiceCollection();

        services.AddDatabaseDestinations(configuration);
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<
            IDatabaseDestinationProfileCatalog>();
        var factory = provider.GetRequiredService<
            IDatabaseDestinationConnectionFactory>();

        var postgresProfile = catalog.GetRequired("customer-postgres");
        var sqlServerProfile = catalog.GetRequired("legacy-sqlserver");
        using var postgres = factory.CreateConnection("customer-postgres");
        using var sqlServer = factory.CreateConnection("legacy-sqlserver");

        Assert.Equal(DatabaseProvider.PostgreSql, postgresProfile.Provider);
        Assert.False(postgresProfile.WriteEnabled);
        Assert.Equal(["public"], postgresProfile.AllowedSchemas);
        Assert.StartsWith("sha256:", postgresProfile.ConfigurationHash);
        Assert.IsType<NpgsqlConnection>(postgres);
        Assert.Equal(DatabaseProvider.SqlServer, sqlServerProfile.Provider);
        Assert.True(sqlServerProfile.WriteEnabled);
        Assert.Equal("Microsoft.Data.SqlClient.SqlConnection",
            sqlServer.GetType().FullName);
        Assert.DoesNotContain("Password=secret", postgresProfile.ToString());
        Assert.DoesNotContain("Password=secret", sqlServerProfile.ToString());
    }

    [Fact]
    public void Empty_profile_list_keeps_feature_disabled_without_breaking_startup()
    {
        var services = new ServiceCollection();

        services.AddDatabaseDestinations(Configuration());
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<
            IDatabaseDestinationProfileCatalog>();

        Assert.Empty(catalog.GetAll());
    }

    [Theory]
    [InlineData("DestinationDatabases:Profiles:0:Id", "UPPERCASE")]
    [InlineData("DestinationDatabases:Profiles:0:Provider", "Oracle")]
    [InlineData("DestinationDatabases:Profiles:0:ConnectionStringName", "")]
    [InlineData("DestinationDatabases:Profiles:0:AllowedSchemas:0", "")]
    [InlineData("DestinationDatabases:Profiles:0:CommandTimeoutSeconds", "0")]
    [InlineData("DestinationDatabases:Profiles:0:SchemaCacheSeconds", "-1")]
    public void Invalid_profile_fails_closed(string key, string value)
    {
        var values = ValidPostgresValues();
        values[key] = value;

        Assert.Throws<DatabaseDestinationProfileValidationException>(() =>
            new ServiceCollection().AddDatabaseDestinations(
                Configuration(values)));
    }

    [Fact]
    public void Duplicate_profile_id_fails_closed()
    {
        var values = ValidPostgresValues();
        values["DestinationDatabases:Profiles:1:Id"] = "customer-postgres";
        values["DestinationDatabases:Profiles:1:Provider"] = "SqlServer";
        values["DestinationDatabases:Profiles:1:ConnectionStringName"] =
            "TargetSqlServer";
        values["DestinationDatabases:Profiles:1:AllowedSchemas:0"] = "dbo";
        values["ConnectionStrings:TargetSqlServer"] =
            "Server=localhost;Database=target;Integrated Security=true;TrustServerCertificate=true";

        Assert.Throws<DatabaseDestinationProfileValidationException>(() =>
            new ServiceCollection().AddDatabaseDestinations(
                Configuration(values)));
    }

    [Fact]
    public void Missing_named_connection_string_fails_without_exposing_other_secret()
    {
        var values = ValidPostgresValues();
        values.Remove("ConnectionStrings:TargetPostgres");
        values["ConnectionStrings:Mapping"] =
            "Host=localhost;Password=do-not-leak";

        var exception = Assert.Throws<
            DatabaseDestinationProfileValidationException>(() =>
                new ServiceCollection().AddDatabaseDestinations(
                    Configuration(values)));

        Assert.Contains("TargetPostgres", exception.Message);
        Assert.DoesNotContain("do-not-leak", exception.Message);
    }

    [Fact]
    public void Invalid_connection_string_fails_without_exposing_its_value()
    {
        var values = ValidPostgresValues();
        values["ConnectionStrings:TargetPostgres"] =
            "Host=localhost;Password=do-not-leak;UnknownKeyword=value";

        var exception = Assert.Throws<
            DatabaseDestinationProfileValidationException>(() =>
                new ServiceCollection().AddDatabaseDestinations(
                    Configuration(values)));

        Assert.Contains("TargetPostgres", exception.Message);
        Assert.DoesNotContain("do-not-leak", exception.ToString());
        Assert.DoesNotContain("UnknownKeyword=value", exception.ToString());
    }

    [Fact]
    public void Unknown_profile_id_is_rejected_before_connection_creation()
    {
        var services = new ServiceCollection();
        services.AddDatabaseDestinations(
            Configuration(ValidPostgresValues()));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<
            IDatabaseDestinationConnectionFactory>();

        Assert.Throws<KeyNotFoundException>(() =>
            factory.CreateConnection("unknown-profile"));
    }

    private static Dictionary<string, string?> ValidPostgresValues() => new()
    {
        ["ConnectionStrings:TargetPostgres"] =
            "Host=localhost;Database=target;Username=test;Password=secret",
        ["DestinationDatabases:Profiles:0:Id"] = "customer-postgres",
        ["DestinationDatabases:Profiles:0:Provider"] = "PostgreSql",
        ["DestinationDatabases:Profiles:0:ConnectionStringName"] =
            "TargetPostgres",
        ["DestinationDatabases:Profiles:0:AllowedSchemas:0"] = "public",
        ["DestinationDatabases:Profiles:0:CommandTimeoutSeconds"] = "15",
        ["DestinationDatabases:Profiles:0:SchemaCacheSeconds"] = "60",
        ["DestinationDatabases:Profiles:0:WriteEnabled"] = "false"
    };

    private static IConfiguration Configuration(
        params (string Key, string? Value)[] values) =>
        Configuration(values.ToDictionary(item => item.Key, item => item.Value));

    private static IConfiguration Configuration(
        IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
