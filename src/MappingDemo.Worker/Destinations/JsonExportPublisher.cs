using System.Security.Cryptography;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Worker.Destinations;

public interface IJsonExportPublisher
{
    Task PublishAsync(
        FileDestinationProfile profile,
        string relativeTempPath,
        string relativeFinalPath,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default);
}

public interface IJsonExportArtifactCleaner
{
    Task DeleteAsync(
        FileDestinationProfile profile,
        string relativePath,
        CancellationToken cancellationToken = default);
}

public sealed class JsonExportCollisionException()
    : InvalidOperationException(
        "The final JSON artifact already exists with different content.");

public sealed class FileSystemJsonExportPublisher :
    IJsonExportPublisher,
    IJsonExportArtifactCleaner
{
    public Task DeleteAsync(
        FileDestinationProfile profile,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var rootPath = Path.GetFullPath(profile.OutputRoot);
        var fullPath = ResolveContainedPath(rootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "JSON export artifact path has no directory.");
        if (Directory.Exists(directory))
        {
            RejectReparsePoints(rootPath, directory);
        }
        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(
        FileDestinationProfile profile,
        string relativeTempPath,
        string relativeFinalPath,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeTempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeFinalPath);
        ArgumentNullException.ThrowIfNull(writeAsync);

        var rootPath = Path.GetFullPath(profile.OutputRoot);
        Directory.CreateDirectory(rootPath);
        var tempPath = ResolveContainedPath(rootPath, relativeTempPath);
        var finalPath = ResolveContainedPath(rootPath, relativeFinalPath);
        if (string.Equals(tempPath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "JSON export temp and final paths must be different.");
        }

        var tempDirectory = Path.GetDirectoryName(tempPath)
            ?? throw new InvalidOperationException(
                "JSON export temp path has no directory.");
        var finalDirectory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException(
                "JSON export final path has no directory.");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(finalDirectory);
        RejectReparsePoints(rootPath, tempDirectory);
        RejectReparsePoints(rootPath, finalDirectory);

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 64 * 1024,
                    Options = FileOptions.Asynchronous |
                              FileOptions.WriteThrough
                }))
            {
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            EnsureStoragePolicy(profile, rootPath);

            try
            {
                File.Move(tempPath, finalPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                if (!await FilesMatchAsync(
                        tempPath,
                        finalPath,
                        cancellationToken))
                {
                    throw new JsonExportCollisionException();
                }

                TryDeleteTemp(tempPath);
            }
        }
        catch
        {
            TryDeleteTemp(tempPath);
            throw;
        }
    }

    private static void EnsureStoragePolicy(
        FileDestinationProfile profile,
        string rootPath)
    {
        var usedBytes = 0L;
        foreach (var path in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     }))
        {
            var length = new FileInfo(path).Length;
            if (length > profile.QuotaBytes - usedBytes)
            {
                throw new IOException(
                    "JSON export profile quota would be exceeded.");
            }
            usedBytes += length;
        }

        var driveRoot = Path.GetPathRoot(rootPath)
            ?? throw new IOException(
                "JSON export Output Root has no storage volume.");
        if (new DriveInfo(driveRoot).AvailableFreeSpace <
            profile.MinimumFreeBytes)
        {
            throw new IOException(
                "JSON export storage has insufficient free space.");
        }
    }

    private static async Task<bool> FilesMatchAsync(
        string firstPath,
        string secondPath,
        CancellationToken cancellationToken)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        await using var first = new FileStream(
            firstPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var second = new FileStream(
            secondPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var firstHash = await SHA256.HashDataAsync(first, cancellationToken);
        var secondHash = await SHA256.HashDataAsync(second, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
    }

    private static string ResolveContainedPath(
        string rootPath,
        string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException(
                "JSON export paths must be relative to the configured Output Root.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "JSON export path escapes the configured Output Root.");
        }

        return fullPath;
    }

    private static void RejectReparsePoints(
        string rootPath,
        string directoryPath)
    {
        var root = new DirectoryInfo(rootPath);
        for (var current = new DirectoryInfo(directoryPath);
             current is not null;
             current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "JSON export paths cannot traverse a reparse point.");
            }

            if (string.Equals(
                    current.FullName,
                    root.FullName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "JSON export path is outside the configured Output Root.");
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
