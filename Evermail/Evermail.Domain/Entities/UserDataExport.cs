using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class UserDataExport
{
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }

    [Required, MaxLength(64)]
    public Guid UserId { get; set; }

    [Required, MaxLength(32)]
    public string Status { get; set; } = "Pending";

    [MaxLength(500)]
    public string? BlobPath { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
}


