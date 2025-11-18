using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class MailboxDeletionQueue
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    [Required]
    public Guid MailboxId { get; set; }
    
    public Guid? MailboxUploadId { get; set; }
    
    public bool DeleteUpload { get; set; }
    public bool DeleteEmails { get; set; }
    
    [Required]
    public Guid RequestedByUserId { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime ExecuteAfter { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public Guid? ExecutedByUserId { get; set; }
    
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, Running, Completed, Failed
    
    public string? Notes { get; set; }
    
    // Navigation
    public Mailbox Mailbox { get; set; } = null!;
}

