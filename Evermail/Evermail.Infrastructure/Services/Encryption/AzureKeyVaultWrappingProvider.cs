using System.Security.Cryptography;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public class AzureKeyVaultWrappingProvider : IKeyWrappingProvider
{
    private readonly TokenCredential _credential;

    public AzureKeyVaultWrappingProvider(TokenCredential credential)
    {
        _credential = credential;
    }

    public string ProviderName => "AzureKeyVault";

    public async Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);

        var dekBytes = RandomNumberGenerator.GetBytes(32);
        var (cryptoClient, keyVersion) = await CreateCryptographyClientAsync(settings, cancellationToken);

        KeyWrapAlgorithm algorithm = KeyWrapAlgorithm.RsaOaep256;
        var wrapResult = await cryptoClient.WrapKeyAsync(
            algorithm,
            dekBytes,
            cancellationToken);

        var wrappedDek = Convert.ToBase64String(wrapResult.EncryptedKey);

        return new WrappedDekResult(
            PlaintextDek: dekBytes,
            WrappedDekBase64: wrappedDek,
            Algorithm: "AES-256-GCM",
            ProviderKeyVersion: keyVersion,
            ProviderRequestId: wrapResult.KeyId ?? string.Empty,
            ProviderMetadataJson: null);
    }

    public async Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        byte[] wrappedDekBytes,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);

        var (cryptoClient, _) = await CreateCryptographyClientAsync(settings, cancellationToken);

        KeyWrapAlgorithm algorithm = KeyWrapAlgorithm.RsaOaep256;
        var unwrapResult = await cryptoClient.UnwrapKeyAsync(
            algorithm,
            wrappedDekBytes,
            cancellationToken);

        return new UnwrappedDekResult(
            PlaintextDek: unwrapResult.Key,
            ProviderRequestId: unwrapResult.KeyId ?? string.Empty,
            ProviderMetadataJson: null);
    }

    private async Task<(CryptographyClient Client, string Version)> CreateCryptographyClientAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken)
    {
        var keyVersion = settings.KeyVaultKeyVersion;
        if (string.IsNullOrWhiteSpace(keyVersion))
        {
            var keyClient = new KeyClient(new Uri(settings.KeyVaultUri!), _credential);
            var key = await keyClient.GetKeyAsync(settings.KeyVaultKeyName!, cancellationToken: cancellationToken);
            keyVersion = key.Value.Properties.Version ?? throw new InvalidOperationException("Key version not available.");
        }

        var keyUri = $"{settings.KeyVaultUri!.TrimEnd('/')}/keys/{settings.KeyVaultKeyName}/{keyVersion}";
        var cryptoClient = new CryptographyClient(new Uri(keyUri), _credential);
        return (cryptoClient, keyVersion);
    }

    private static void ValidateSettings(TenantEncryptionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.KeyVaultUri) ||
            string.IsNullOrWhiteSpace(settings.KeyVaultKeyName))
        {
            throw new InvalidOperationException("Azure Key Vault settings are incomplete.");
        }
    }
}

