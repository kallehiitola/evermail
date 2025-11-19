using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class EmailThread
{
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required, MaxLength(512)]
    public string ConversationKey { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? RootMessageId { get; set; }

    [MaxLength(1024)]
    public string? Subject { get; set; }

    public string ParticipantsSummary { get; set; } = "[]";

    public DateTime FirstMessageDate { get; set; }
    public DateTime LastMessageDate { get; set; }
    public int MessageCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<EmailMessage> Emails { get; set; } = new List<EmailMessage>();
}

