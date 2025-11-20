using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public class AwsKmsWrappingProvider : IKeyWrappingProvider
{
    private readonly IAwsKmsConnector _connector;

    public AwsKmsWrappingProvider(IAwsKmsConnector connector)
    {
        _connector = connector;
    }

    public string ProviderName => "AwsKms";

    public async Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var response = await _connector.GenerateDataKeyAsync(settings, cancellationToken);
        return new WrappedDekResult(
            PlaintextDek: response.Plaintext,
            WrappedDekBase64: Convert.ToBase64String(response.Ciphertext),
            Algorithm: "AES-256-GCM",
            ProviderKeyVersion: response.KeyId,
            ProviderRequestId: response.RequestId,
            ProviderMetadataJson: null);
    }

    public async Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        byte[] wrappedDekBytes,
        CancellationToken cancellationToken = default)
    {
        var response = await _connector.DecryptAsync(settings, wrappedDekBytes, cancellationToken);
        return new UnwrappedDekResult(
            PlaintextDek: response.Plaintext,
            ProviderRequestId: response.RequestId,
            ProviderMetadataJson: null);
    }
}

