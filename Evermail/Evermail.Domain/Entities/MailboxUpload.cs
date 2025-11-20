using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class MailboxUpload
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    [Required]
    public Guid MailboxId { get; set; }
    
    [Required]
    public Guid UploadedByUserId { get; set; }
    
    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    
    [Required, MaxLength(1000)]
    public string BlobPath { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Deleted
    
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int TotalEmails { get; set; }
    public int ProcessedEmails { get; set; }
    public int FailedEmails { get; set; }
    public long ProcessedBytes { get; set; }
    
    public bool KeepEmails { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTime? PurgeAfter { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Mailbox Mailbox { get; set; } = null!;
    public MailboxEncryptionState? EncryptionState { get; set; }
}

