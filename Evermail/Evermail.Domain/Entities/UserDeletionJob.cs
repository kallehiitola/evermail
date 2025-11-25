using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class UserDeletionJob
{
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }

    [Required, MaxLength(64)]
    public Guid UserId { get; set; }

    [Required, MaxLength(64)]
    public Guid RequestedByUserId { get; set; }

    [Required, MaxLength(32)]
    public string Status { get; set; } = "Pending";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}


