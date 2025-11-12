using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    // Identity
    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    
    public bool EmailConfirmed { get; set; }
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public string? SecurityStamp { get; set; }
    
    // Profile
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    // 2FA
    public bool TwoFactorEnabled { get; set; }
    
    [MaxLength(255)]
    public string? TwoFactorSecret { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Mailbox> Mailboxes { get; set; } = new List<Mailbox>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

