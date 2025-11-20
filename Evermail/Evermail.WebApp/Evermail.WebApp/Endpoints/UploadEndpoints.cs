using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Upload;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class UploadEndpoints
{
    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/initiate", InitiateUploadAsync)
            .RequireAuthorization();
        
        group.MapPost("/complete", CompleteUploadAsync)
            .RequireAuthorization();
        
        return group;
    }

    internal static async Task<IResult> InitiateUploadInternalAsync(
        InitiateUploadRequest request,
        Guid? overrideMailboxId,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        var mailboxIdOverride = overrideMailboxId ?? request.MailboxId;
        
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // 1. Get tenant's subscription plan
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);
        
        if (tenant == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Tenant not found"
            ));
        }

        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name == tenant.SubscriptionTier);
        
        if (plan == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Subscription plan not found"
            ));
        }
        
        // 2. Validate file size against plan
        var fileSizeGB = request.FileSizeBytes / (1024.0 * 1024.0 * 1024.0);
        if (fileSizeGB > plan.MaxFileSizeGB)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"File size ({fileSizeGB:F2} GB) exceeds your plan limit ({plan.MaxFileSizeGB} GB). Please upgrade your subscription."
            ));
        }
        
        // 3. Validate file type
        var allowedFileTypes = new[] { "mbox", "google-takeout-zip", "microsoft-export-zip" };
        if (!allowedFileTypes.Contains(request.FileType.ToLowerInvariant()))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"Invalid file type. Allowed types: {string.Join(", ", allowedFileTypes)}"
            ));
        }
        
        // Ensure user exists
        var userExists = await context.Users.AnyAsync(u => u.Id == tenantContext.UserId);
        if (!userExists)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User not found. UserId: {tenantContext.UserId}"
            ));
        }

        Mailbox? mailboxEntity;
        if (mailboxIdOverride is Guid targetMailboxId)
        {
            mailboxEntity = await context.Mailboxes
                .FirstOrDefaultAsync(m => m.Id == targetMailboxId && m.TenantId == tenantContext.TenantId);

            if (mailboxEntity == null)
            {
                return Results.NotFound(new ApiResponse<object>(
                    Success: false,
                    Error: "Mailbox not found"
                ));
            }
        }
        else
        {
            mailboxEntity = new Mailbox
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                UserId = tenantContext.UserId,
                DisplayName = request.FileName,
                FileName = request.FileName,
                FileSizeBytes = request.FileSizeBytes,
                BlobPath = string.Empty,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Mailboxes.Add(mailboxEntity);
            await context.SaveChangesAsync();
        }

        var mailbox = mailboxEntity;
        if (mailbox == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox could not be resolved"
            ));
        }

        // 4. Create MailboxUpload record
        var upload = new MailboxUpload
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            MailboxId = mailbox.Id,
            UploadedByUserId = tenantContext.UserId,
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            BlobPath = string.Empty,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.MailboxUploads.Add(upload);
        mailbox.FileName = request.FileName;
        mailbox.FileSizeBytes = request.FileSizeBytes;
        mailbox.Status = "Pending";
        mailbox.LatestUploadId = upload.Id;
        mailbox.UploadRemovedAt = null;
        mailbox.UploadRemovedByUserId = null;
        mailbox.IsPendingDeletion = false;
        mailbox.PurgeAfter = null;
        await context.SaveChangesAsync();
        
        // 5. Generate SAS token (2 hours validity for large uploads)
        var sasInfo = await blobService.GenerateUploadSasTokenAsync(
            tenantContext.TenantId,
            mailbox.Id,
            request.FileName,
            TimeSpan.FromHours(2)
        );
        
        // 6. Update mailbox with blob path
        mailbox.BlobPath = sasInfo.BlobPath;
        upload.BlobPath = sasInfo.BlobPath;
        await context.SaveChangesAsync();
        
        return Results.Ok(new ApiResponse<InitiateUploadResponse>(
            Success: true,
            Data: new InitiateUploadResponse(
                sasInfo.SasUrl,
                sasInfo.BlobPath,
                mailbox.Id,
                upload.Id,
                sasInfo.ExpiresAt
            )
        ));
    }
    
    private static Task<IResult> InitiateUploadAsync(
        InitiateUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
        => InitiateUploadInternalAsync(request, null, blobService, context, tenantContext);

    private static async Task<IResult> CompleteUploadAsync(
        CompleteUploadRequest request,
        EvermailDbContext context,
        IQueueService queueService,
        IMailboxEncryptionStateService encryptionStateService,
        TenantContext tenantContext)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Get mailbox and verify ownership
        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == request.MailboxId && m.TenantId == tenantContext.TenantId);
        
        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found or you don't have permission to access it"
            ));
        }
        
        // Update status to Queued
        mailbox.Status = "Queued";
        mailbox.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        // Send message to queue for background processing
        if (request.UploadId == Guid.Empty)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "UploadId is required"
            ));
        }

        var upload = await context.MailboxUploads
            .FirstOrDefaultAsync(u => u.Id == request.UploadId && u.MailboxId == mailbox.Id && u.TenantId == tenantContext.TenantId);

        if (upload == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Upload not found or you don't have permission to access it"
            ));
        }

        upload.Status = "Queued";
        upload.ProcessingStartedAt = null;
        mailbox.LatestUploadId = upload.Id;

        await context.SaveChangesAsync();

        var encryptionState = await encryptionStateService.CreateAsync(
            tenantContext.TenantId,
            mailbox.Id,
            upload.Id,
            tenantContext.UserId);

        // Send message to queue for background processing
        await queueService.EnqueueMailboxProcessingAsync(mailbox.Id, upload.Id, encryptionState.Id);
        
        return Results.Ok(new ApiResponse<CompleteUploadResponse>(
            Success: true,
            Data: new CompleteUploadResponse(
                mailbox.Id,
                upload.Id,
                "Queued"
            )
        ));
    }
}

