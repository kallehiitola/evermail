using Evermail.Common.DTOs;
using Evermail.Common.DTOs.User;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.WebApp.Services.Audit;
using Evermail.WebApp.Services.Gdpr;
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

        group.MapPost("/me/export", RequestDataExportAsync)
            .RequireAuthorization();

        group.MapGet("/me/exports/{exportId:guid}", GetDataExportStatusAsync)
            .RequireAuthorization();

        group.MapGet("/me/exports/{exportId:guid}/download", GetDataExportDownloadAsync)
            .RequireAuthorization();

        group.MapDelete("/me", DeleteAccountAsync)
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

    private static async Task<IResult> RequestDataExportAsync(
        IGdprExportService exportService,
        IAuditLogger auditLogger,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var export = await exportService.CreateExportAsync(tenantContext.TenantId, tenantContext.UserId, cancellationToken);
        var downloadUrl = await exportService.TryCreateDownloadUrlAsync(export, cancellationToken);

        await auditLogger.LogAsync(
            action: "UserDataExportRequested",
            resourceType: "UserDataExport",
            resourceId: export.Id,
            details: $"status:{export.Status}");

        var dto = MapExport(export, downloadUrl);
        return Results.Accepted($"/api/v1/users/me/exports/{export.Id}", new ApiResponse<UserDataExportDto>(true, dto));
    }

    private static async Task<IResult> GetDataExportStatusAsync(
        Guid exportId,
        IGdprExportService exportService,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var export = await exportService.GetExportAsync(exportId, tenantContext.TenantId, tenantContext.UserId, cancellationToken);
        if (export is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Export not found."));
        }

        var downloadUrl = export.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            ? await exportService.TryCreateDownloadUrlAsync(export, cancellationToken)
            : null;

        return Results.Ok(new ApiResponse<UserDataExportDto>(true, MapExport(export, downloadUrl)));
    }

    private static async Task<IResult> GetDataExportDownloadAsync(
        Guid exportId,
        IGdprExportService exportService,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var export = await exportService.GetExportAsync(exportId, tenantContext.TenantId, tenantContext.UserId, cancellationToken);
        if (export is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Export not found."));
        }

        if (!string.Equals(export.Status, "Completed", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(export.BlobPath))
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, "Export is not ready yet."));
        }

        var downloadUrl = await exportService.TryCreateDownloadUrlAsync(export, cancellationToken);
        if (downloadUrl is null)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, "Unable to generate download URL."));
        }

        var dto = new UserDataExportDownloadDto(downloadUrl, DateTime.UtcNow.AddMinutes(15));
        return Results.Ok(new ApiResponse<UserDataExportDownloadDto>(true, dto));
    }

    private static async Task<IResult> DeleteAccountAsync(
        UserManager<ApplicationUser> userManager,
        EvermailDbContext context,
        TenantContext tenantContext,
        IQueueService queueService,
        IAuditLogger auditLogger,
        IJwtTokenService jwtTokenService,
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

        var deletionJob = new UserDeletionJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            RequestedByUserId = tenantContext.UserId,
            RequestedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        context.UserDeletionJobs.Add(deletionJob);

        var mailboxes = await context.Mailboxes
            .Where(m => m.TenantId == tenantContext.TenantId)
            .ToListAsync(cancellationToken);

        var deletionQueueJobs = new List<MailboxDeletionQueue>();

        foreach (var mailbox in mailboxes)
        {
            mailbox.IsPendingDeletion = true;
            mailbox.PurgeAfter = DateTime.UtcNow;

            var deletion = new MailboxDeletionQueue
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                MailboxId = mailbox.Id,
                DeleteUpload = true,
                DeleteEmails = true,
                RequestedByUserId = tenantContext.UserId,
                RequestedAt = DateTime.UtcNow,
                ExecuteAfter = DateTime.UtcNow,
                Status = "Scheduled"
            };

            context.MailboxDeletionQueue.Add(deletion);
            deletionQueueJobs.Add(deletion);
        }

        var settings = await context.UserDisplaySettings
            .Where(s => s.UserId == tenantContext.UserId)
            .ToListAsync(cancellationToken);
        context.UserDisplaySettings.RemoveRange(settings);

        var filters = await context.SavedSearchFilters
            .Where(f => f.UserId == tenantContext.UserId)
            .ToListAsync(cancellationToken);
        context.SavedSearchFilters.RemoveRange(filters);

        var pins = await context.PinnedEmailThreads
            .Where(p => p.UserId == tenantContext.UserId)
            .ToListAsync(cancellationToken);
        context.PinnedEmailThreads.RemoveRange(pins);

        var anonymizedEmail = $"{user.Id:N}@deleted.evermail.local";
        user.IsActive = false;
        user.Email = anonymizedEmail;
        user.NormalizedEmail = anonymizedEmail.ToUpperInvariant();
        user.UserName = anonymizedEmail;
        user.NormalizedUserName = user.NormalizedEmail;
        user.FirstName = "Deleted";
        user.LastName = "User";
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.TwoFactorEnabled = false;

        await userManager.UpdateAsync(user);
        await jwtTokenService.RevokeAllUserTokensAsync(user.Id, "GDPR delete request");

        await context.SaveChangesAsync(cancellationToken);

        foreach (var job in deletionQueueJobs)
        {
            await queueService.EnqueueMailboxDeletionAsync(job.Id, job.MailboxId);
        }

        await context.AuditLogs
            .Where(a => a.TenantId == tenantContext.TenantId && a.UserId == tenantContext.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.UserId, a => null)
                .SetProperty(a => a.Details, a => (a.Details ?? string.Empty) + " [user-anonymized]"),
                cancellationToken);

        deletionJob.Status = "Completed";
        deletionJob.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            action: "UserDeletionRequested",
            resourceType: "UserDeletionJob",
            resourceId: deletionJob.Id,
            details: $"status:{deletionJob.Status}",
            cancellationToken: cancellationToken);

        var response = new UserDeletionResponse(
            deletionJob.Id,
            deletionJob.Status,
            deletionJob.RequestedAt,
            deletionJob.CompletedAt);

        return Results.Accepted($"/api/v1/users/me/deletion/{deletionJob.Id}", new ApiResponse<UserDeletionResponse>(true, response));
    }

    private static UserDataExportDto MapExport(UserDataExport export, string? downloadUrl) =>
        new(
            export.Id,
            export.Status,
            export.RequestedAt,
            export.CompletedAt,
            export.ExpiresAt,
            export.FileSizeBytes,
            downloadUrl,
            export.ErrorMessage);
}

