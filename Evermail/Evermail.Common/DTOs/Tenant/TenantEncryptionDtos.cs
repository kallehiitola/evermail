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


