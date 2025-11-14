using Evermail.Domain.Entities;
using System.Security.Claims;

namespace Evermail.Infrastructure.Services;

/// <summary>
/// Record representing an access token and refresh token pair.
/// </summary>
public record TokenPair(
    string AccessToken, 
    string RefreshToken, 
    Guid RefreshTokenId,
    DateTime AccessTokenExpires, 
    DateTime RefreshTokenExpires
);

/// <summary>
/// Service for generating and validating JWT tokens with refresh token support.
/// Handles token generation, validation, rotation, and revocation.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with their roles.
    /// </summary>
    Task<string> GenerateTokenAsync(ApplicationUser user, IList<string> roles);
    
    /// <summary>
    /// Generates both an access token and a refresh token for the user.
    /// Stores the refresh token in the database with security features (hashing, IP tracking).
    /// </summary>
    Task<TokenPair> GenerateTokenPairAsync(ApplicationUser user, IList<string> roles, string? ipAddress = null);
    
    /// <summary>
    /// Validates and refreshes an expired or expiring access token using a refresh token.
    /// Implements token rotation: old refresh token is revoked, new one issued.
    /// </summary>
    Task<TokenPair?> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
    
    /// <summary>
    /// Revokes a specific refresh token (e.g., on logout).
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken, string reason, string? ipAddress = null);
    
    /// <summary>
    /// Revokes all refresh tokens for a user (e.g., on password change or security incident).
    /// </summary>
    Task RevokeAllUserTokensAsync(Guid userId, string reason);
    
    /// <summary>
    /// Validates a JWT token and returns the ClaimsPrincipal if valid.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}

