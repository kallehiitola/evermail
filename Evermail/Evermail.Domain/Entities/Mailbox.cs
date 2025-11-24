using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class Mailbox
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    [Required, MaxLength(64)]
    public Guid UserId { get; set; }
    
    // User defined metadata
    [MaxLength(500)]
    public string? DisplayName { get; set; }
    
    // File Info (legacy snapshot of latest upload)
    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    public long NormalizedSizeBytes { get; set; }

    public bool IsClientEncrypted { get; set; }

    [MaxLength(100)]
    public string? EncryptionScheme { get; set; }

    public string? EncryptionMetadataJson { get; set; }

    [MaxLength(128)]
    public string? EncryptionKeyFingerprint { get; set; }

    [MaxLength(64)]
    public string? ZeroAccessTokenSalt { get; set; }

    [Required, MaxLength(64)]
    public string SourceFormat { get; set; } = "mbox";
    
    [Required, MaxLength(1000)]
    public string BlobPath { get; set; } = string.Empty;
    
    // Processing Status
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    // Statistics
    public int TotalEmails { get; set; }
    public int ProcessedEmails { get; set; }
    public int FailedEmails { get; set; }
    public long ProcessedBytes { get; set; } // Track bytes processed for progress calculation
    
    // Lifecycle / uploads
    public Guid? LatestUploadId { get; set; }
    public DateTime? UploadRemovedAt { get; set; }
    public Guid? UploadRemovedByUserId { get; set; }
    public bool IsPendingDeletion { get; set; }
    public DateTime? SoftDeletedAt { get; set; }
    public Guid? SoftDeletedByUserId { get; set; }
    public DateTime? PurgeAfter { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<MailboxUpload> Uploads { get; set; } = new List<MailboxUpload>();
    public MailboxUpload? LatestUpload { get; set; }
}

