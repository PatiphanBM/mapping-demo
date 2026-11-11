namespace MappingDemo.Shared.DatabaseDestinations;

public static class DatabaseTargetTypeCanonicalizer
{
    public static DatabaseScalarType Canonicalize(
        DatabaseProvider provider,
        string providerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);
        var type = providerType.Trim().ToLowerInvariant();
        return provider switch
        {
            DatabaseProvider.PostgreSql => type switch
            {
                "text" or "character varying" or "varchar" or
                    "character" or "char" or "name" =>
                    DatabaseScalarType.Text,
                "smallint" or "integer" or "bigint" or "int2" or
                    "int4" or "int8" => DatabaseScalarType.Integer,
                "numeric" or "decimal" or "real" or "double precision" or
                    "float4" or "float8" or "money" =>
                    DatabaseScalarType.Decimal,
                "boolean" or "bool" => DatabaseScalarType.Boolean,
                "date" => DatabaseScalarType.Date,
                "time" or "time without time zone" or
                    "time with time zone" or "timetz" =>
                    DatabaseScalarType.Time,
                "timestamp" or "timestamp without time zone" or
                    "timestamp with time zone" or "timestamptz" =>
                    DatabaseScalarType.DateTime,
                "uuid" => DatabaseScalarType.Guid,
                "bytea" => DatabaseScalarType.Binary,
                _ => DatabaseScalarType.Unsupported
            },
            DatabaseProvider.SqlServer => type switch
            {
                "char" or "nchar" or "varchar" or "nvarchar" or "text" or
                    "ntext" => DatabaseScalarType.Text,
                "tinyint" or "smallint" or "int" or "bigint" =>
                    DatabaseScalarType.Integer,
                "decimal" or "numeric" or "smallmoney" or "money" or
                    "real" or "float" => DatabaseScalarType.Decimal,
                "bit" => DatabaseScalarType.Boolean,
                "date" => DatabaseScalarType.Date,
                "time" => DatabaseScalarType.Time,
                "datetime" or "datetime2" or "smalldatetime" or
                    "datetimeoffset" => DatabaseScalarType.DateTime,
                "uniqueidentifier" => DatabaseScalarType.Guid,
                "binary" or "varbinary" or "image" or "timestamp" or
                    "rowversion" => DatabaseScalarType.Binary,
                _ => DatabaseScalarType.Unsupported
            },
            _ => DatabaseScalarType.Unsupported
        };
    }
}
