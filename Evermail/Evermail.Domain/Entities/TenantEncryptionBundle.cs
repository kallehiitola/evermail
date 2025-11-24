using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class TenantEncryptionBundle
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CreatedByUserId { get; set; }

    [MaxLength(150)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Version { get; set; } = "offline-byok/v1";

    [Required]
    public string WrappedDek { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Salt { get; set; } = string.Empty;

    [Required, MaxLength(48)]
    public string Nonce { get; set; } = string.Empty;

    [Required, MaxLength(88)]
    public string Checksum { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

