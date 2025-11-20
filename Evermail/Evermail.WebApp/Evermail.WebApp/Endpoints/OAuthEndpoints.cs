using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class OAuthEndpoints
{
    public static RouteGroupBuilder MapOAuthEndpoints(this RouteGroupBuilder group)
    {
        // Google OAuth
        group.MapGet("/google/login", (HttpContext context, string? returnUrl = null) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/v1/auth/google/callback?returnUrl={returnUrl ?? "/"}"
            };
            return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
        });

        group.MapGet("/google/callback", GoogleCallbackAsync);

        // Microsoft OAuth
        group.MapGet("/microsoft/login", (HttpContext context, string? returnUrl = null) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/v1/auth/microsoft/callback?returnUrl={returnUrl ?? "/"}"
            };
            return Results.Challenge(properties, new[] { MicrosoftAccountDefaults.AuthenticationScheme });
        });

        group.MapGet("/microsoft/callback", MicrosoftCallbackAsync);

        return group;
    }

    private static async Task<IResult> GoogleCallbackAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        EvermailDbContext dbContext,
        IJwtTokenService jwtService)
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

        if (string.IsNullOrEmpty(email))
        {
            return Results.Redirect("/login?error=no_email");
        }

        // Find or create user
        var user = await userManager.FindByEmailAsync(email);
        
        if (user == null)
        {
            try
            {
                // Create new tenant for this user
                var tenantName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrWhiteSpace(tenantName))
                {
                    tenantName = email.Split('@')[0]; // Use email prefix if no name
                }
                
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = tenantName,
                    Slug = GenerateSlug(tenantName, Guid.NewGuid().ToString("N")[..8]),
                    CreatedAt = DateTime.UtcNow
                };

                // Create new user
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true, // OAuth emails are pre-verified
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.Tenants.Add(tenant);
                
                // Create user without password (OAuth only)
                var createResult = await userManager.CreateAsync(user);
                
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    Console.WriteLine($"❌ Failed to create user: {errors}");
                    return Results.Redirect("/login?error=registration_failed");
                }

                // Assign default roles
                await userManager.AddToRoleAsync(user, "User");
                await userManager.AddToRoleAsync(user, "Admin");
                
                Console.WriteLine($"✅ New user registered via Google OAuth: {email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during OAuth registration: {ex.Message}");
                return Results.Redirect("/login?error=registration_error");
            }
        }
        else
        {
            Console.WriteLine($"✅ Existing user logged in via Google OAuth: {email}");
        }

        // Get user roles
        var roles = await userManager.GetRolesAsync(user);

        // Generate JWT token pair (access token + refresh token)
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var tokenPair = await jwtService.GenerateTokenPairAsync(user, roles, ipAddress);

        // Encode both tokens in URL (will be parsed by frontend)
        var returnUrl = context.Request.Query["returnUrl"].ToString() ?? "/";
        return Results.Redirect($"{returnUrl}?token={tokenPair.AccessToken}&refreshToken={tokenPair.RefreshToken}");
    }

    private static async Task<IResult> MicrosoftCallbackAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        EvermailDbContext dbContext,
        IJwtTokenService jwtService)
    {
        var result = await context.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            return Results.Redirect("/login?error=oauth_failed");
        }

        // Extract user info
        var email = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        var firstName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "";
        var lastName = result.Principal?.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Surname)?.Value ?? "";

        if (string.IsNullOrEmpty(email))
        {
            return Results.Redirect("/login?error=no_email");
        }

        // Find or create user (same logic as Google)
        var user = await userManager.FindByEmailAsync(email);
        
        if (user == null)
        {
            try
            {
                // Create new tenant for this user
                var tenantName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrWhiteSpace(tenantName))
                {
                    tenantName = email.Split('@')[0]; // Use email prefix if no name
                }
                
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = tenantName,
                    Slug = GenerateSlug(tenantName, Guid.NewGuid().ToString("N")[..8]),
                    CreatedAt = DateTime.UtcNow
                };

                // Create new user
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true, // OAuth emails are pre-verified
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.Tenants.Add(tenant);
                
                // Create user without password (OAuth only)
                var createResult = await userManager.CreateAsync(user);
                
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    Console.WriteLine($"❌ Failed to create user: {errors}");
                    return Results.Redirect("/login?error=registration_failed");
                }

                // Assign default roles
                await userManager.AddToRoleAsync(user, "User");
                await userManager.AddToRoleAsync(user, "Admin");
                
                Console.WriteLine($"✅ New user registered via Microsoft OAuth: {email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during OAuth registration: {ex.Message}");
                return Results.Redirect("/login?error=registration_error");
            }
        }
        else
        {
            Console.WriteLine($"✅ Existing user logged in via Microsoft OAuth: {email}");
        }

        // Get user roles
        var roles = await userManager.GetRolesAsync(user);

        // Generate JWT token pair (access token + refresh token)
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var tokenPair = await jwtService.GenerateTokenPairAsync(user, roles, ipAddress);

        // Encode both tokens in URL (will be parsed by frontend)
        var returnUrl = context.Request.Query["returnUrl"].ToString() ?? "/";
        return Results.Redirect($"{returnUrl}?token={tokenPair.AccessToken}&refreshToken={tokenPair.RefreshToken}");
    }

    private static string GenerateSlug(string name, string suffix)
    {
        // Convert to lowercase and replace spaces/special chars with hyphens
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
        
        // Remove any characters that aren't alphanumeric or hyphen
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        
        // Add suffix for uniqueness
        return $"{slug}-{suffix}";
    }
}

