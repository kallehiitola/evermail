using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class ZeroAccessMailboxToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid MailboxId { get; set; }

    [Required, MaxLength(50)]
    public string TokenType { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string TokenValue { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Mailbox Mailbox { get; set; } = null!;
}

