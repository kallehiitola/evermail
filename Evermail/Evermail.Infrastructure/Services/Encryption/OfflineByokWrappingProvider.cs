using System.Security.Cryptography;
using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public sealed class OfflineByokWrappingProvider : IKeyWrappingProvider
{
    private readonly IOfflineByokKeyProtector _protector;

    public OfflineByokWrappingProvider(IOfflineByokKeyProtector protector)
    {
        _protector = protector;
    }

    public string ProviderName => "Offline";

    public Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var masterKey = ResolveMasterKey(settings);

        var dek = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[dek.Length];
        var tag = new byte[16];

        try
        {
            using var aes = new AesGcm(masterKey, tag.Length);
            aes.Encrypt(nonce, dek, ciphertext, tag);

            var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

            var wrappedDek = Convert.ToBase64String(payload);

            return Task.FromResult(new WrappedDekResult(
                PlaintextDek: dek,
                WrappedDekBase64: wrappedDek,
                Algorithm: "AES-256-GCM",
                ProviderKeyVersion: settings.OfflineBundleVersion ?? "offline-byok/v1",
                ProviderRequestId: $"offline-{Guid.NewGuid():N}",
                ProviderMetadataJson: null));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    public Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        byte[] wrappedDekBytes,
        CancellationToken cancellationToken = default)
    {
        var masterKey = ResolveMasterKey(settings);

        try
        {
            if (wrappedDekBytes.Length < 12 + 16)
            {
                throw new InvalidOperationException("Wrapped DEK payload is invalid.");
            }

            var nonce = wrappedDekBytes.AsSpan(0, 12);
            var tag = wrappedDekBytes.AsSpan(12, 16);
            var cipher = wrappedDekBytes.AsSpan(28);

            var plaintext = new byte[cipher.Length];
            using var aes = new AesGcm(masterKey, tag.Length);
            aes.Decrypt(nonce, cipher, tag, plaintext);

            return Task.FromResult(new UnwrappedDekResult(
                PlaintextDek: plaintext,
                ProviderRequestId: $"offline-unwrap-{Guid.NewGuid():N}",
                ProviderMetadataJson: null));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private byte[] ResolveMasterKey(TenantEncryptionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OfflineMasterKeyCiphertext))
        {
            throw new InvalidOperationException("Offline BYOK has not been configured for this tenant.");
        }

        return _protector.Unprotect(settings.OfflineMasterKeyCiphertext);
    }
}





