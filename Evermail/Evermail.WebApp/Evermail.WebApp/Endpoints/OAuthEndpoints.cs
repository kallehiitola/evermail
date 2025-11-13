using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Evermail.WebApp.Endpoints;

public static class OAuthEndpoints
{
    public static RouteGroupBuilder MapOAuthEndpoints(this RouteGroupBuilder group)
    {
        // Google OAuth
        group.MapGet("/google/login", async (HttpContext context, string? returnUrl = null) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/v1/auth/google/callback?returnUrl={returnUrl ?? "/"}"
            };
            return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
        });

        group.MapGet("/google/callback", GoogleCallbackAsync);

        // Microsoft OAuth
        group.MapGet("/microsoft/login", async (HttpContext context, string? returnUrl = null) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/v1/auth/microsoft/callback?returnUrl={returnUrl ?? "/"}"
            };
            return Results.Challenge(properties, new[] { OpenIdConnectDefaults.AuthenticationScheme });
        });

        group.MapGet("/microsoft/callback", MicrosoftCallbackAsync);

        return group;
    }

    private static async Task<IResult> GoogleCallbackAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            return Results.Redirect("/login?error=oauth_failed");
        }

        // Extract user info from claims
        var email = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        var firstName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "";
        var lastName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Surname)?.Value ?? "";

        // TODO: Find or create user with this email
        // TODO: Generate JWT token
        // For now, redirect to home
        var returnUrl = context.Request.Query["returnUrl"].ToString() ?? "/";
        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> MicrosoftCallbackAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            return Results.Redirect("/login?error=oauth_failed");
        }

        // Extract user info
        var email = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        var firstName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "";
        var lastName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Surname)?.Value ?? "";

        // TODO: Find or create user with this email
        // TODO: Generate JWT token
        var returnUrl = context.Request.Query["returnUrl"].ToString() ?? "/";
        return Results.Redirect(returnUrl);
    }
}

