namespace Evermail.Common.DTOs.Tenant;

public record TenantEncryptionSettingsDto(
    bool IsConfigured,
    string EncryptionPhase,
    string? KeyVaultUri,
    string? KeyVaultKeyName,
    string? KeyVaultKeyVersion,
    string? KeyVaultTenantId,
    string? ManagedIdentityObjectId,
    DateTime? UpdatedAt,
    DateTime? LastVerifiedAt,
    string? LastVerificationMessage,
    bool IsSecureKeyReleaseConfigured,
    DateTime? SecureKeyReleaseConfiguredAt);

public record UpsertTenantEncryptionSettingsRequest(
    string KeyVaultUri,
    string KeyVaultKeyName,
    string? KeyVaultKeyVersion,
    string KeyVaultTenantId,
    string? ManagedIdentityObjectId);

public record TenantEncryptionTestResultDto(
    bool Success,
    string Message,
    DateTime Timestamp);


