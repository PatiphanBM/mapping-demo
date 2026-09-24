using System.Text.RegularExpressions;

namespace MappingDemo.Shared.Tables;

public static class IdentifierRules
{
    private static readonly Regex ValidIdentifierPattern = new(
        "\\A[a-z][a-z0-9_]{0,62}\\z",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ReservedNames = new(
        StringComparer.Ordinal)
    {
        "id",
        "file_job_id",
        "row_number",
        "imported_at",
        "source_row_id",
        "row_job_id",
        "config_version_id",
        "normalized_at"
    };

    public static bool IsValid(string? identifier)
    {
        return identifier is not null &&
               ValidIdentifierPattern.IsMatch(identifier) &&
               !identifier.StartsWith("pg_", StringComparison.Ordinal) &&
               !ReservedNames.Contains(identifier);
    }
}
