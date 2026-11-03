using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MappingDemo.Shared.Destinations;

public static class JsonArtifactSerializer
{
    public static async Task<JsonArtifactSerializationResult> SerializeAsync(
        IReadOnlyList<string> objects,
        string formatting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objects);
        await using var destination = new MemoryStream();
        var metadata = await WriteAsync(
            destination,
            formatting,
            ToUtf8Objects(objects, cancellationToken),
            cancellationToken);
        return new JsonArtifactSerializationResult(
            destination.ToArray(),
            metadata);
    }

    public static async Task<JsonArtifactMetadata> WriteAsync(
        Stream destination,
        string formatting,
        IAsyncEnumerable<ReadOnlyMemory<byte>> objects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(objects);
        var indented = formatting switch
        {
            "compact" => false,
            "indented" => true,
            _ => throw new InvalidOperationException(
                $"Unsupported JSON export formatting '{formatting}'.")
        };

        await using var hashing = new HashingWriteStream(destination);
        await using var normalizedNewlines = indented
            ? new CarriageReturnFilteringStream(hashing)
            : null;
        Stream writerStream = normalizedNewlines is null
            ? hashing
            : normalizedNewlines;
        using var writer = new Utf8JsonWriter(
            writerStream,
            new JsonWriterOptions
            {
                Indented = indented,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        writer.WriteStartArray();
        var objectCount = 0;
        await foreach (var objectBytes in objects.WithCancellation(
                           cancellationToken))
        {
            if (objectBytes.Length > JsonArtifactLimits.MaxObjectBytes)
            {
                throw new InvalidOperationException(
                    "A staged JSON export object exceeds the 1 MiB limit.");
            }
            if (++objectCount > JsonArtifactLimits.MaxObjectsPerArtifact)
            {
                throw new InvalidOperationException(
                    "A JSON export exceeds the 320,000 object limit.");
            }

            using var document = JsonDocument.Parse(objectBytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "A staged JSON export payload must be an object.");
            }
            document.RootElement.WriteTo(writer);
            writer.Flush();
            await writerStream.FlushAsync(cancellationToken);
            if (hashing.BytesWritten > JsonArtifactLimits.MaxArtifactBytes)
            {
                throw new InvalidOperationException(
                    "A JSON export exceeds the 256 MiB artifact limit.");
            }
        }

        writer.WriteEndArray();
        writer.Flush();
        await writerStream.FlushAsync(cancellationToken);
        if (hashing.BytesWritten > JsonArtifactLimits.MaxArtifactBytes)
        {
            throw new InvalidOperationException(
                "A JSON export exceeds the 256 MiB artifact limit.");
        }
        return new JsonArtifactMetadata(
            objectCount,
            hashing.BytesWritten,
            "sha256:" + Convert.ToHexString(
                hashing.GetHashAndReset()).ToLowerInvariant());
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToUtf8Objects(
        IReadOnlyList<string> objects,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var value in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Encoding.UTF8.GetBytes(value);
            await Task.Yield();
        }
    }

    private sealed class HashingWriteStream(Stream inner) : Stream
    {
        private readonly IncrementalHash _hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public byte[] GetHashAndReset() => _hash.GetHashAndReset();

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _hash.AppendData(buffer, offset, count);
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _hash.AppendData(buffer);
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _hash.AppendData(buffer.Span);
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class CarriageReturnFilteringStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var written = CopyWithoutCarriageReturns(buffer, rented);
                inner.Write(rented.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var written = CopyWithoutCarriageReturns(
                    buffer.Span,
                    rented);
                await inner.WriteAsync(
                    rented.AsMemory(0, written),
                    cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        private static int CopyWithoutCarriageReturns(
            ReadOnlySpan<byte> source,
            Span<byte> destination)
        {
            var written = 0;
            foreach (var value in source)
            {
                if (value != (byte)'\r')
                {
                    destination[written++] = value;
                }
            }
            return written;
        }
    }
}

public sealed record JsonArtifactMetadata(
    int ObjectCount,
    long ByteCount,
    string Sha256);

public sealed record JsonArtifactSerializationResult(
    byte[] Bytes,
    JsonArtifactMetadata Metadata);
