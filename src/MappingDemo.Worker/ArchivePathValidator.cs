namespace MappingDemo.Worker;

public static class ArchivePathValidator
{
    public static void Validate(
        string inputRoot,
        string archiveRoot,
        string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var inputRootPath = Path.GetFullPath(inputRoot, contentRoot);
        var archiveRootPath = Path.GetFullPath(archiveRoot, contentRoot);
        var relativePath = Path.GetRelativePath(
            inputRootPath,
            archiveRootPath);
        var isOutsideInputRoot = Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);

        if (!isOutsideInputRoot)
        {
            throw new InvalidOperationException(
                $"Configuration 'Paths:ArchiveRoot' must not be the same as or inside 'Paths:InputRoot'. Input root: '{inputRootPath}'. Archive root: '{archiveRootPath}'.");
        }
    }
}
