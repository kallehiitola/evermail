using Evermail.Domain.Entities;

namespace Evermail.WebApp.Services.Onboarding;

public static class OnboardingStatusCalculator
{
    public static bool IsEncryptionConfigured(TenantEncryptionSettings settings)
    {
        if (settings is null)
        {
            return false;
        }

        var provider = settings.Provider ?? "AzureKeyVault";

        return provider switch
        {
            "AwsKms" => !string.IsNullOrWhiteSpace(settings.AwsKmsKeyArn) &&
                        !string.IsNullOrWhiteSpace(settings.AwsIamRoleArn),
            "EvermailManaged" => true,
            "Offline" => !string.IsNullOrWhiteSpace(settings.OfflineMasterKeyCiphertext),
            _ => !string.IsNullOrWhiteSpace(settings.KeyVaultUri) &&
                 !string.IsNullOrWhiteSpace(settings.KeyVaultKeyName)
        };
    }

    public static bool IsOnboardingComplete(
        Tenant tenant,
        bool encryptionConfigured,
        bool hasMailbox)
    {
        if (tenant is null)
        {
            return true;
        }

        return tenant.OnboardingPlanConfirmedAt.HasValue &&
               tenant.PaymentAcknowledgedAt.HasValue &&
               encryptionConfigured &&
               hasMailbox;
    }
}


