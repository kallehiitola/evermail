namespace Evermail.WebApp.Services.Onboarding;

public interface IOnboardingStatusService
{
    Task<bool> IsOnboardingCompleteAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> ResetAsync(Guid tenantId, CancellationToken cancellationToken);
    void Invalidate(Guid tenantId);
}





