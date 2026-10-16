using System.Text;

namespace MappingDemo.Shared.Input;

internal static class Utf8InputDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static async Task<Utf8InputDecodeResult> DecodeAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var preambleLength = HasPreamble(bytes) ? 3 : 0;

        try
        {
            var text = StrictUtf8.GetString(
                bytes,
                preambleLength,
                bytes.Length - preambleLength);
            return new Utf8InputDecodeResult(text, null);
        }
        catch (DecoderFallbackException exception)
        {
            var byteOffset = preambleLength + exception.Index;
            var physicalLineNumber = 1;
            for (var index = 0; index < byteOffset; index++)
            {
                if (bytes[index] == (byte)'\n')
                {
                    physicalLineNumber++;
                }
            }

            return new Utf8InputDecodeResult(
                null,
                new InputRecordReadError(
                    "InvalidEncoding",
                    physicalLineNumber,
                    ByteOffset: byteOffset));
        }
    }

    private static bool HasPreamble(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 3 &&
               bytes[0] == 0xEF &&
               bytes[1] == 0xBB &&
               bytes[2] == 0xBF;
    }
}

internal sealed record Utf8InputDecodeResult(
    string? Text,
    InputRecordReadError? Error);
