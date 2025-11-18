using System.Diagnostics;
using System.Text.Json;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Email;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class EmailEndpoints
{
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/search", SearchEmailsAsync)
            .RequireAuthorization();
        
        group.MapGet("/{id:guid}", GetEmailByIdAsync)
            .RequireAuthorization();
        
        return group;
    }

    private static async Task<IResult> SearchEmailsAsync(
        EvermailDbContext context,
        TenantContext tenantContext,
        string? q = null,
        Guid? mailboxId = null,
        string? from = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool? hasAttachments = null,
        int page = 1,
        int pageSize = 50,
        string sortBy = "date",
        string sortOrder = "desc")
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Enforce max page size
        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var stopwatch = Stopwatch.StartNew();

        // Build base query with tenant isolation
        var query = context.EmailMessages
            .AsNoTracking()
            .Where(e => e.TenantId == tenantContext.TenantId && e.UserId == tenantContext.UserId);

        // Filter by mailbox if provided
        if (mailboxId.HasValue)
        {
            query = query.Where(e => e.MailboxId == mailboxId.Value);
        }

        // Filter by sender if provided
        if (!string.IsNullOrEmpty(from))
        {
            query = query.Where(e => e.FromAddress.Contains(from) || (e.FromName != null && e.FromName.Contains(from)));
        }

        // Filter by date range
        if (dateFrom.HasValue)
        {
            query = query.Where(e => e.Date >= dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            query = query.Where(e => e.Date <= dateTo.Value);
        }

        // Filter by attachments
        if (hasAttachments.HasValue)
        {
            query = query.Where(e => e.HasAttachments == hasAttachments.Value);
        }

        // Full-text search (simple contains for now - can be enhanced with SQL Server FTS later)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim();
            query = query.Where(e =>
                (e.Subject != null && e.Subject.Contains(searchTerm)) ||
                (e.TextBody != null && e.TextBody.Contains(searchTerm)) ||
                (e.Snippet != null && e.Snippet.Contains(searchTerm)) ||
                e.FromAddress.Contains(searchTerm) ||
                (e.FromName != null && e.FromName.Contains(searchTerm))
            );
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "subject" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(e => e.Subject)
                : query.OrderByDescending(e => e.Subject),
            "from" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(e => e.FromAddress)
                : query.OrderByDescending(e => e.FromAddress),
            _ => sortOrder.ToLower() == "asc"
                ? query.OrderBy(e => e.Date)
                : query.OrderByDescending(e => e.Date)
        };

        // Apply pagination
        var emailEntities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get email IDs
        var emailIds = emailEntities.Select(e => e.Id).ToList();

        // Get first attachment ID for each email (if any)
        // Use a subquery approach that EF Core can translate properly
        var firstAttachmentIds = new Dictionary<Guid, Guid>();
        if (emailIds.Any())
        {
            var attachments = await context.Attachments
                .AsNoTracking()
                .Where(a => emailIds.Contains(a.EmailMessageId) && 
                           a.TenantId == tenantContext.TenantId)
                .OrderBy(a => a.EmailMessageId)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();

            // Group in memory to get first attachment per email
            var grouped = attachments.GroupBy(a => a.EmailMessageId);
            foreach (var group in grouped)
            {
                var firstAttachment = group.FirstOrDefault();
                if (firstAttachment != null)
                {
                    firstAttachmentIds[group.Key] = firstAttachment.Id;
                }
            }
        }

        // Map to DTOs with first attachment ID
        var emails = emailEntities.Select(e => new EmailListItemDto(
            e.Id,
            e.MailboxId,
            e.Subject,
            e.FromAddress,
            e.FromName,
            e.Date,
            e.Snippet,
            e.HasAttachments,
            e.AttachmentCount,
            e.IsRead,
            firstAttachmentIds.TryGetValue(e.Id, out var attachmentId) ? attachmentId : null
        )).ToList();

        stopwatch.Stop();

        return Results.Ok(new ApiResponse<PagedEmailsResponse>(
            Success: true,
            Data: new PagedEmailsResponse(
                emails,
                totalCount,
                page,
                pageSize,
                stopwatch.Elapsed.TotalSeconds
            )
        ));
    }

    private static async Task<IResult> GetEmailByIdAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var email = await context.EmailMessages
            .AsNoTracking()
            .Include(e => e.Attachments)
            .Where(e => e.Id == id && e.TenantId == tenantContext.TenantId && e.UserId == tenantContext.UserId)
            .FirstOrDefaultAsync();

        if (email == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Email not found"
            ));
        }

        // Parse JSON arrays for recipients
        var toAddresses = string.IsNullOrEmpty(email.ToAddresses)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.ToAddresses) ?? new List<string>();
        
        var toNames = string.IsNullOrEmpty(email.ToNames)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.ToNames) ?? new List<string>();
        
        var ccAddresses = string.IsNullOrEmpty(email.CcAddresses)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.CcAddresses) ?? new List<string>();
        
        var ccNames = string.IsNullOrEmpty(email.CcNames)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.CcNames) ?? new List<string>();
        
        var bccAddresses = string.IsNullOrEmpty(email.BccAddresses)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.BccAddresses) ?? new List<string>();
        
        var bccNames = string.IsNullOrEmpty(email.BccNames)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(email.BccNames) ?? new List<string>();

        // Map attachments
        var attachments = email.Attachments.Select(a => new AttachmentDto(
            a.Id,
            a.FileName,
            a.ContentType,
            a.SizeBytes,
            null // Download URL will be generated by attachment endpoint
        )).ToList();

        var emailDto = new EmailDetailDto(
            email.Id,
            email.MailboxId,
            email.MessageId,
            email.InReplyTo,
            email.References,
            email.Subject,
            email.FromAddress,
            email.FromName,
            toAddresses,
            toNames,
            ccAddresses,
            ccNames,
            bccAddresses,
            bccNames,
            email.Date,
            email.Snippet,
            email.TextBody,
            email.HtmlBody,
            email.HasAttachments,
            email.AttachmentCount,
            email.IsRead,
            attachments
        );

        return Results.Ok(new ApiResponse<EmailDetailDto>(
            Success: true,
            Data: emailDto
        ));
    }
}

