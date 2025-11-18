using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Evermail.WebApp.Services;

/// <summary>
/// Custom AuthenticationStateProvider for Blazor that reads JWT tokens from localStorage.
/// Provides authentication state based on JWT claims.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthenticationStateService _authStateService;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;

    public CustomAuthenticationStateProvider(
        IAuthenticationStateService authStateService,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _authStateService = authStateService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _logger.LogWarning("🔵 CustomAuthenticationStateProvider.GetAuthenticationStateAsync - Called");
        
        try
        {
            var principal = await _authStateService.GetUserFromTokenAsync();
            
            if (principal == null)
            {
                _logger.LogWarning("🔵 CustomAuthenticationStateProvider - Principal is null (no token or validation failed)");
                var anonymousIdentity = new ClaimsIdentity();
                var anonymous = new ClaimsPrincipal(anonymousIdentity);
                return new AuthenticationState(anonymous);
            }
            
            if (principal.Identity == null)
            {
                _logger.LogWarning("🔵 CustomAuthenticationStateProvider - Principal.Identity is null");
                var anonymousIdentity = new ClaimsIdentity();
                var anonymous = new ClaimsPrincipal(anonymousIdentity);
                return new AuthenticationState(anonymous);
            }
            
            if (!principal.Identity.IsAuthenticated)
            {
                _logger.LogWarning("🔵 CustomAuthenticationStateProvider - Identity.IsAuthenticated is false. Claims: {Claims}", 
                    string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
                var anonymousIdentity = new ClaimsIdentity();
                var anonymous = new ClaimsPrincipal(anonymousIdentity);
                return new AuthenticationState(anonymous);
            }

            _logger.LogWarning("🔵 CustomAuthenticationStateProvider - User IS authenticated: {UserName}, Claims: {ClaimCount}", 
                principal.Identity.Name ?? "null", principal.Claims.Count());
            return new AuthenticationState(principal);
        }
        catch (InvalidOperationException)
        {
            // JSRuntime not available during SSR - return anonymous state
            // Client-side hydration will re-check authentication
            _logger.LogWarning("🔵 CustomAuthenticationStateProvider - JSRuntime not available (SSR), returning anonymous");
            var anonymousIdentity = new ClaimsIdentity();
            var anonymous = new ClaimsPrincipal(anonymousIdentity);
            return new AuthenticationState(anonymous);
        }
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

