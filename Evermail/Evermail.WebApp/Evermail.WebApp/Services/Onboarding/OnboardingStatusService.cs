using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Evermail.WebApp.Services.Onboarding;

public sealed class OnboardingStatusService : IOnboardingStatusService
{
    private readonly EvermailDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OnboardingStatusService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public OnboardingStatusService(
        EvermailDbContext context,
        IMemoryCache cache,
        ILogger<OnboardingStatusService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsOnboardingCompleteAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return true;
        }

        var cacheKey = BuildCacheKey(tenantId);
        if (_cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return cachedResult;
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found while checking onboarding status. Allowing access.", tenantId);
            _cache.Set(cacheKey, true, CacheDuration);
            return true;
        }

        var encryptionSettings = await _context.TenantEncryptionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        var hasMailbox = await _context.Mailboxes
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantId, cancellationToken);

        var encryptionConfigured = encryptionSettings is not null &&
            OnboardingStatusCalculator.IsEncryptionConfigured(encryptionSettings);

        var complete = OnboardingStatusCalculator.IsOnboardingComplete(tenant, encryptionConfigured, hasMailbox);

        _cache.Set(cacheKey, complete, CacheDuration);
        return complete;
    }

    public async Task<bool> ResetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return false;
        }

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return false;
        }

        tenant.OnboardingPlanConfirmedAt = null;
        tenant.PaymentAcknowledgedAt = null;
        tenant.PaymentAcknowledgedByUserId = null;
        tenant.SecurityPreference = "QuickStart";
        tenant.SecurityLevel = "FullService";
        tenant.UpdatedAt = DateTime.UtcNow;

        var encryptionSettings = await _context.TenantEncryptionSettings
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (encryptionSettings.Count > 0)
        {
            _context.TenantEncryptionSettings.RemoveRange(encryptionSettings);
        }

        await _context.SaveChangesAsync(cancellationToken);
        Invalidate(tenantId);
        _logger.LogInformation("Reset onboarding state for tenant {TenantId}", tenantId);
        return true;
    }

    public void Invalidate(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return;
        }

        _cache.Remove(BuildCacheKey(tenantId));
    }

    private static string BuildCacheKey(Guid tenantId) => $"onboarding-status:{tenantId}";
}

