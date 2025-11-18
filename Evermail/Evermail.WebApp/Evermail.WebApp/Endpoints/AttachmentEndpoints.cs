using Azure.Storage.Blobs;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Email;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class AttachmentEndpoints
{
    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/download", DownloadAttachmentAsync)
            .RequireAuthorization();
        
        return group;
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext,
        BlobServiceClient blobServiceClient)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Get attachment from database
        var attachment = await context.Attachments
            .AsNoTracking()
            .Include(a => a.EmailMessage)
            .Where(a => a.Id == id && 
                       a.TenantId == tenantContext.TenantId && 
                       a.EmailMessage.UserId == tenantContext.UserId)
            .FirstOrDefaultAsync();

        if (attachment == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Attachment not found"
            ));
        }

        // Get blob from Azure Storage
        var containerClient = blobServiceClient.GetBlobContainerClient("attachments");
        var blobClient = containerClient.GetBlobClient(attachment.BlobPath);

        if (!await blobClient.ExistsAsync())
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Attachment file not found"
            ));
        }

        // Stream the file directly through our API (more secure - SAS URL never exposed)
        var blobStream = await blobClient.OpenReadAsync();
        
        // Return file stream with proper headers
        return Results.File(
            blobStream,
            contentType: attachment.ContentType,
            fileDownloadName: attachment.FileName,
            enableRangeProcessing: true // Support partial content/range requests
        );
    }
}

