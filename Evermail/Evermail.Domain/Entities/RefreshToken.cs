using System.ComponentModel.DataAnnotations;

namespace Evermail.Domain.Entities;

/// <summary>
/// Refresh token for JWT authentication.
/// Enables long-lived sessions without requiring user to re-authenticate.
/// Stored server-side for security and revocation capability.
/// </summary>
public class RefreshToken
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// The actual refresh token string (hashed for security)
    /// </summary>
    [Required, MaxLength(512)]
    public string TokenHash { get; set; } = string.Empty;
    
    /// <summary>
    /// JTI (JWT ID) of the access token this refresh token was issued with
    /// Used to prevent replay attacks
    /// </summary>
    [Required, MaxLength(64)]
    public string JwtId { get; set; } = string.Empty;
    
    /// <summary>
    /// When this refresh token expires (typically 30-90 days)
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// When this refresh token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this refresh token was last used to generate a new access token
    /// </summary>
    public DateTime? UsedAt { get; set; }
    
    /// <summary>
    /// When this refresh token was revoked (logout, password change, etc.)
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// Reason for revocation (e.g., "User logout", "Password changed", "Suspicious activity")
    /// </summary>
    [MaxLength(500)]
    public string? RevokeReason { get; set; }
    
    /// <summary>
    /// IP address where token was created (for security auditing)
    /// </summary>
    [MaxLength(45)]
    public string? CreatedByIp { get; set; }
    
    /// <summary>
    /// IP address where token was last used (for security auditing)
    /// </summary>
    [MaxLength(45)]
    public string? UsedByIp { get; set; }
    
    /// <summary>
    /// If this refresh token was replaced by a new one (token rotation)
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
    
    /// <summary>
    /// Check if this refresh token is currently valid
    /// </summary>
    public bool IsActive => RevokedAt == null && !IsExpired;
    
    /// <summary>
    /// Check if this refresh token has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    
    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
}

