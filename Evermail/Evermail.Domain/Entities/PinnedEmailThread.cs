namespace Evermail.Domain.Entities;

public class PinnedEmailThread
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ConversationId { get; set; }

    public Guid? EmailMessageId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }

    public ApplicationUser? User { get; set; }

    public EmailThread? Conversation { get; set; }

    public EmailMessage? EmailMessage { get; set; }
}

