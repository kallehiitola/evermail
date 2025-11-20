using System.Security.Cryptography;
using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public class EvermailManagedWrappingProvider : IKeyWrappingProvider
{
    public string ProviderName => "EvermailManaged";

    public Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var dek = RandomNumberGenerator.GetBytes(32);
        var wrapped = Convert.ToBase64String(dek);

        return Task.FromResult(new WrappedDekResult(
            PlaintextDek: dek,
            WrappedDekBase64: wrapped,
            Algorithm: "AES-256-GCM",
            ProviderKeyVersion: "local",
            ProviderRequestId: "local",
            ProviderMetadataJson: null));
    }

    public Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        byte[] wrappedDekBytes,
        CancellationToken cancellationToken = default)
    {
        // For the managed provider, the "wrapped" value is plaintext.
        var copy = wrappedDekBytes.ToArray();
        return Task.FromResult(new UnwrappedDekResult(
            PlaintextDek: copy,
            ProviderRequestId: "local",
            ProviderMetadataJson: null));
    }
}

