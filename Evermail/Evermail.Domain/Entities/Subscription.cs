using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    [Required]
    public Guid SubscriptionPlanId { get; set; }
    
    // Stripe Integration
    [Required, MaxLength(255)]
    public string StripeSubscriptionId { get; set; } = string.Empty;
    
    [Required, MaxLength(255)]
    public string StripeCustomerId { get; set; } = string.Empty;
    
    [Required, MaxLength(255)]
    public string StripePriceId { get; set; } = string.Empty;
    
    // Status
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Canceled, PastDue, Unpaid
    
    // Billing Period
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CanceledAt { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}

