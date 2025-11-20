using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class MailboxEncryptionState
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    [Required]
    public Guid MailboxId { get; set; }
    
    [Required]
    public Guid MailboxUploadId { get; set; }
    
    [Required, MaxLength(50)]
    public string Algorithm { get; set; } = "AES-256-GCM";
    
    [Required]
    public string WrappedDek { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? DekVersion { get; set; }
    
    [MaxLength(100)]
    public string? TenantKeyVersion { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastKeyReleaseAt { get; set; }
    
    [MaxLength(200)]
    public string? LastKeyReleaseComponent { get; set; }
    
    [MaxLength(200)]
    public string? LastKeyReleaseLedgerEntryId { get; set; }
    
    [MaxLength(200)]
    public string? AttestationPolicyId { get; set; }
    
    [MaxLength(200)]
    public string? KeyVaultKeyVersion { get; set; }
    
    [MaxLength(50)]
    public string Provider { get; set; } = "AzureKeyVault";
    
    [MaxLength(200)]
    public string? ProviderKeyVersion { get; set; }
    
    [MaxLength(200)]
    public string? WrapRequestId { get; set; }
    
    [MaxLength(200)]
    public string? LastUnwrapRequestId { get; set; }
    
    public string? ProviderMetadata { get; set; }
    
    public Mailbox Mailbox { get; set; } = null!;
    public MailboxUpload MailboxUpload { get; set; } = null!;
}


