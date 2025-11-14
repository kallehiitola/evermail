using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Evermail.WebApp.Services;

/// <summary>
/// Custom AuthenticationStateProvider for Blazor that reads JWT tokens from localStorage.
/// Provides authentication state based on JWT claims.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthenticationStateService _authStateService;

    public CustomAuthenticationStateProvider(IAuthenticationStateService authStateService)
    {
        _authStateService = authStateService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = await _authStateService.GetUserFromTokenAsync();
        
        if (principal == null)
        {
            // Anonymous user
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(anonymous);
        }

        return new AuthenticationState(principal);
    }

    /// <summary>
    /// Notifies the authentication state provider that the authentication state has changed.
    /// Call this after login/logout to update the UI.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

