using System.Globalization;

namespace MappingDemo.Worker;

public static class ArchivePathBuilder
{
    public static string Build(
        string archiveRootPath,
        string fileName,
        DateOnly archiveDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Path.Combine(
            archiveRootPath,
            archiveDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            Path.GetFileName(fileName));
    }
}
