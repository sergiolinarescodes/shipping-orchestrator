namespace ShippingOrchestrator.Application.Common.Encryption;

/// <summary>
/// Wraps and unwraps small payloads (OAuth tokens, API keys) using a KMS-managed data key.
/// Implementations: AWS KMS in production, AES-GCM with a dev key in local compose / tests.
/// </summary>
public interface IEnvelopeEncryptor
{
    Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken);
    Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken);
}
