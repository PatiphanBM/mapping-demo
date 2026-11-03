using System.Security.Cryptography;

namespace MappingDemo.Worker.Delivery;

public interface IDeliverySecretProvider
{
    ValueTask<string> GetSecretAsync(
        string reference,
        CancellationToken cancellationToken = default);
}

public sealed class EnvironmentDeliverySecretProvider : IDeliverySecretProvider
{
    public ValueTask<string> GetSecretAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var suffix = string.Concat(reference.Select(character =>
            char.IsAsciiLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_'));
        var variable = $"MAPPING_SECRET__{suffix}";
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value)
            ? ValueTask.FromException<string>(new InvalidOperationException(
                $"Server-owned secret '{reference}' is unavailable."))
            : ValueTask.FromResult(value);
    }
}

public sealed record ProtectedDeliveryPayload(
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    string KeyReference);

public sealed class DeliveryPayloadProtector(
    IDeliverySecretProvider secrets,
    IConfiguration configuration)
{
    private readonly AesGcmPayloadProtector _protector = new(secrets);

    private readonly string _keyReference = configuration[
        "ApiDelivery:PayloadEncryptionKeyReference"] ??
        "delivery-payload-v1";

    public async ValueTask<ProtectedDeliveryPayload> ProtectAsync(
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken)
    {
        return await _protector.ProtectAsync(
            plaintext,
            _keyReference,
            cancellationToken);
    }

    public async ValueTask<byte[]> UnprotectAsync(
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        string keyReference,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                keyReference,
                _keyReference,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Payload key reference '{keyReference}' is not configured.");
        }

        return await _protector.UnprotectAsync(
            ciphertext,
            nonce,
            tag,
            keyReference,
            cancellationToken);
    }
}

public sealed class JsonExportPayloadProtector(
    IDeliverySecretProvider secrets,
    IConfiguration configuration)
{
    private readonly AesGcmPayloadProtector _protector = new(secrets);

    private readonly string _keyReference = configuration[
        "JsonExport:PayloadEncryptionKeyReference"] ??
        "json-export-payload-v1";

    public ValueTask<ProtectedDeliveryPayload> ProtectAsync(
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken) =>
        _protector.ProtectAsync(
            plaintext,
            _keyReference,
            cancellationToken);

    public async ValueTask<byte[]> UnprotectAsync(
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        string keyReference,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                keyReference,
                _keyReference,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Payload key reference '{keyReference}' is not configured.");
        }

        return await _protector.UnprotectAsync(
            ciphertext,
            nonce,
            tag,
            keyReference,
            cancellationToken);
    }
}

internal sealed class AesGcmPayloadProtector(IDeliverySecretProvider secrets)
{
    public async ValueTask<ProtectedDeliveryPayload> ProtectAsync(
        ReadOnlyMemory<byte> plaintext,
        string keyReference,
        CancellationToken cancellationToken)
    {
        var key = await ResolveKeyAsync(keyReference, cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);
        CryptographicOperations.ZeroMemory(key);
        return new ProtectedDeliveryPayload(
            ciphertext,
            nonce,
            tag,
            keyReference);
    }

    public async ValueTask<byte[]> UnprotectAsync(
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        string keyReference,
        CancellationToken cancellationToken)
    {
        var key = await ResolveKeyAsync(keyReference, cancellationToken);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce.Span, ciphertext.Span, tag.Span, plaintext);
        CryptographicOperations.ZeroMemory(key);
        return plaintext;
    }

    private async ValueTask<byte[]> ResolveKeyAsync(
        string keyReference,
        CancellationToken cancellationToken)
    {
        var encoded = await secrets.GetSecretAsync(
            keyReference,
            cancellationToken);
        byte[] key;
        try
        {
            key = Convert.FromBase64String(encoded);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "The delivery payload encryption key must be base64.",
                exception);
        }

        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException(
                "The delivery payload encryption key must contain 32 bytes.");
        }

        return key;
    }
}
