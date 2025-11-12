using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    [Required]
    public Guid EmailMessageId { get; set; }
    
    // File Info
    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required, MaxLength(255)]
    public string ContentType { get; set; } = string.Empty;
    
    public long SizeBytes { get; set; }
    
    [Required, MaxLength(1000)]
    public string BlobPath { get; set; } = string.Empty;
    
    // Metadata
    public bool IsInline { get; set; }
    
    [MaxLength(255)]
    public string? ContentId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public EmailMessage EmailMessage { get; set; } = null!;
}

