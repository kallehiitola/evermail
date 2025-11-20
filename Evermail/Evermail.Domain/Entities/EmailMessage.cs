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

    [MaxLength(512)]
    public string? ReplyToAddress { get; set; }

    [MaxLength(512)]
    public string? SenderAddress { get; set; }

    [MaxLength(512)]
    public string? SenderName { get; set; }

    [MaxLength(512)]
    public string? ReturnPath { get; set; }

    [MaxLength(512)]
    public string? ListId { get; set; }

    [MaxLength(1024)]
    public string? ThreadTopic { get; set; }

    [MaxLength(32)]
    public string? Importance { get; set; }

    [MaxLength(32)]
    public string? Priority { get; set; }

    [MaxLength(512)]
    public string? Categories { get; set; }
    
    // Recipients
    public string? ToAddresses { get; set; }
    public string? ToNames { get; set; }
    public string? CcAddresses { get; set; }
    public string? CcNames { get; set; }
    public string? BccAddresses { get; set; }
    public string? BccNames { get; set; }

    [MaxLength(2000)]
    public string? RecipientsSearch { get; set; }
    
    public string SearchVector { get; set; } = string.Empty;
    
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

    // Threads
    public Guid? ConversationId { get; set; }

    [MaxLength(512)]
    public string? ConversationKey { get; set; }

    public int ThreadDepth { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Mailbox Mailbox { get; set; } = null!;
    public MailboxUpload? MailboxUpload { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public EmailThread? Thread { get; set; }
    public ICollection<EmailRecipient> Recipients { get; set; } = new List<EmailRecipient>();
}

