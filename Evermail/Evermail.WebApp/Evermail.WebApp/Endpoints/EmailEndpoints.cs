using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Email;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.Data.SqlClient;
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
        string? sortBy = null,
        string sortOrder = "desc",
        string? stopWords = null,
        bool useInflectionalForms = false,
        ILoggerFactory? loggerFactory = null)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        page = Math.Max(1, page);
        if (pageSize > 100)
        {
            pageSize = 100;
        }
        else if (pageSize < 1)
        {
            pageSize = 1;
        }

        var stopwatch = Stopwatch.StartNew();
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();

        var query = context.EmailMessages
            .AsNoTracking()
            .Where(e => e.TenantId == tenantContext.TenantId && e.UserId == tenantContext.UserId);

        if (mailboxId.HasValue)
        {
            query = query.Where(e => e.MailboxId == mailboxId.Value);
        }

        if (!string.IsNullOrEmpty(from))
        {
            query = query.Where(e => e.FromAddress.Contains(from) || (e.FromName != null && e.FromName.Contains(from)));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(e => e.Date >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(e => e.Date <= dateTo.Value);
        }

        if (hasAttachments.HasValue)
        {
            query = query.Where(e => e.HasAttachments == hasAttachments.Value);
        }

        var stopWordSet = BuildStopWordSet(stopWords);
        var fullTextCondition = BuildFullTextSearchCondition(q, stopWordSet, useInflectionalForms);
        var isFullTextSearch = !string.IsNullOrWhiteSpace(fullTextCondition);

        var searchProjection = BuildSearchProjection(
            context,
            query,
            q,
            fullTextCondition,
            page,
            pageSize,
            ref isFullTextSearch);

        var effectiveSortBy = string.IsNullOrWhiteSpace(normalizedSortBy)
            ? (isFullTextSearch ? "rank" : "date")
            : normalizedSortBy;

        int totalCount;
        List<EmailWithRank> pageResults;

        var logger = loggerFactory?.CreateLogger("EmailEndpoints");

        try
        {
            totalCount = await searchProjection.CountAsync();

            var sortedProjection = ApplySorting(searchProjection, effectiveSortBy, sortOrder, isFullTextSearch);

            pageResults = await sortedProjection
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (SqlException ex) when (isFullTextSearch && ex.Number == 7601)
        {
            logger?.LogWarning(ex, "Full-text search unavailable. Falling back to basic search for tenant {TenantId}", tenantContext.TenantId);
            isFullTextSearch = false;

            searchProjection = BuildFallbackProjection(query, q);
            effectiveSortBy = string.IsNullOrWhiteSpace(normalizedSortBy) ? "date" : normalizedSortBy;

            totalCount = await searchProjection.CountAsync();

            var sortedFallback = ApplySorting(searchProjection, effectiveSortBy, sortOrder, isFullTextSearch);

            pageResults = await sortedFallback
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        var emailEntities = pageResults.Select(r => r.Email).ToList();
        var rankLookup = pageResults.ToDictionary(r => r.Email.Id, r => r.Rank);

        var emailIds = emailEntities.Select(e => e.Id).ToList();
        var firstAttachmentIds = new Dictionary<Guid, Guid>();

        if (emailIds.Any())
        {
            var attachments = await context.Attachments
                .AsNoTracking()
                .Where(a => emailIds.Contains(a.EmailMessageId) && a.TenantId == tenantContext.TenantId)
                .OrderBy(a => a.EmailMessageId)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();

            foreach (var group in attachments.GroupBy(a => a.EmailMessageId))
            {
                var firstAttachment = group.FirstOrDefault();
                if (firstAttachment != null)
                {
                    firstAttachmentIds[group.Key] = firstAttachment.Id;
                }
            }
        }

        var emails = emailEntities.Select(e =>
        {
            rankLookup.TryGetValue(e.Id, out var rank);
            var hasAttachmentId = firstAttachmentIds.TryGetValue(e.Id, out var attachmentId) ? attachmentId : (Guid?)null;

            return new EmailListItemDto(
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
                hasAttachmentId,
                rank);
        }).ToList();

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

    private sealed class EmailWithRank
    {
        public EmailMessage Email { get; set; } = default!;
        public double? Rank { get; set; }
    }

    private static IQueryable<EmailWithRank> BuildSearchProjection(
        EvermailDbContext context,
        IQueryable<EmailMessage> baseQuery,
        string? q,
        string? fullTextCondition,
        int page,
        int pageSize,
        ref bool isFullTextSearch)
    {
        if (!isFullTextSearch)
        {
            return BuildFallbackProjection(baseQuery, q);
        }

        var windowMultiplier = 5;
        var ftsWindow = Math.Clamp(page * pageSize * windowMultiplier, pageSize, 5000);

        var rankQuery = context.FullTextSearchResults
            .FromSqlInterpolated($@"
                SELECT [KEY] AS EmailId, [RANK] AS Rank
                FROM CONTAINSTABLE(
                    EmailMessages,
                    (Subject, TextBody, FromName, FromAddress),
                    {fullTextCondition},
                    {ftsWindow}
                )")
            .AsNoTracking();

        return baseQuery.Join(
            rankQuery,
            email => email.Id,
            rank => rank.EmailId,
            (email, rank) => new EmailWithRank { Email = email, Rank = rank.Rank });
    }

    private static IQueryable<EmailWithRank> BuildFallbackProjection(IQueryable<EmailMessage> query, string? q)
    {
        if (!string.IsNullOrWhiteSpace(q))
        {
            var fallbackTerm = q.Trim();
            query = query.Where(e =>
                (e.Subject != null && e.Subject.Contains(fallbackTerm)) ||
                (e.TextBody != null && e.TextBody.Contains(fallbackTerm)) ||
                (e.Snippet != null && e.Snippet.Contains(fallbackTerm)) ||
                e.FromAddress.Contains(fallbackTerm) ||
                (e.FromName != null && e.FromName.Contains(fallbackTerm)));
        }

        return query.Select(e => new EmailWithRank { Email = e, Rank = null });
    }

    private static IQueryable<EmailWithRank> ApplySorting(
        IQueryable<EmailWithRank> source,
        string sortBy,
        string sortOrder,
        bool hasRank)
    {
        var ascending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "subject" => ascending
                ? source.OrderBy(x => x.Email.Subject)
                : source.OrderByDescending(x => x.Email.Subject),
            "from" => ascending
                ? source.OrderBy(x => x.Email.FromAddress)
                : source.OrderByDescending(x => x.Email.FromAddress),
            "rank" when hasRank => ascending
                ? source.OrderBy(x => x.Rank ?? 0).ThenByDescending(x => x.Email.Date)
                : source.OrderByDescending(x => x.Rank ?? 0).ThenByDescending(x => x.Email.Date),
            _ => ascending
                ? source.OrderBy(x => x.Email.Date)
                : source.OrderByDescending(x => x.Email.Date)
        };
    }

    private static HashSet<string> BuildStopWordSet(string? stopWords)
    {
        if (string.IsNullOrWhiteSpace(stopWords))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return stopWords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Trim().Trim('"'))
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? BuildFullTextSearchCondition(string? query, HashSet<string> stopWords, bool useInflectionalForms)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var matches = Regex.Matches(query, @"(?:""[^""]+"")|(?:\S+)");
        var expressions = new List<string>();

        foreach (Match match in matches)
        {
            var token = match.Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var normalized = token.Trim('"');
            if (stopWords.Contains(normalized))
            {
                continue;
            }

            var escaped = normalized.Replace("\"", "\"\"");
            var literal = $"\"{escaped}\"";
            expressions.Add(useInflectionalForms ? $"FORMSOF(INFLECTIONAL, {literal})" : literal);
        }

        return expressions.Count == 0 ? null : string.Join(" AND ", expressions);
    }
}

