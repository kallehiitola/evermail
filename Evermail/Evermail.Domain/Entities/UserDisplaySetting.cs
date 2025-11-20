using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class UserDisplaySetting
{
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [StringLength(32)]
    public string DateFormat { get; set; } = "MMM dd, yyyy";

    [StringLength(16)]
    public string ResultDensity { get; set; } = "Cozy";

    public bool AutoScrollToKeyword { get; set; } = true;

    public bool MatchNavigatorEnabled { get; set; } = true;

    public bool KeyboardShortcutsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public ApplicationUser? User { get; set; }
}

