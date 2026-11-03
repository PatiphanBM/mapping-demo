using System.Text.RegularExpressions;

namespace MappingDemo.Shared.Tables;

public static partial class PostgresTypeCanonicalizer
{
    public static string Canonicalize(string dataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataType);

        var value = Whitespace().Replace(
            dataType.Trim().ToLowerInvariant(),
            " ");
        value = CommaWhitespace().Replace(value, ",");

        return value switch
        {
            "int" or "int4" => "integer",
            "int8" => "bigint",
            "bool" => "boolean",
            "timestamptz" => "timestamp with time zone",
            "timestamp" => "timestamp without time zone",
            "decimal" => "numeric",
            _ when value.StartsWith("varchar(", StringComparison.Ordinal) =>
                $"character varying{value[7..]}",
            _ when value.StartsWith("decimal(", StringComparison.Ordinal) =>
                $"numeric{value[7..]}",
            _ => value
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\s*,\s*")]
    private static partial Regex CommaWhitespace();
}
