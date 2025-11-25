using Evermail.Common.DTOs.Tenant;

namespace Evermail.Infrastructure.Services;

public interface ITenantEncryptionService
{
    Task<TenantEncryptionSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantEncryptionSettingsDto> UpsertSettingsAsync(Guid tenantId, Guid userId, UpsertTenantEncryptionSettingsRequest request, CancellationToken cancellationToken = default);
    Task<TenantEncryptionSettingsDto> UploadOfflineBundleAsync(Guid tenantId, Guid userId, OfflineByokUploadRequest request, CancellationToken cancellationToken = default);
    Task<TenantEncryptionTestResultDto> TestAccessAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantEncryptionHistoryItemDto>> GetEncryptionHistoryAsync(Guid tenantId, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantEncryptionBundleDto>> GetBundlesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantEncryptionBundleDto> CreateBundleAsync(Guid tenantId, Guid userId, CreateTenantEncryptionBundleRequest request, CancellationToken cancellationToken = default);
    Task DeleteBundleAsync(Guid tenantId, Guid bundleId, Guid userId, CancellationToken cancellationToken = default);
    Task<SecureKeyReleaseTemplateDto> GetSecureKeyReleaseTemplateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SecureKeyReleasePolicyDto> GetSecureKeyReleasePolicyAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SecureKeyReleasePolicyDto> ConfigureSecureKeyReleaseAsync(Guid tenantId, Guid userId, ConfigureSecureKeyReleaseRequest request, CancellationToken cancellationToken = default);
    Task DeleteSecureKeyReleasePolicyAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}


