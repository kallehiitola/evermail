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
            "AwsKms" => HasAwsSettings(settings) && settings.IsSecureKeyReleaseConfigured,
            "EvermailManaged" => true,
            "Offline" => !string.IsNullOrWhiteSpace(settings.OfflineMasterKeyCiphertext),
            _ => HasAzureSettings(settings) && settings.IsSecureKeyReleaseConfigured
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

    private static bool HasAzureSettings(TenantEncryptionSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.KeyVaultUri) &&
        !string.IsNullOrWhiteSpace(settings.KeyVaultKeyName);

    private static bool HasAwsSettings(TenantEncryptionSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.AwsKmsKeyArn) &&
        !string.IsNullOrWhiteSpace(settings.AwsIamRoleArn);
}


