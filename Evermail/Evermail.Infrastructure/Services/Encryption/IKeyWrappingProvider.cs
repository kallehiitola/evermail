using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public interface IKeyWrappingProvider
{
    string ProviderName { get; }

    Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default);

    Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        byte[] wrappedDekBytes,
        CancellationToken cancellationToken = default);
}

