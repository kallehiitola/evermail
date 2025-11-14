using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    // Pricing
    public decimal PriceMonthly { get; set; }
    public decimal? PriceYearly { get; set; }
    
    [Required, MaxLength(3)]
    public string Currency { get; set; } = "EUR";
    
    // Stripe Integration
    [MaxLength(255)]
    public string? StripePriceIdMonthly { get; set; }
    
    [MaxLength(255)]
    public string? StripePriceIdYearly { get; set; }
    
    // Limits
    public int MaxStorageGB { get; set; }
    public int MaxUsers { get; set; }
    public int MaxMailboxes { get; set; }
    
    /// <summary>
    /// Maximum size of a single file upload in GB.
    /// Free: 1GB, Pro: 5GB, Team: 10GB, Enterprise: 100GB
    /// </summary>
    public int MaxFileSizeGB { get; set; } = 1;
    
    // Features (JSON)
    public string? Features { get; set; }
    
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

