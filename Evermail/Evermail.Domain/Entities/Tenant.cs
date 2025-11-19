using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(256)]
    public string Name { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Subscription
    [Required, MaxLength(50)]
    public string SubscriptionTier { get; set; } = "Free";
    
    [MaxLength(255)]
    public string? StripeCustomerId { get; set; }
    
    // Limits
    public int MaxStorageGB { get; set; } = 1;
    public int MaxUsers { get; set; } = 1;
    
    // Status
    public bool IsActive { get; set; } = true;
    
    [MaxLength(500)]
    public string? SuspensionReason { get; set; }
    
    // Navigation properties
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Mailbox> Mailboxes { get; set; } = new List<Mailbox>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<EmailThread> EmailThreads { get; set; } = new List<EmailThread>();
}

