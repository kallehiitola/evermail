using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    [Required, MaxLength(64)]
    public Guid TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    // 2FA - Additional fields (IdentityUser already has TwoFactorEnabled)
    [MaxLength(255)]
    public string? TwoFactorSecret { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Mailbox> Mailboxes { get; set; } = new List<Mailbox>();
}

