using System.Data.Common;
using System.Text.Json;
using Dapper;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using MappingDemo.Worker.DatabaseDelivery;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MappingDemo.Tests.DatabaseDestinations;

public sealed class DatabaseWriteAdapterIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task PostgreSql_insert_is_parameterized_and_reconciles_unique_key()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_TARGET_INTEGRATION") ??
            Environment.GetEnvironmentVariable("MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual("true", Environment.GetEnvironmentVariable(
                "MAPPING_REQUIRE_POSTGRES_TARGET_INTEGRATION"));
            return;
        }

        var schemaName = $"write_{Guid.NewGuid():N}"[..30];
        var schema = SqlIdentifier.Quote(schemaName);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync($"""
                CREATE SCHEMA {schema};
                CREATE TABLE {schema}.customer (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    code text NOT NULL UNIQUE,
                    note text NULL,
                    amount numeric(18,2) NOT NULL,
                    enabled boolean NOT NULL,
                    external_id uuid NOT NULL,
                    happened_at timestamptz NOT NULL);
                """);
            await AssertWriteAndReconcileAsync(
                DatabaseProvider.PostgreSql,
                "postgres-write",
                schemaName,
                connectionString,
                connection);
        }
        finally
        {
            await connection.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {schema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServer_insert_is_parameterized_and_reconciles_unique_key()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_SQLSERVER_TARGET_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual("true", Environment.GetEnvironmentVariable(
                "MAPPING_REQUIRE_SQLSERVER_TARGET_INTEGRATION"));
            return;
        }

        var schemaName = $"write_{Guid.NewGuid():N}"[..30];
        var schema = QuoteSqlServer(schemaName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync($"CREATE SCHEMA {schema};");
            await connection.ExecuteAsync($"""
                CREATE TABLE {schema}.[customer] (
                    [id] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [code] nvarchar(100) NOT NULL UNIQUE,
                    [note] nvarchar(max) NULL,
                    [amount] decimal(18,2) NOT NULL,
                    [enabled] bit NOT NULL,
                    [external_id] uniqueidentifier NOT NULL,
                    [happened_at] datetimeoffset NOT NULL);
                """);
            await AssertWriteAndReconcileAsync(
                DatabaseProvider.SqlServer,
                "sqlserver-write",
                schemaName,
                connectionString,
                connection);
        }
        finally
        {
            await connection.ExecuteAsync($"""
                IF OBJECT_ID(N'{schemaName}.customer', N'U') IS NOT NULL
                    DROP TABLE {schema}.[customer];
                IF SCHEMA_ID(N'{schemaName}') IS NOT NULL
                    EXEC(N'DROP SCHEMA {schema}');
                """);
        }
    }

    private static async Task AssertWriteAndReconcileAsync(
        DatabaseProvider provider,
        string profileId,
        string schema,
        string connectionString,
        DbConnection verificationConnection)
    {
        const string connectionName = "WriteTarget";
        const string profileHash = "sha256:write-profile";
        var profile = new DatabaseDestinationProfile(
            profileId,
            provider,
            connectionName,
            [schema],
            30,
            0,
            WriteEnabled: true,
            profileHash);
        var profiles = new DatabaseDestinationProfileCatalog([profile]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{connectionName}"] = connectionString
            })
            .Build();
        var connections = new DatabaseDestinationConnectionFactory(
            profiles,
            configuration);
        var inspector = new DatabaseTargetSchemaInspector(profiles, connections);
        var target = await inspector.InspectAsync(
            new TargetTableReference(profileId, schema, "customer"),
            refresh: true);
        IDatabaseWriteAdapter adapter = provider == DatabaseProvider.PostgreSql
            ? new PostgreSqlDatabaseWriteAdapter(
                profiles,
                connections,
                inspector)
            : new SqlServerDatabaseWriteAdapter(
                profiles,
                connections,
                inspector);
        var policy = new ExistingUniqueKeyDuplicatePolicy(["code"]);

        var inserted = await adapter.WriteAsync(new DatabaseWriteRequest(
            profileId,
            profileHash,
            target.ToSnapshot(),
            policy,
            Values("C'001; DROP TABLE customer;--", "ภาษาไทย 😀")));
        Assert.Equal(DatabaseDeliveryJobStatus.Delivered, inserted.Status);

        var same = await adapter.WriteAsync(new DatabaseWriteRequest(
            profileId,
            profileHash,
            target.ToSnapshot(),
            policy,
            Values("C'001; DROP TABLE customer;--", "ภาษาไทย 😀")));
        Assert.Equal(DatabaseDeliveryJobStatus.AlreadyDelivered, same.Status);

        var conflict = await adapter.WriteAsync(new DatabaseWriteRequest(
            profileId,
            profileHash,
            target.ToSnapshot(),
            policy,
            Values("C'001; DROP TABLE customer;--", "different")));
        Assert.Equal(DatabaseDeliveryJobStatus.DeliveryConflict, conflict.Status);
        Assert.Equal(
            1,
            await verificationConnection.ExecuteScalarAsync<int>($"""
                SELECT COUNT(*) FROM {Quote(provider, schema)}.{Quote(provider, "customer")};
                """));
    }

    private static IReadOnlyDictionary<string, JsonElement> Values(
        string code,
        string note) => new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["code"] = JsonSerializer.SerializeToElement(code),
            ["note"] = JsonSerializer.SerializeToElement(note),
            ["amount"] = JsonSerializer.SerializeToElement(1234.50m),
            ["enabled"] = JsonSerializer.SerializeToElement(true),
            ["external_id"] = JsonSerializer.SerializeToElement(
                "95ae3ea7-66de-4638-bd1d-a2873dc0fb73"),
            ["happened_at"] = JsonSerializer.SerializeToElement(
                "2026-11-09T10:15:30+07:00")
        };

    private static string Quote(DatabaseProvider provider, string identifier) =>
        provider == DatabaseProvider.PostgreSql
            ? SqlIdentifier.Quote(identifier)
            : QuoteSqlServer(identifier);

    private static string QuoteSqlServer(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
