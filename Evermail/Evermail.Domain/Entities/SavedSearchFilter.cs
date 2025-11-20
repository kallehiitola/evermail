using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class SavedSearchFilter
{
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string DefinitionJson { get; set; } = "{}";

    public int OrderIndex { get; set; }

    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public ApplicationUser? User { get; set; }
}

