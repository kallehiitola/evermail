using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Mailbox;
using Evermail.Common.DTOs.Upload;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Evermail.WebApp.Endpoints;

public static class MailboxEndpoints
{
    public static RouteGroupBuilder MapMailboxEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetMailboxesAsync)
            .RequireAuthorization();
        
        group.MapGet("/{id:guid}", GetMailboxByIdAsync)
            .RequireAuthorization();
        
        group.MapGet("/{id:guid}/uploads", GetMailboxUploadsAsync)
            .RequireAuthorization();
        
        group.MapPost("/{id:guid}/uploads", InitiateMailboxReimportAsync)
            .RequireAuthorization();
        
        group.MapPatch("/{id:guid}", RenameMailboxAsync)
            .RequireAuthorization();
        
        group.MapPost("/{id:guid}/delete", ScheduleMailboxDeletionAsync)
            .RequireAuthorization();
        
        group.MapDelete("/{id:guid}", PurgeMailboxAsync)
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
        
        return group;
    }

    private static async Task<IResult> GetMailboxesAsync(
        EvermailDbContext context,
        TenantContext tenantContext,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Build query
        var query = context.Mailboxes
            .Where(m => m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId && m.SoftDeletedAt == null);

        // Filter by status if provided
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(m => m.Status == status);
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination
        var mailboxes = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MailboxDto(
                m.Id,
                m.DisplayName,
                m.FileName,
                m.FileSizeBytes,
                m.NormalizedSizeBytes,
                m.Status,
                m.UploadRemovedAt != null,
                m.IsPendingDeletion,
                m.TotalEmails,
                m.ProcessedEmails,
                m.FailedEmails,
                m.ProcessedBytes,
                m.CreatedAt,
                m.ProcessingStartedAt,
                m.ProcessingCompletedAt,
                m.ErrorMessage,
                m.Uploads
                    .Where(u => u.Id == m.LatestUploadId)
                    .Select(u => new MailboxUploadSummaryDto(
                        u.Id,
                        u.FileName,
                        u.FileSizeBytes,
                        u.NormalizedSizeBytes,
                        u.Status,
                        u.KeepEmails,
                        u.CreatedAt,
                        u.ProcessingStartedAt,
                        u.ProcessingCompletedAt,
                        u.DeletedAt
                    ))
                    .FirstOrDefault()
            ))
            .ToListAsync();

        return Results.Ok(new ApiResponse<PagedMailboxesResponse>(
            Success: true,
            Data: new PagedMailboxesResponse(
                mailboxes,
                totalCount,
                page,
                pageSize
            )
        ));
    }

    private static async Task<IResult> GetMailboxByIdAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var mailbox = await context.Mailboxes
            .Where(m => m.Id == id && m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId && m.SoftDeletedAt == null)
            .Select(m => new MailboxDto(
                m.Id,
                m.DisplayName,
                m.FileName,
                m.FileSizeBytes,
                m.NormalizedSizeBytes,
                m.Status,
                m.UploadRemovedAt != null,
                m.IsPendingDeletion,
                m.TotalEmails,
                m.ProcessedEmails,
                m.FailedEmails,
                m.ProcessedBytes,
                m.CreatedAt,
                m.ProcessingStartedAt,
                m.ProcessingCompletedAt,
                m.ErrorMessage,
                m.Uploads
                    .Where(u => u.Id == m.LatestUploadId)
                    .Select(u => new MailboxUploadSummaryDto(
                        u.Id,
                        u.FileName,
                        u.FileSizeBytes,
                        u.NormalizedSizeBytes,
                        u.Status,
                        u.KeepEmails,
                        u.CreatedAt,
                        u.ProcessingStartedAt,
                        u.ProcessingCompletedAt,
                        u.DeletedAt
                    ))
                    .FirstOrDefault()
            ))
            .FirstOrDefaultAsync();

        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found"
            ));
        }

        return Results.Ok(new ApiResponse<MailboxDto>(
            Success: true,
            Data: mailbox
        ));
    }
    private static Task<IResult> InitiateMailboxReimportAsync(
        Guid id,
        InitiateUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
        => UploadEndpoints.InitiateUploadInternalAsync(request with { MailboxId = id }, id, blobService, context, tenantContext);

    private static async Task<IResult> GetMailboxUploadsAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var uploads = await context.MailboxUploads
            .Where(u => u.MailboxId == id && u.TenantId == tenantContext.TenantId)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new MailboxUploadDto(
                u.Id,
                u.FileName,
                u.FileSizeBytes,
                u.NormalizedSizeBytes,
                u.Status,
                u.KeepEmails,
                u.ErrorMessage,
                u.CreatedAt,
                u.ProcessingStartedAt,
                u.ProcessingCompletedAt,
                u.DeletedAt
            ))
            .ToListAsync();

        return Results.Ok(new ApiResponse<MailboxUploadsResponse>(
            Success: true,
            Data: new MailboxUploadsResponse(uploads)
        ));
    }

    private static async Task<IResult> RenameMailboxAsync(
        Guid id,
        RenameMailboxRequest request,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Display name is required"
            ));
        }

        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId);

        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found"
            ));
        }

        mailbox.DisplayName = request.DisplayName.Trim();
        mailbox.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { mailbox.Id, mailbox.DisplayName }
        ));
    }

    private static async Task<IResult> ScheduleMailboxDeletionAsync(
        Guid id,
        DeleteMailboxRequest request,
        EvermailDbContext context,
        TenantContext tenantContext,
        ClaimsPrincipal user,
        IQueueService queueService)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (!request.DeleteUpload && !request.DeleteEmails)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "At least one of deleteUpload or deleteEmails must be true"
            ));
        }

        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId);

        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found"
            ));
        }

        MailboxUpload? targetUpload = null;
        if (request.UploadId.HasValue)
        {
            targetUpload = await context.MailboxUploads
                .FirstOrDefaultAsync(u => u.Id == request.UploadId.Value && u.MailboxId == mailbox.Id && u.TenantId == tenantContext.TenantId);

            if (targetUpload == null)
            {
                return Results.NotFound(new ApiResponse<object>(
                    Success: false,
                    Error: "Upload not found"
                ));
            }
        }

        var isSuperAdmin = user.IsInRole("SuperAdmin");
        var executeAfter = (request.PurgeNow && isSuperAdmin)
            ? DateTime.UtcNow
            : DateTime.UtcNow.AddDays(30);

        var job = new MailboxDeletionQueue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            MailboxId = mailbox.Id,
            MailboxUploadId = targetUpload?.Id,
            DeleteUpload = request.DeleteUpload,
            DeleteEmails = request.DeleteEmails,
            RequestedByUserId = tenantContext.UserId,
            RequestedAt = DateTime.UtcNow,
            ExecuteAfter = executeAfter,
            Status = "Scheduled"
        };

        context.MailboxDeletionQueue.Add(job);
        mailbox.IsPendingDeletion = true;
        mailbox.PurgeAfter = executeAfter;
        await context.SaveChangesAsync();

        await queueService.EnqueueMailboxDeletionAsync(job.Id, mailbox.Id);

        return Results.Accepted($"/api/v1/mailboxes/{mailbox.Id}/delete/{job.Id}", new ApiResponse<DeleteMailboxResponse>(
            Success: true,
            Data: new DeleteMailboxResponse(job.Id, executeAfter, job.Status)
        ));
    }

    private static async Task<IResult> PurgeMailboxAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext,
        ClaimsPrincipal user,
        IQueueService queueService)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (!user.IsInRole("SuperAdmin"))
        {
            return Results.Forbid();
        }

        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantContext.TenantId);

        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found"
            ));
        }

        var job = new MailboxDeletionQueue
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

        context.MailboxDeletionQueue.Add(job);
        mailbox.IsPendingDeletion = true;
        mailbox.PurgeAfter = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await queueService.EnqueueMailboxDeletionAsync(job.Id, mailbox.Id);

        return Results.Accepted($"/api/v1/mailboxes/{mailbox.Id}/delete/{job.Id}", new ApiResponse<DeleteMailboxResponse>(
            Success: true,
            Data: new DeleteMailboxResponse(job.Id, job.ExecuteAfter, job.Status)
        ));
    }
}

