using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace MappingDemo.Shared.DatabaseDestinations;

[JsonConverter(typeof(JsonStringEnumConverter<DatabaseScalarType>))]
public enum DatabaseScalarType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    Date,
    Time,
    DateTime,
    Guid,
    Binary,
    Unsupported
}

public sealed record TargetColumnSchema(
    string Name,
    string ProviderType,
    DatabaseScalarType ScalarType,
    int Ordinal,
    bool IsNullable,
    bool HasDefault,
    bool IsIdentity,
    bool IsGenerated,
    bool IsComputed,
    bool IsWritable,
    int? Length,
    int? Precision,
    int? Scale)
{
    public bool IsRequired => IsWritable && !IsNullable && !HasDefault;
}

public sealed record TargetUniqueConstraint(
    string Name,
    IReadOnlyList<string> Columns);

public sealed record TargetTableReference(
    string ProfileId,
    string Schema,
    string Table);

public sealed record TargetTableSummary(
    string ProfileId,
    DatabaseProvider Provider,
    string Schema,
    string Table);

public sealed record TargetTableSchema(
    string ProfileId,
    DatabaseProvider Provider,
    string Schema,
    string Table,
    IReadOnlyList<TargetColumnSchema> Columns,
    IReadOnlyList<TargetUniqueConstraint> UniqueConstraints,
    string SchemaHash)
{
    public static TargetTableSchema Create(
        string profileId,
        DatabaseProvider provider,
        string schema,
        string table,
        IEnumerable<TargetColumnSchema> columns,
        IEnumerable<TargetUniqueConstraint> uniqueConstraints)
    {
        var orderedColumns = columns
            .OrderBy(column => column.Ordinal)
            .ThenBy(column => column.Name, StringComparer.Ordinal)
            .ToArray();
        var orderedConstraints = uniqueConstraints
            .OrderBy(constraint => constraint.Name, StringComparer.Ordinal)
            .Select(constraint => constraint with
            {
                Columns = constraint.Columns.ToArray()
            })
            .ToArray();
        var hash = TargetSchemaHash.Compute(
            provider,
            schema,
            table,
            orderedColumns,
            orderedConstraints);
        return new TargetTableSchema(
            profileId,
            provider,
            schema,
            table,
            orderedColumns,
            orderedConstraints,
            hash);
    }

    public TargetSchemaSnapshot ToSnapshot() =>
        new(
            ProfileId,
            Provider,
            Schema,
            Table,
            Columns.ToArray(),
            UniqueConstraints.ToArray(),
            SchemaHash);
}

public sealed record TargetSchemaSnapshot(
    string ProfileId,
    DatabaseProvider Provider,
    string Schema,
    string Table,
    IReadOnlyList<TargetColumnSchema> Columns,
    IReadOnlyList<TargetUniqueConstraint> UniqueConstraints,
    string SchemaHash);

[JsonConverter(typeof(JsonStringEnumConverter<SchemaCompatibilityStatus>))]
public enum SchemaCompatibilityStatus
{
    Healthy,
    CompatibleChange,
    BreakingChange,
    Unavailable
}

public sealed record TargetSchemaChange(
    string Code,
    string? Column,
    string Message,
    bool IsBreaking);

public sealed record SchemaCompatibilityResult(
    SchemaCompatibilityStatus Status,
    IReadOnlyList<TargetSchemaChange> Changes,
    string ConfiguredHash,
    string? CurrentHash)
{
    public bool BlocksDelivery =>
        Status is SchemaCompatibilityStatus.BreakingChange
            or SchemaCompatibilityStatus.Unavailable;
}

internal static class TargetSchemaHash
{
    public static string Compute(
        DatabaseProvider provider,
        string schema,
        string table,
        IReadOnlyList<TargetColumnSchema> columns,
        IReadOnlyList<TargetUniqueConstraint> constraints)
    {
        var canonical = new StringBuilder()
            .Append(provider).Append('\n')
            .Append(schema).Append('\n')
            .Append(table).Append('\n');
        foreach (var column in columns.OrderBy(value => value.Ordinal))
        {
            canonical.Append("column|")
                .Append(column.Ordinal).Append('|')
                .Append(column.Name).Append('|')
                .Append(column.ProviderType).Append('|')
                .Append(column.ScalarType).Append('|')
                .Append(column.IsNullable).Append('|')
                .Append(column.HasDefault).Append('|')
                .Append(column.IsIdentity).Append('|')
                .Append(column.IsGenerated).Append('|')
                .Append(column.IsComputed).Append('|')
                .Append(column.IsWritable).Append('|')
                .Append(column.Length).Append('|')
                .Append(column.Precision).Append('|')
                .Append(column.Scale).Append('\n');
        }

        foreach (var constraint in constraints.OrderBy(
                     value => value.Name,
                     StringComparer.Ordinal))
        {
            canonical.Append("unique|")
                .Append(constraint.Name).Append('|')
                .AppendJoin(',', constraint.Columns)
                .Append('\n');
        }

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
