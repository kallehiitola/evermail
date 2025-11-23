using System.Collections.Generic;

namespace Evermail.Common.DTOs.Tenant;

public record TenantEncryptionSettingsDto(
    string Provider,
    string EncryptionPhase,
    bool IsConfigured,
    DateTime? UpdatedAt,
    DateTime? LastVerifiedAt,
    string? LastVerificationMessage,
    SecureKeyReleaseDto SecureKeyRelease,
    AzureTenantEncryptionSettingsDto? Azure,
    AwsTenantEncryptionSettingsDto? Aws);

public record SecureKeyReleaseDto(
    bool IsConfigured,
    DateTime? ConfiguredAt,
    string? AttestationProvider);

public record AzureTenantEncryptionSettingsDto(
    string? KeyVaultUri,
    string? KeyVaultKeyName,
    string? KeyVaultKeyVersion,
    string? KeyVaultTenantId,
    string? ManagedIdentityObjectId);

public record AwsTenantEncryptionSettingsDto(
    string? AccountId,
    string? Region,
    string? KmsKeyArn,
    string? IamRoleArn,
    string? ExternalId);

public record UpsertTenantEncryptionSettingsRequest(
    string Provider,
    AzureTenantEncryptionUpsertDto? Azure,
    AwsTenantEncryptionUpsertDto? Aws);

public record AzureTenantEncryptionUpsertDto(
    string KeyVaultUri,
    string KeyVaultKeyName,
    string? KeyVaultKeyVersion,
    string KeyVaultTenantId,
    string? ManagedIdentityObjectId);

public record AwsTenantEncryptionUpsertDto(
    string KmsKeyArn,
    string IamRoleArn,
    string Region,
    string AccountId);

public record TenantEncryptionTestResultDto(
    bool Success,
    string Message,
    DateTime Timestamp);

public record TenantOnboardingStatusDto(
    bool HasAdmin,
    bool EncryptionConfigured,
    bool HasMailbox,
    bool PlanConfirmed,
    string SubscriptionTier,
    string SecurityPreference,
    bool PaymentAcknowledged,
    DateTime? PaymentAcknowledgedAt,
    string? IdentityProvider);

public record SubscriptionPlanDto(
    string Name,
    string DisplayName,
    string Description,
    decimal PriceMonthly,
    decimal? PriceYearly,
    string Currency,
    int MaxStorageGb,
    int MaxFileSizeGb,
    int MaxUsers,
    int MaxMailboxes,
    bool IsRecommended,
    IReadOnlyList<string> Features);

public record SelectSubscriptionPlanRequest(string PlanName);

public record SetSecurityPreferenceRequest(string Mode);

public record PaymentAcknowledgementRequest(bool Acknowledged);

public record SecurityPreferenceResponse(string SecurityPreference);

public record PaymentAcknowledgementResponse(DateTime? PaymentAcknowledgedAt);

public record OfflineByokUploadRequest(
    string Version,
    string? TenantLabel,
    DateTime CreatedAt,
    string WrappedDek,
    string Salt,
    string Nonce,
    string Checksum,
    string Passphrase);

public record TenantEncryptionHistoryItemDto(
    Guid Id,
    Guid MailboxId,
    Guid MailboxUploadId,
    string Provider,
    string Algorithm,
    DateTime CreatedAt,
    string? ProviderKeyVersion,
    string? WrapRequestId,
    string? LastUnwrapRequestId,
    string? ProviderMetadata);


