using Evermail.Common.DTOs;
using Evermail.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Evermail.WebApp.Endpoints;

/// <summary>
/// Development-only endpoints for testing and setup.
/// These should be disabled in production.
/// </summary>
public static class DevEndpoints
{
    public static RouteGroupBuilder MapDevEndpoints(this RouteGroupBuilder group)
    {
        // Only enable in Development environment
        group.MapPost("/add-admin", AddAdminRoleAsync);
        group.MapPost("/add-superadmin", AddSuperAdminRoleAsync);
        group.MapGet("/user-roles/{email}", GetUserRolesAsync);
        
        return group;
    }

    private static async Task<IResult> AddAdminRoleAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found. Please register first."
            ));
        }

        var result = await userManager.AddToRoleAsync(user, "Admin");
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"Failed to add Admin role: {errors}"
            ));
        }

        var roles = await userManager.GetRolesAsync(user);
        
        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new 
            { 
                Email = user.Email,
                Roles = roles,
                Message = $"✅ User '{email}' has been added to Admin role!"
            }
        ));
    }

    private static async Task<IResult> AddSuperAdminRoleAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found. Please register first."
            ));
        }

        // Add both Admin and SuperAdmin roles
        var adminResult = await userManager.AddToRoleAsync(user, "Admin");
        var superAdminResult = await userManager.AddToRoleAsync(user, "SuperAdmin");
        
        var roles = await userManager.GetRolesAsync(user);
        
        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new 
            { 
                Email = user.Email,
                Roles = roles,
                Message = $"✅ User '{email}' has been added to SuperAdmin role!"
            }
        ));
    }

    private static async Task<IResult> GetUserRolesAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found."
            ));
        }

        var roles = await userManager.GetRolesAsync(user);
        
        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new 
            { 
                Email = user.Email,
                UserId = user.Id,
                TenantId = user.TenantId,
                Roles = roles
            }
        ));
    }
}

