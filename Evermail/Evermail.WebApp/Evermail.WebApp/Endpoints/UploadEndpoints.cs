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

    private static async Task<IResult> InitiateUploadAsync(
        InitiateUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
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
        
        // 4. Create Mailbox record (Pending status)
        // Log values for debugging
        Console.WriteLine($"DEBUG: Creating mailbox with TenantId={tenantContext.TenantId}, UserId={tenantContext.UserId}");
        
        // Verify user exists
        var userExists = await context.Users.AnyAsync(u => u.Id == tenantContext.UserId);
        Console.WriteLine($"DEBUG: User exists in database: {userExists}");
        
        if (!userExists)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User not found. UserId: {tenantContext.UserId}"
            ));
        }
        
        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            BlobPath = string.Empty, // Will be set after blob path is generated
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        
        context.Mailboxes.Add(mailbox);
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
        await context.SaveChangesAsync();
        
        return Results.Ok(new ApiResponse<InitiateUploadResponse>(
            Success: true,
            Data: new InitiateUploadResponse(
                sasInfo.SasUrl,
                sasInfo.BlobPath,
                mailbox.Id,
                sasInfo.ExpiresAt
            )
        ));
    }

    private static async Task<IResult> CompleteUploadAsync(
        CompleteUploadRequest request,
        EvermailDbContext context,
        IQueueService queueService,
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
        await queueService.EnqueueMailboxProcessingAsync(mailbox.Id);
        
        return Results.Ok(new ApiResponse<CompleteUploadResponse>(
            Success: true,
            Data: new CompleteUploadResponse(
                mailbox.Id,
                "Queued"
            )
        ));
    }
}

