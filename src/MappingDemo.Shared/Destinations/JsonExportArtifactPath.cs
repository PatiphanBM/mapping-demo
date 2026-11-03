using System.Globalization;

namespace MappingDemo.Shared.Destinations;

public static class JsonExportArtifactPath
{
    public static string Resolve(
        JsonFileDestination destination,
        string sourceFileName,
        long fileJobId,
        int configVersion,
        long configVersionId,
        int artifactRevision)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);

        var fileName = destination.FileNameTemplate
            .Replace(
                "{sourceBaseName}",
                Path.GetFileNameWithoutExtension(sourceFileName),
                StringComparison.Ordinal)
            .Replace(
                "{fileJobId}",
                fileJobId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{configVersion}",
                configVersion.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{configVersionId}",
                configVersionId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{artifactRevision}",
                artifactRevision.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        return string.IsNullOrEmpty(destination.RelativeFolder)
            ? fileName
            : $"{destination.RelativeFolder.TrimEnd('/')}/{fileName}";
    }
}
