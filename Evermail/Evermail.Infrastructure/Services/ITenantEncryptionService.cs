using Evermail.Common.DTOs.Tenant;

namespace Evermail.Infrastructure.Services;

public interface ITenantEncryptionService
{
    Task<TenantEncryptionSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantEncryptionSettingsDto> UpsertSettingsAsync(Guid tenantId, Guid userId, UpsertTenantEncryptionSettingsRequest request, CancellationToken cancellationToken = default);
    Task<TenantEncryptionTestResultDto> TestAccessAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantEncryptionHistoryItemDto>> GetEncryptionHistoryAsync(Guid tenantId, int limit = 20, CancellationToken cancellationToken = default);
}


