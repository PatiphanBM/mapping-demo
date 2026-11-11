using System.Text.Json;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.DatabaseDestinations;

public sealed class DatabaseTargetSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Snapshot_hash_is_stable_across_catalog_order()
    {
        var first = Schema(
            [Column("name", 2, nullable: true), Column("id", 1)],
            [new TargetUniqueConstraint("uq_customer_name", ["name"]),
                new TargetUniqueConstraint("pk_customer", ["id"])]);
        var second = Schema(
            [Column("id", 1), Column("name", 2, nullable: true)],
            [new TargetUniqueConstraint("pk_customer", ["id"]),
                new TargetUniqueConstraint("uq_customer_name", ["name"])]);

        Assert.Equal(first.SchemaHash, second.SchemaHash);
        Assert.StartsWith("sha256:", first.SchemaHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Compatible_addition_does_not_block_existing_mapping()
    {
        var configured = Schema([Column("id", 1)]).ToSnapshot();
        var current = Schema(
            [Column("id", 1), Column("note", 2, nullable: true)]);

        var result = TargetSchemaCompatibilityAnalyzer.Compare(
            configured,
            current,
            new HashSet<string>(["id"], StringComparer.Ordinal),
            []);

        Assert.Equal(SchemaCompatibilityStatus.CompatibleChange, result.Status);
        Assert.False(result.BlocksDelivery);
        Assert.Contains(result.Changes, change => change.Code == "ColumnAddedCompatible");
    }

    [Fact]
    public void Required_addition_blocks_delivery()
    {
        var configured = Schema([Column("id", 1)]).ToSnapshot();
        var current = Schema([Column("id", 1), Column("code", 2)]);

        var result = TargetSchemaCompatibilityAnalyzer.Compare(
            configured,
            current,
            new HashSet<string>(["id"], StringComparer.Ordinal),
            []);

        Assert.Equal(SchemaCompatibilityStatus.BreakingChange, result.Status);
        Assert.True(result.BlocksDelivery);
        Assert.Contains(result.Changes, change => change.Code == "RequiredColumnAdded");
    }

    [Fact]
    public void Mapped_column_and_selected_key_drift_block_delivery()
    {
        var configured = Schema(
            [Column("id", 1), Column("name", 2, nullable: true)],
            [new TargetUniqueConstraint("pk_customer", ["id"])]).ToSnapshot();
        var current = Schema(
            [Column("id", 1, providerType: "bigint")]);

        var result = TargetSchemaCompatibilityAnalyzer.Compare(
            configured,
            current,
            new HashSet<string>(["id", "name"], StringComparer.Ordinal),
            ["id"]);

        Assert.Equal(SchemaCompatibilityStatus.BreakingChange, result.Status);
        Assert.Contains(result.Changes, change => change.Code == "MappedColumnRemoved");
        Assert.Contains(result.Changes, change => change.Code == "MappedColumnChanged");
        Assert.Contains(result.Changes, change => change.Code == "SelectedUniqueKeyChanged");
    }

    [Fact]
    public void Database_table_destination_round_trips_and_validates()
    {
        var schema = Schema(
            [Column("id", 1), Column("note", 2, nullable: true)],
            [new TargetUniqueConstraint("pk_customer", ["id"])]);
        DestinationDefinition destination = new DatabaseTableDestination(
            "customer-db",
            "public",
            "customer",
            "insert",
            new ExistingUniqueKeyDuplicatePolicy(["id"]),
            schema.SchemaHash,
            schema.ToSnapshot());

        var json = JsonSerializer.Serialize(destination, JsonOptions);
        var restored = PolymorphicJson.Deserialize<DestinationDefinition>(
            json,
            JsonOptions);

        var database = Assert.IsType<DatabaseTableDestination>(restored);
        Assert.Equal(schema.SchemaHash, database.SchemaHash);
        Assert.Empty(DestinationDefinitionValidator.Validate(destination));
    }

    [Fact]
    public void Database_table_rejects_tampered_hash_and_generated_key()
    {
        var generated = Column("id", 1) with
        {
            IsIdentity = true,
            IsWritable = false
        };
        var schema = Schema(
            [generated],
            [new TargetUniqueConstraint("pk_customer", ["id"])]);
        DestinationDefinition destination = new DatabaseTableDestination(
            "customer-db",
            "public",
            "customer",
            "insert",
            new ExistingUniqueKeyDuplicatePolicy(["id"]),
            "sha256:tampered",
            schema.ToSnapshot());

        var errors = DestinationDefinitionValidator.Validate(destination);

        Assert.Contains(errors, error => error.Field == "destination.schemaHash");
        Assert.Contains(errors, error => error.Field == "destination.duplicatePolicy.columns");
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, "timestamptz", DatabaseScalarType.DateTime)]
    [InlineData(DatabaseProvider.PostgreSql, "uuid", DatabaseScalarType.Guid)]
    [InlineData(DatabaseProvider.SqlServer, "nvarchar", DatabaseScalarType.Text)]
    [InlineData(DatabaseProvider.SqlServer, "rowversion", DatabaseScalarType.Binary)]
    [InlineData(DatabaseProvider.SqlServer, "geography", DatabaseScalarType.Unsupported)]
    public void Provider_types_have_one_canonical_vocabulary(
        DatabaseProvider provider,
        string providerType,
        DatabaseScalarType expected)
    {
        Assert.Equal(
            expected,
            DatabaseTargetTypeCanonicalizer.Canonicalize(
                provider,
                providerType));
    }

    [Fact]
    public void Selected_unique_key_becomes_required_at_compiler_seam()
    {
        var schema = Schema([
            Column("id", 1, nullable: true),
            Column("note", 2, nullable: true)
        ]).ToSnapshot();

        var target = DatabaseTargetSchemaAdapter.Adapt(
            schema,
            new HashSet<string>(["id"], StringComparer.Ordinal));

        Assert.True(target.Root.Properties.Single(node => node.Name == "id").IsRequired);
        Assert.False(target.Root.Properties.Single(node => node.Name == "note").IsRequired);
        Assert.All(target.Root.Properties, node =>
            Assert.Equal(TargetNodeKind.Scalar, node.Kind));
    }

    private static TargetTableSchema Schema(
        IReadOnlyList<TargetColumnSchema> columns,
        IReadOnlyList<TargetUniqueConstraint>? keys = null) =>
        TargetTableSchema.Create(
            "customer-db",
            DatabaseProvider.PostgreSql,
            "public",
            "customer",
            columns,
            keys ?? []);

    private static TargetColumnSchema Column(
        string name,
        int ordinal,
        bool nullable = false,
        string providerType = "text") =>
        new(
            name,
            providerType,
            providerType == "bigint"
                ? DatabaseScalarType.Integer
                : DatabaseScalarType.Text,
            ordinal,
            nullable,
            HasDefault: false,
            IsIdentity: false,
            IsGenerated: false,
            IsComputed: false,
            IsWritable: true,
            Length: null,
            Precision: null,
            Scale: null);
}
