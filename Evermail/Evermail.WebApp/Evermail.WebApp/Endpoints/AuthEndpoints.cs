using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Auth;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/enable-2fa", Enable2FAAsync).RequireAuthorization();
        group.MapPost("/verify-2fa", Verify2FAAsync).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        EmailDbContext context,
        IJwtTokenService jwtService)
    {
        // Create tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.TenantName,
            Slug = GenerateSlug(request.TenantName),
            SubscriptionTier = "Free",
            MaxStorageGB = 1,
            MaxUsers = 1
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        // Create user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            context.Tenants.Remove(tenant);
            await context.SaveChangesAsync();

            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Registration failed",
                ValidationErrors: result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())
            ));
        }

        // Add to User role
        await userManager.AddToRoleAsync(user, "User");

        // Generate token
        var roles = await userManager.GetRolesAsync(user);
        var token = await jwtService.GenerateTokenAsync(user, roles);

        return Results.Ok(new ApiResponse<AuthResponse>(
            Success: true,
            Data: new AuthResponse(
                Token: token,
                ExpiresAt: DateTime.UtcNow.AddMinutes(15),
                User: new UserDto(user.Id, user.TenantId, user.Email!, user.FirstName, user.LastName, user.TwoFactorEnabled)
            )
        ));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtService)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        
        if (user == null)
        {
            return Results.Unauthorized();
        }

        // Check password
        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await userManager.AccessFailedAsync(user);
            return Results.Unauthorized();
        }

        // Check if 2FA is required
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(request.TwoFactorCode))
            {
                return Results.BadRequest(new ApiResponse<object>(
                    Success: false,
                    Error: "Two-factor authentication code required"
                ));
            }

            // Validate 2FA code (implement with ITwoFactorService)
            // For now, skip 2FA validation in this checkpoint
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Generate token
        var roles = await userManager.GetRolesAsync(user);
        var token = await jwtService.GenerateTokenAsync(user, roles);

        return Results.Ok(new ApiResponse<AuthResponse>(
            Success: true,
            Data: new AuthResponse(
                Token: token,
                ExpiresAt: DateTime.UtcNow.AddMinutes(15),
                User: new UserDto(user.Id, user.TenantId, user.Email!, user.FirstName, user.LastName, user.TwoFactorEnabled)
            )
        ));
    }

    private static async Task<IResult> Enable2FAAsync(
        UserManager<ApplicationUser> userManager,
        ITwoFactorService twoFactorService,
        HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (userId == null) return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return Results.NotFound();

        var secret = twoFactorService.GenerateSecret();
        user.TwoFactorSecret = secret;
        user.TwoFactorEnabled = false; // Enable after verification

        await userManager.UpdateAsync(user);

        var qrCodeUrl = twoFactorService.GenerateQrCodeUrl(user.Email!, secret);

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { QrCodeUrl = qrCodeUrl, Secret = secret }
        ));
    }

    private static async Task<IResult> Verify2FAAsync(
        UserManager<ApplicationUser> userManager,
        ITwoFactorService twoFactorService,
        HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        var code = httpContext.Request.Form["code"].ToString();
        
        if (userId == null) return Results.Unauthorized();
        if (string.IsNullOrEmpty(code)) return Results.BadRequest("Code is required");

        var user = await userManager.FindByIdAsync(userId);
        if (user == null || user.TwoFactorSecret == null) return Results.NotFound();

        var isValid = twoFactorService.ValidateCode(user.TwoFactorSecret, code);
        
        if (!isValid)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Invalid code"
            ));
        }

        user.TwoFactorEnabled = true;
        await userManager.UpdateAsync(user);

        var backupCodes = twoFactorService.GenerateBackupCodes();

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { TwoFactorEnabled = true, BackupCodes = backupCodes }
        ));
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Trim();
    }
}

