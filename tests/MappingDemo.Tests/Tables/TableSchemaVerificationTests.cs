using System.Text.Json;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Tables;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MappingDemo.Tests.Tables;

public sealed class TableSchemaVerificationTests
{
    [Theory]
    [InlineData("int", "integer")]
    [InlineData("int4", "integer")]
    [InlineData("int8", "bigint")]
    [InlineData("bool", "boolean")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("timestamp", "timestamp without time zone")]
    [InlineData("varchar(50)", "character varying(50)")]
    [InlineData("decimal(18, 2)", "numeric(18,2)")]
    public void PostgreSql_type_aliases_are_canonicalized(
        string value,
        string expected)
    {
        Assert.Equal(expected, PostgresTypeCanonicalizer.Canonicalize(value));
    }

    [Fact]
    public void Expected_schema_contains_system_business_and_constraints()
    {
        var definition = new TableDefinition(
            "norm_orders",
            TableKind.Normalized,
            [
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Decimal,
                    IsRequired: true,
                    Ordinal: 1,
                    Precision: 18,
                    Scale: 2)
            ]);

        var schema = TablePhysicalSchema.Expected(definition);

        Assert.Equal("id", schema.Columns[0].Name);
        Assert.True(schema.Columns[0].IsIdentity);
        Assert.Contains(schema.Columns, column =>
            column.Name == "amount" &&
            column.DataType == "numeric(18,2)" &&
            column.IsNullable);
        Assert.Equal(["id"], schema.PrimaryKey.Columns);
        Assert.Contains(schema.UniqueConstraints, constraint =>
            constraint.Columns.SequenceEqual(
                ["source_row_id", "output_key"]));
    }

    [Fact]
    public void Mutation_gate_and_permissions_fail_closed()
    {
        var disabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TableAdministration:AllowLocalOperatorHeader"] = "true",
                ["TableAdministration:MutationsEnabled"] = "false"
            })
            .Build();
        var observability = new TableAdministrationObservability();
        var authorizer = new TableAdministrationAuthorizer(
            disabled,
            observability);

        Assert.False(authorizer.IsAuthorized(
            new(),
            "TableAdmin.Create",
            "operator-a",
            mutation: true));
        Assert.True(authorizer.IsAuthorized(
            new(),
            "TableAdmin.Read",
            operatorId: null,
            mutation: false));
        Assert.Equal(1, observability.Snapshot().UnauthorizedMutationCount);
    }

    [Fact]
    public void Verification_rate_limit_is_scoped_by_table()
    {
        var limiter = new TableVerificationRateLimiter(
            TimeProvider.System,
            permitLimit: 2,
            TimeSpan.FromMinutes(1));

        Assert.True(limiter.TryAcquire(1));
        Assert.True(limiter.TryAcquire(1));
        Assert.False(limiter.TryAcquire(1));
        Assert.True(limiter.TryAcquire(2));
    }

    [Fact]
    public void Observability_exposes_safe_bounded_labels_and_alerts()
    {
        var expected = TablePhysicalSchema.Expected(new TableDefinition(
            "private_customer_table",
            TableKind.Source,
            [new("customer_secret", ColumnDataType.Text, false, 1)]));
        var observability = new TableAdministrationObservability();
        observability.RecordMutation("create", success: true);
        observability.RecordVerification(new(
            TableVerificationStatus.Drifted,
            expected,
            Actual: null,
            [new("columns.customer_secret", "ColumnMissing", "Missing")],
            DateTimeOffset.UtcNow,
            1));
        observability.RecordUnauthorizedMutation();

        var snapshot = observability.Snapshot();
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Contains(snapshot.Alerts, alert =>
            alert.Code == "TableSchemaDriftDetected");
        Assert.Contains(snapshot.Alerts, alert =>
            alert.Code == "UnauthorizedTableMutationAttempt");
        Assert.DoesNotContain("private_customer_table", json);
        Assert.DoesNotContain("customer_secret", json);
    }

    [Fact]
    public async Task Catalog_outage_returns_safe_verification_failure()
    {
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=unavailable;" +
            "Username=none;Password=none;Timeout=1");
        var inspector = new PostgreSqlTableSchemaInspector(dataSource);

        var result = await inspector.InspectAsync(
            1,
            new TableDefinition(
                "src_unavailable",
                TableKind.Source,
                [new("raw_value", ColumnDataType.Text, false, 1)]));

        Assert.Equal(
            TableVerificationStatus.VerificationFailed,
            result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CatalogUnavailable", diagnostic.Code);
        Assert.DoesNotContain("127.0.0.1", diagnostic.Message);
        Assert.DoesNotContain("Password", diagnostic.Message);
    }
}
