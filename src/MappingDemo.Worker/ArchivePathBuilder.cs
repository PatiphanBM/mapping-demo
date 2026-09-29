using System.Globalization;

namespace MappingDemo.Worker;

public static class ArchivePathBuilder
{
    public static string Build(
        string archiveRootPath,
        long configId,
        long fileJobId,
        string fileName,
        DateOnly archiveDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Path.Combine(
            archiveRootPath,
            configId.ToString(CultureInfo.InvariantCulture),
            archiveDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            $"{fileJobId}_{Path.GetFileName(fileName)}");
    }
}
