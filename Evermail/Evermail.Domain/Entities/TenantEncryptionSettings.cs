using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class TenantEncryptionSettings
{
    [Key]
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    [MaxLength(50)]
    public string Provider { get; set; } = "AzureKeyVault"; // EvermailManaged, AzureKeyVault, AwsKms, …

    [MaxLength(500)]
    public string? KeyVaultUri { get; set; }

    [MaxLength(200)]
    public string? KeyVaultKeyName { get; set; }

    [MaxLength(200)]
    public string? KeyVaultKeyVersion { get; set; }

    [MaxLength(64)]
    public string? KeyVaultTenantId { get; set; }

    [MaxLength(100)]
    public string? ManagedIdentityObjectId { get; set; }

    [MaxLength(32)]
    public string? AwsAccountId { get; set; }

    [MaxLength(32)]
    public string? AwsRegion { get; set; }

    [MaxLength(2048)]
    public string? AwsKmsKeyArn { get; set; }

    [MaxLength(2048)]
    public string? AwsIamRoleArn { get; set; }

    [MaxLength(128)]
    public string? AwsExternalId { get; set; }

    public string? ProviderMetadata { get; set; }

    [MaxLength(50)]
    public string EncryptionPhase { get; set; } = "NotConfigured";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    [MaxLength(500)]
    public string? LastVerificationMessage { get; set; }

    public bool IsSecureKeyReleaseConfigured { get; set; }
    public DateTime? SecureKeyReleaseConfiguredAt { get; set; }
    public Guid? SecureKeyReleaseConfiguredByUserId { get; set; }

    public string? SecureKeyReleasePolicyJson { get; set; }

    [MaxLength(128)]
    public string? SecureKeyReleasePolicyHash { get; set; }

    [MaxLength(128)]
    public string? AttestationProvider { get; set; }

    [MaxLength(200)]
    public string? OfflineTenantLabel { get; set; }

    [MaxLength(50)]
    public string? OfflineBundleVersion { get; set; }

    public DateTime? OfflineKeyCreatedAt { get; set; }

    [MaxLength(128)]
    public string? OfflineKeyChecksum { get; set; }

    public string? OfflineMasterKeyCiphertext { get; set; }
}


