using Evermail.Common.DTOs;
using Evermail.Common.DTOs.User;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class UserEndpoints
{
    private static readonly HashSet<string> AllowedDateFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "MMM dd, yyyy",
        "dd.MM.yyyy"
    };

    private static readonly HashSet<string> AllowedDensities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cozy",
        "Compact"
    };

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/profile", GetProfileAsync)
            .RequireAuthorization();

        group.MapGet("/me/settings/display", GetDisplaySettingsAsync)
            .RequireAuthorization();

        group.MapPut("/me/settings/display", UpdateDisplaySettingsAsync)
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetProfileAsync(
        UserManager<ApplicationUser> userManager,
        EvermailDbContext context,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var user = await userManager.FindByIdAsync(tenantContext.UserId.ToString());
        if (user is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "User not found."));
        }

        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Tenant not found."));
        }

        var roles = await userManager.GetRolesAsync(user);

        var storageBytes = await context.Mailboxes
            .AsNoTracking()
            .Where(m => m.TenantId == tenantContext.TenantId)
            .SumAsync(m => (long?)(m.NormalizedSizeBytes > 0 ? m.NormalizedSizeBytes : m.FileSizeBytes) ?? 0, cancellationToken);

        var mailboxCount = await context.Mailboxes
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantContext.TenantId, cancellationToken);

        var dto = new UserProfileDto(
            user.Id,
            tenant.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.TwoFactorEnabled,
            user.CreatedAt,
            user.LastLoginAt,
            tenant.Name,
            tenant.SubscriptionTier,
            tenant.MaxStorageGB,
            tenant.MaxUsers,
            storageBytes,
            mailboxCount,
            roles.ToArray());

        return Results.Ok(new ApiResponse<UserProfileDto>(true, dto));
    }

    private static async Task<IResult> GetDisplaySettingsAsync(
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var settings = await GetOrCreateSettingsAsync(context, tenantContext);

        return Results.Ok(new ApiResponse<UserDisplaySettingsDto>(
            Success: true,
            Data: MapToDto(settings)));
    }

    private static async Task<IResult> UpdateDisplaySettingsAsync(
        UpdateUserDisplaySettingsRequest request,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var settings = await GetOrCreateSettingsAsync(context, tenantContext);

        if (!string.IsNullOrWhiteSpace(request?.DateFormat) &&
            AllowedDateFormats.Contains(request.DateFormat))
        {
            settings.DateFormat = request.DateFormat;
        }

        if (!string.IsNullOrWhiteSpace(request?.ResultDensity) &&
            AllowedDensities.Contains(request.ResultDensity))
        {
            settings.ResultDensity = request.ResultDensity;
        }

        if (request?.AutoScrollToKeyword is not null)
        {
            settings.AutoScrollToKeyword = request.AutoScrollToKeyword.Value;
        }

        if (request?.MatchNavigatorEnabled is not null)
        {
            settings.MatchNavigatorEnabled = request.MatchNavigatorEnabled.Value;
        }

        if (request?.KeyboardShortcutsEnabled is not null)
        {
            settings.KeyboardShortcutsEnabled = request.KeyboardShortcutsEnabled.Value;
        }

        settings.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return Results.Ok(new ApiResponse<UserDisplaySettingsDto>(
            Success: true,
            Data: MapToDto(settings)));
    }

    private static async Task<UserDisplaySetting> GetOrCreateSettingsAsync(
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        var settings = await context.UserDisplaySettings
            .FirstOrDefaultAsync(s => s.UserId == tenantContext.UserId);

        if (settings != null)
        {
            return settings;
        }

        settings = new UserDisplaySetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            DateFormat = "MMM dd, yyyy",
            ResultDensity = "Cozy",
            AutoScrollToKeyword = true,
            MatchNavigatorEnabled = true,
            KeyboardShortcutsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        context.UserDisplaySettings.Add(settings);
        await context.SaveChangesAsync();

        return settings;
    }

    private static UserDisplaySettingsDto MapToDto(UserDisplaySetting settings) =>
        new(
            settings.DateFormat,
            settings.ResultDensity,
            settings.AutoScrollToKeyword,
            settings.MatchNavigatorEnabled,
            settings.KeyboardShortcutsEnabled);

    private static bool TryEnsureTenant(TenantContext tenantContext, out IResult? errorResult)
    {
        if (tenantContext.TenantId == Guid.Empty || tenantContext.UserId == Guid.Empty)
        {
            errorResult = Results.Unauthorized();
            return false;
        }

        errorResult = null;
        return true;
    }
}

