using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    public Guid? UserId { get; set; }
    
    // Action
    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? ResourceType { get; set; }
    
    public Guid? ResourceId { get; set; }
    
    // Context
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    public string? Details { get; set; }
    
    // Timestamp
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User? User { get; set; }
}

