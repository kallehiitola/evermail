using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Evermail.WebApp.Services;

/// <summary>
/// Service for managing authentication state with JWT tokens in localStorage.
/// Provides token storage, retrieval, and user claims management.
/// </summary>
public interface IAuthenticationStateService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
    Task<ClaimsPrincipal?> GetUserFromTokenAsync();
}

public class AuthenticationStateService : IAuthenticationStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Infrastructure.Services.IJwtTokenService _jwtTokenService;
    private const string TokenKey = "evermail_auth_token";

    public AuthenticationStateService(
        IJSRuntime jsRuntime,
        Infrastructure.Services.IJwtTokenService jwtTokenService)
    {
        _jsRuntime = jsRuntime;
        _jwtTokenService = jwtTokenService;
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

    public async Task RemoveTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
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

        var principal = _jwtTokenService.ValidateToken(token);
        return principal;
    }
}

