using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Encryption;

public interface IKeyWrappingService
{
    Task<WrappedDekResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default);

    Task<UnwrappedDekResult> UnwrapDataKeyAsync(
        TenantEncryptionSettings settings,
        string wrappedDekBase64,
        CancellationToken cancellationToken = default);
}

