using System.Text;
using MappingDemo.Shared.Contracts;
using MappingDemo.Worker.Destinations;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonExportPublisherTests
{
    [Fact]
    public async Task Reconciles_existing_final_file_when_content_matches()
    {
        var root = CreateOutputRoot();
        try
        {
            var expected = Encoding.UTF8.GetBytes("[{\"id\":1}]");
            var finalPath = Path.Combine(root, "exports", "result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            await File.WriteAllBytesAsync(finalPath, expected);

            await new FileSystemJsonExportPublisher().PublishAsync(
                Profile(root),
                "exports/result.json.tmp",
                "exports/result.json",
                (stream, cancellationToken) =>
                    stream.WriteAsync(expected, cancellationToken).AsTask());

            Assert.Equal(expected, await File.ReadAllBytesAsync(finalPath));
            Assert.False(File.Exists(finalPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Blocks_existing_final_file_when_content_differs()
    {
        var root = CreateOutputRoot();
        try
        {
            var existing = Encoding.UTF8.GetBytes("[{\"owner\":\"other\"}]");
            var expected = Encoding.UTF8.GetBytes("[{\"owner\":\"this-job\"}]");
            var finalPath = Path.Combine(root, "exports", "result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            await File.WriteAllBytesAsync(finalPath, existing);

            var exception = await Assert.ThrowsAsync<JsonExportCollisionException>(
                () => new FileSystemJsonExportPublisher().PublishAsync(
                    Profile(root),
                    "exports/result.json.tmp",
                    "exports/result.json",
                    (stream, cancellationToken) =>
                        stream.WriteAsync(expected, cancellationToken).AsTask()));

            Assert.Equal(
                "The final JSON artifact already exists with different content.",
                exception.Message);
            Assert.Equal(existing, await File.ReadAllBytesAsync(finalPath));
            Assert.False(File.Exists(finalPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_traversal_without_writing_outside_output_root()
    {
        var root = CreateOutputRoot();
        var outside = Path.Combine(Path.GetDirectoryName(root)!, "escaped.json");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FileSystemJsonExportPublisher().PublishAsync(
                    Profile(root),
                    "../escaped.tmp",
                    "../escaped.json",
                    (stream, cancellationToken) => Task.CompletedTask));
            Assert.False(File.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_symbolic_link_beneath_output_root()
    {
        var root = CreateOutputRoot();
        var outside = CreateOutputRoot();
        var link = Path.Combine(root, "exports");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or
                IOException or
                PlatformNotSupportedException)
            {
                return;
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FileSystemJsonExportPublisher().PublishAsync(
                    Profile(root),
                    "exports/result.tmp",
                    "exports/result.json",
                    (stream, cancellationToken) => Task.CompletedTask));
            Assert.Empty(Directory.EnumerateFiles(outside));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_profile_quota_and_low_disk_before_publish()
    {
        var root = CreateOutputRoot();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("[{\"id\":1}]");
            await Assert.ThrowsAsync<IOException>(() =>
                new FileSystemJsonExportPublisher().PublishAsync(
                    Profile(root) with { QuotaBytes = 4 },
                    "exports/quota.tmp",
                    "exports/quota.json",
                    (stream, cancellationToken) =>
                        stream.WriteAsync(bytes, cancellationToken).AsTask()));
            await Assert.ThrowsAsync<IOException>(() =>
                new FileSystemJsonExportPublisher().PublishAsync(
                    Profile(root) with
                    {
                        QuotaBytes = long.MaxValue,
                        MinimumFreeBytes = long.MaxValue
                    },
                    "exports/disk.tmp",
                    "exports/disk.json",
                    (stream, cancellationToken) =>
                        stream.WriteAsync(bytes, cancellationToken).AsTask()));
            Assert.False(File.Exists(Path.Combine(root, "exports", "quota.json")));
            Assert.False(File.Exists(Path.Combine(root, "exports", "disk.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateOutputRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "mapping-demo-json-publisher",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static FileDestinationProfile Profile(string outputRoot) =>
        new(
            "local-json-export",
            true,
            Path.GetFullPath(outputRoot),
            "volume-encrypted",
            1024 * 1024,
            0,
            DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
            10,
            [new SupportedContract("proposal", "1", "proposal/1")],
            []);
}
