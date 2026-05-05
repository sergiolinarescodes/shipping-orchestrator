using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ShippingOrchestrator.Application.Common.Encryption;

namespace ShippingOrchestrator.Infrastructure.Encryption;

public sealed class AesEnvelopeOptions
{
    /// <summary>
    /// 32-byte key as a base64 string. For local dev only — production hosts use the
    /// <c>KmsEnvelopeEncryptor</c> instead so secrets never sit in app config.
    /// </summary>
    public string Base64Key { get; set; } = string.Empty;
}

/// <summary>
/// AES-GCM dev-only envelope encryptor. Drop-in for <see cref="IEnvelopeEncryptor"/>
/// when AWS KMS is unavailable (local compose, integration tests). Replaced by the KMS
/// implementation in production via configuration.
/// </summary>
public sealed class AesEnvelopeEncryptor : IEnvelopeEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesEnvelopeEncryptor(IOptions<AesEnvelopeOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Base64Key))
            throw new InvalidOperationException("AesEnvelopeOptions:Base64Key is required.");
        _key = Convert.FromBase64String(options.Value.Base64Key);
        if (_key.Length != 32)
            throw new InvalidOperationException("AesEnvelopeOptions:Base64Key must decode to 32 bytes (AES-256).");
    }

    public Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var output = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);
        return Task.FromResult(output);
    }

    public Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length < NonceSize + TagSize)
            throw new ArgumentException("Ciphertext is too short.", nameof(ciphertext));
        var nonce = ciphertext.AsSpan(0, NonceSize);
        var tag = ciphertext.AsSpan(NonceSize, TagSize);
        var data = ciphertext.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[data.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, data, tag, plaintext);
        return Task.FromResult(plaintext);
    }
}
