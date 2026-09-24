namespace MappingDemo.Shared.Tables;

public static class SqlIdentifier
{
    public static string Quote(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return "\"" +
               identifier.Replace("\"", "\"\"", StringComparison.Ordinal) +
               "\"";
    }
}
