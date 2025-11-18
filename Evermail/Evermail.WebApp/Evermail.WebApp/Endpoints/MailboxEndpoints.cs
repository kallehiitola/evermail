using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Mailbox;
using Evermail.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class MailboxEndpoints
{
    public static RouteGroupBuilder MapMailboxEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetMailboxesAsync)
            .RequireAuthorization();
        
        group.MapGet("/{id:guid}", GetMailboxByIdAsync)
            .RequireAuthorization();
        
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
            .Where(m => m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId);

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
                m.FileName,
                m.FileSizeBytes,
                m.Status,
                m.TotalEmails,
                m.ProcessedEmails,
                m.FailedEmails,
                m.ProcessedBytes,
                m.CreatedAt,
                m.ProcessingStartedAt,
                m.ProcessingCompletedAt,
                m.ErrorMessage
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
            .Where(m => m.Id == id && m.TenantId == tenantContext.TenantId && m.UserId == tenantContext.UserId)
            .Select(m => new MailboxDto(
                m.Id,
                m.FileName,
                m.FileSizeBytes,
                m.Status,
                m.TotalEmails,
                m.ProcessedEmails,
                m.FailedEmails,
                m.ProcessedBytes,
                m.CreatedAt,
                m.ProcessingStartedAt,
                m.ProcessingCompletedAt,
                m.ErrorMessage
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
}

