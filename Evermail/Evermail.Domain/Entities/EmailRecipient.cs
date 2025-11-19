using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class EmailRecipient
{
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid EmailMessageId { get; set; }

    [Required, MaxLength(16)]
    public string RecipientType { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EmailMessage EmailMessage { get; set; } = null!;
}

