using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class EmailMessage
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    [Required, MaxLength(64)]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid MailboxId { get; set; }
    public Guid? MailboxUploadId { get; set; }
    
    // Email Headers
    [MaxLength(512)]
    public string? MessageId { get; set; }
    
    [MaxLength(512)]
    public string? InReplyTo { get; set; }
    
    public string? References { get; set; }
    
    [MaxLength(1024)]
    public string? Subject { get; set; }
    
    // Sender
    [Required, MaxLength(512)]
    public string FromAddress { get; set; } = string.Empty;
    
    [MaxLength(512)]
    public string? FromName { get; set; }
    
    // Recipients
    public string? ToAddresses { get; set; }
    public string? ToNames { get; set; }
    public string? CcAddresses { get; set; }
    public string? CcNames { get; set; }
    public string? BccAddresses { get; set; }
    public string? BccNames { get; set; }
    
    // Date
    public DateTime Date { get; set; }
    
    // Content
    [MaxLength(512)]
    public string? Snippet { get; set; }
    
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    
    // Metadata
    public bool HasAttachments { get; set; }
    public int AttachmentCount { get; set; }
    public bool IsRead { get; set; }
    public byte[]? ContentHash { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Mailbox Mailbox { get; set; } = null!;
    public MailboxUpload? MailboxUpload { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

