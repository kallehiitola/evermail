using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Evermail.WebApp.Services;

public class AuthenticationStateService : IAuthenticationStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Infrastructure.Services.IJwtTokenService _jwtTokenService;
    private readonly HttpClient _httpClient;
    private const string TokenKey = "evermail_auth_token";
    private const string RefreshTokenKey = "evermail_refresh_token";

    public AuthenticationStateService(
        IJSRuntime jsRuntime,
        Infrastructure.Services.IJwtTokenService jwtTokenService,
        HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _jwtTokenService = jwtTokenService;
        _httpClient = httpClient;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        }
        catch (InvalidOperationException)
        {
            // JSRuntime not available during prerendering
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        }
        catch (InvalidOperationException)
        {
            // JSRuntime not available during prerendering
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string refreshToken)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
        }
        catch (InvalidOperationException)
        {
            // JSRuntime not available during prerendering
        }
    }

    public async Task SetTokenPairAsync(string accessToken, string refreshToken)
    {
        await SetTokenAsync(accessToken);
        await SetRefreshTokenAsync(refreshToken);
    }

    public async Task RemoveTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        }
        catch (InvalidOperationException)
        {
            // JSRuntime not available during prerendering
        }
    }

    public async Task<ClaimsPrincipal?> GetUserFromTokenAsync()
    {
        var token = await GetTokenAsync();
        
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var principal = _jwtTokenService.ValidateToken(token);
            
            // Ensure the identity is marked as authenticated
            if (principal != null && principal.Identity != null && !principal.Identity.IsAuthenticated)
            {
                // Create a new authenticated identity from the claims
                var claims = principal.Claims.ToList();
                var authenticatedIdentity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
                principal = new ClaimsPrincipal(authenticatedIdentity);
            }
            
            return principal;
        }
        catch (Exception ex)
        {
            // Token validation failed - log but don't throw
            // This allows the app to continue and show login page
            System.Diagnostics.Debug.WriteLine($"Token validation failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RefreshTokenIfNeededAsync()
    {
        var token = await GetTokenAsync();
        var refreshToken = await GetRefreshTokenAsync();

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        // Check if token is expired or will expire soon (within 2 minutes)
        var principal = _jwtTokenService.ValidateToken(token);
        if (principal != null)
        {
            var exp = principal.FindFirst("exp")?.Value;
            if (exp != null)
            {
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)).UtcDateTime;
                if (expiryTime > DateTime.UtcNow.AddMinutes(2))
                {
                    return false; // Token still valid for more than 2 minutes
                }
            }
        }

        // Token expired or expiring soon - refresh it
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/refresh", 
                new Common.DTOs.Auth.RefreshTokenRequest(refreshToken));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Common.DTOs.ApiResponse<Common.DTOs.Auth.AuthResponse>>();
                if (result?.Success == true && result.Data != null)
                {
                    await SetTokenPairAsync(result.Data.Token, result.Data.RefreshToken);
                    return true;
                }
            }
        }
        catch
        {
            // Refresh failed - user needs to re-authenticate
        }

        return false;
    }
}

