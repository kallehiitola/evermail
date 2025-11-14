using System.Security.Claims;

namespace Evermail.WebApp.Services;

/// <summary>
/// Service for managing authentication state with JWT tokens in localStorage.
/// Provides token storage, retrieval, user claims management, and automatic token refresh.
/// </summary>
public interface IAuthenticationStateService
{
    /// <summary>
    /// Retrieves the JWT access token from browser localStorage.
    /// </summary>
    Task<string?> GetTokenAsync();
    
    /// <summary>
    /// Stores the JWT access token in browser localStorage.
    /// </summary>
    Task SetTokenAsync(string token);
    
    /// <summary>
    /// Retrieves the refresh token from browser localStorage.
    /// </summary>
    Task<string?> GetRefreshTokenAsync();
    
    /// <summary>
    /// Stores the refresh token in browser localStorage.
    /// </summary>
    Task SetRefreshTokenAsync(string refreshToken);
    
    /// <summary>
    /// Stores both access token and refresh token in browser localStorage.
    /// </summary>
    Task SetTokenPairAsync(string accessToken, string refreshToken);
    
    /// <summary>
    /// Removes both tokens from browser localStorage (logout).
    /// </summary>
    Task RemoveTokenAsync();
    
    /// <summary>
    /// Validates the JWT token and returns the ClaimsPrincipal for the current user.
    /// </summary>
    Task<ClaimsPrincipal?> GetUserFromTokenAsync();
    
    /// <summary>
    /// Checks if the access token is expiring soon (within 2 minutes) and automatically refreshes it.
    /// Returns true if token was refreshed, false otherwise.
    /// </summary>
    Task<bool> RefreshTokenIfNeededAsync();
}

