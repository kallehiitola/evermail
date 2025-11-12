using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

public class UserRole
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required, MaxLength(50)]
    public string Role { get; set; } = "User"; // User, Admin, SuperAdmin
    
    // Navigation properties
    public User User { get; set; } = null!;
}

