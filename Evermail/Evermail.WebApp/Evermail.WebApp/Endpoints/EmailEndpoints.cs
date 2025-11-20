using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Email;
using Evermail.Common.DTOs.User;
using Evermail.Common.Search;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class EmailEndpoints
{
    private static readonly Regex ScriptTagRegex = new(@"<script[\s\S]*?>[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleTagRegex = new(@"<style[\s\S]*?>[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DisallowedTagRegex = new(@"</?(?!p\b|br\b|strong\b|em\b|a\b)[a-z0-9]+[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DisallowedAttributeRegex = new(@"\s+(?!href\b)[a-z0-9:-]+=(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions SavedFilterSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly SavedSearchFilterDefinitionDto EmptySavedFilterDefinition = new(
        Query: null,
        MailboxId: null,
        From: null,
        DateFrom: null,
        DateTo: null,
        HasAttachments: null,
        Recipient: null,
        ConversationId: null);

    private static readonly HashSet<string> BooleanOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND",
        "OR",
        "NOT"
    };
    private static volatile bool FullTextUnavailable;
    internal static bool IsFullTextCircuitOpen => FullTextUnavailable;
    internal static void ClearFullTextCircuitBreaker() => FullTextUnavailable = false;
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/search", SearchEmailsAsync)
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetEmailByIdAsync)
            .RequireAuthorization();

        group.MapGet("/saved-filters", GetSavedFiltersAsync)
            .RequireAuthorization();

        group.MapPost("/saved-filters", CreateSavedFilterAsync)
            .RequireAuthorization();

        group.MapPut("/saved-filters/{id:guid}", UpdateSavedFilterAsync)
            .RequireAuthorization();

        group.MapDelete("/saved-filters/{id:guid}", DeleteSavedFilterAsync)
            .RequireAuthorization();

        group.MapPost("/{id:guid}/pin", PinEmailAsync)
            .RequireAuthorization();

        group.MapDelete("/{id:guid}/pin", UnpinEmailAsync)
            .RequireAuthorization();

        group.MapPost("/conversations/{conversationId:guid}/pin", PinConversationAsync)
            .RequireAuthorization();

        group.MapDelete("/conversations/{conversationId:guid}/pin", UnpinConversationAsync)
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
        string? recipient = null,
        Guid? conversationId = null,
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
            .Include(e => e.Thread)
            .Where(e => e.TenantId == tenantContext.TenantId && e.UserId == tenantContext.UserId);

        if (mailboxId.HasValue)
        {
            query = query.Where(e => e.MailboxId == mailboxId.Value);
        }

        if (conversationId.HasValue)
        {
            query = query.Where(e => e.ConversationId == conversationId.Value);
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

        if (!string.IsNullOrWhiteSpace(recipient))
        {
            var normalizedRecipient = recipient.Trim();
            query = query.Where(e =>
                context.EmailRecipients.Any(r =>
                    r.EmailMessageId == e.Id &&
                    r.TenantId == tenantContext.TenantId &&
                    (r.Address.Contains(normalizedRecipient) ||
                        (r.DisplayName != null && r.DisplayName.Contains(normalizedRecipient)))));
        }

        var stopWordSet = BuildStopWordSet(stopWords);
        var searchTerms = SearchQueryParser.ExtractTerms(q, stopWordSet);
        var fullTextCondition = BuildFullTextSearchCondition(q, stopWordSet, useInflectionalForms);
        var isFullTextSearch = !FullTextUnavailable && !string.IsNullOrWhiteSpace(fullTextCondition);

        var searchProjection = BuildSearchProjection(
            context,
            query,
            q,
            searchTerms,
            fullTextCondition,
            page,
            pageSize,
            ref isFullTextSearch);
        searchProjection = AttachPinnedMetadata(searchProjection, context, tenantContext);

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
        catch (SqlException ex) when (isFullTextSearch && IsRecoverableFullTextError(ex))
        {
            logger?.LogWarning(ex,
                "Full-text search failed with error {ErrorNumber}. Falling back to basic search for tenant {TenantId}",
                ex.Number,
                tenantContext.TenantId);
            isFullTextSearch = false;
            if (!FullTextUnavailable && ShouldDisableFullText(ex))
            {
                FullTextUnavailable = true;
                logger?.LogWarning("Disabling full-text search for remainder of process because SQL Server reported error {ErrorNumber}", ex.Number);
            }

            searchProjection = BuildFallbackProjection(query, q, searchTerms);
            searchProjection = AttachPinnedMetadata(searchProjection, context, tenantContext);
            effectiveSortBy = string.IsNullOrWhiteSpace(normalizedSortBy) ? "date" : normalizedSortBy;

            totalCount = await searchProjection.CountAsync();

            var sortedFallback = ApplySorting(searchProjection, effectiveSortBy, sortOrder, isFullTextSearch);

            pageResults = await sortedFallback
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        if (isFullTextSearch)
        {
            FullTextUnavailable = false;
        }

        var emailEntities = pageResults.Select(r => r.Email).ToList();
        var rankLookup = pageResults.ToDictionary(r => r.Email.Id, r => r.Rank);

        var emailIds = emailEntities.Select(e => e.Id).ToList();
        var firstAttachmentIds = new Dictionary<Guid, Guid>();
        var attachmentPreviewLookup = new Dictionary<Guid, IReadOnlyList<AttachmentPreviewDto>>();

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
                var ordered = group.OrderBy(a => a.CreatedAt).ToList();
                if (ordered.Count == 0)
                {
                    continue;
                }

                firstAttachmentIds[group.Key] = ordered[0].Id;
                attachmentPreviewLookup[group.Key] = ordered
                    .Take(3)
                    .Select(a => new AttachmentPreviewDto(a.Id, a.FileName, a.SizeBytes))
                    .ToList();
            }
        }

        var conversationIds = emailEntities
            .Where(e => e.ConversationId.HasValue)
            .Select(e => e.ConversationId!.Value)
            .Distinct()
            .ToList();

        var pinnedEntries = await context.PinnedEmailThreads
            .AsNoTracking()
            .Where(p => p.TenantId == tenantContext.TenantId && p.UserId == tenantContext.UserId &&
                        ((p.EmailMessageId.HasValue && emailIds.Contains(p.EmailMessageId.Value)) ||
                         (p.ConversationId.HasValue && conversationIds.Contains(p.ConversationId.Value))))
            .ToListAsync();

        var pinnedByEmail = pinnedEntries
            .Where(p => p.EmailMessageId.HasValue)
            .ToDictionary(p => p.EmailMessageId!.Value, p => p);

        var pinnedByConversation = pinnedEntries
            .Where(p => p.ConversationId.HasValue)
            .GroupBy(p => p.ConversationId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).First());

        var emails = emailEntities.Select(e =>
        {
            rankLookup.TryGetValue(e.Id, out var rank);
            var hasAttachmentId = firstAttachmentIds.TryGetValue(e.Id, out var attachmentId) ? attachmentId : (Guid?)null;
            var snippet = BuildSnippetResult(e, searchTerms);
            var previews = attachmentPreviewLookup.TryGetValue(e.Id, out var previewList)
                ? previewList
                : Array.Empty<AttachmentPreviewDto>();
            PinnedEmailThread? pinnedEntry = null;

            if (pinnedByEmail.TryGetValue(e.Id, out var pinnedEmail))
            {
                pinnedEntry = pinnedEmail;
            }
            else if (e.ConversationId.HasValue &&
                     pinnedByConversation.TryGetValue(e.ConversationId.Value, out var pinnedThread))
            {
                pinnedEntry = pinnedThread;
            }

            return new EmailListItemDto(
                e.Id,
                e.MailboxId,
                e.Subject,
                e.FromAddress,
                e.FromName,
                e.Date,
                snippet.PlainText,
                snippet.HighlightHtml,
                snippet.MatchFields,
                snippet.MatchSources,
                snippet.MatchedTerms,
                snippet.Offset,
                snippet.Length,
                e.HasAttachments,
                e.AttachmentCount,
                previews,
                e.IsRead,
                hasAttachmentId,
                rank,
                e.ConversationId,
                e.Thread?.MessageCount ?? 1,
                e.ThreadDepth,
                pinnedEntry is not null,
                pinnedEntry?.CreatedAt);
        }).ToList();

        stopwatch.Stop();

        return Results.Ok(new ApiResponse<PagedEmailsResponse>(
            Success: true,
            Data: new PagedEmailsResponse(
                emails,
                totalCount,
                page,
                pageSize,
                stopwatch.Elapsed.TotalSeconds,
                UsedFullTextSearch: isFullTextSearch,
                FullTextHealthy: !FullTextUnavailable
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
            .Include(e => e.Thread)
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
            SanitizeHtml(email.HtmlBody),
            email.ReplyToAddress,
            email.SenderAddress,
            email.SenderName,
            email.ReturnPath,
            email.ListId,
            email.ThreadTopic,
            email.Importance,
            email.Priority,
            email.Categories,
            email.ConversationId,
            email.Thread?.MessageCount ?? 1,
            email.ThreadDepth,
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

    private static async Task<IResult> GetSavedFiltersAsync(
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var entities = await context.SavedSearchFilters
            .AsNoTracking()
            .OrderBy(f => f.OrderIndex)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync();

        var dtos = entities.Select(MapSavedFilterToDto).ToList();

        return Results.Ok(new ApiResponse<List<SavedSearchFilterDto>>(
            Success: true,
            Data: dtos));
    }

    private static async Task<IResult> CreateSavedFilterAsync(
        CreateSavedSearchFilterRequest request,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        if (request is null || request.Definition is null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Definition is required"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Name is required"));
        }

        var normalizedName = request.Name.Trim();
        var definitionJson = JsonSerializer.Serialize(request.Definition, SavedFilterSerializerOptions);

        var nextOrderIndex = request.OrderIndex ?? await context.SavedSearchFilters
            .CountAsync(f => f.UserId == tenantContext.UserId && f.TenantId == tenantContext.TenantId);

        var entity = new SavedSearchFilter
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            Name = normalizedName,
            DefinitionJson = definitionJson,
            OrderIndex = nextOrderIndex,
            IsFavorite = request.IsFavorite,
            CreatedAt = DateTime.UtcNow
        };

        context.SavedSearchFilters.Add(entity);
        await context.SaveChangesAsync();

        var dto = MapSavedFilterToDto(entity);
        return Results.Created($"/api/v1/emails/saved-filters/{entity.Id}", new ApiResponse<SavedSearchFilterDto>(
            Success: true,
            Data: dto));
    }

    private static async Task<IResult> UpdateSavedFilterAsync(
        Guid id,
        UpdateSavedSearchFilterRequest request,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var filter = await context.SavedSearchFilters
            .FirstOrDefaultAsync(f => f.Id == id);

        if (filter == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Saved filter not found"));
        }

        if (!string.IsNullOrWhiteSpace(request?.Name))
        {
            filter.Name = request!.Name.Trim();
        }

        if (request?.Definition is not null)
        {
            filter.DefinitionJson = JsonSerializer.Serialize(request.Definition, SavedFilterSerializerOptions);
        }

        if (request?.OrderIndex is not null)
        {
            filter.OrderIndex = request.OrderIndex.Value;
        }

        if (request?.IsFavorite is not null)
        {
            filter.IsFavorite = request.IsFavorite.Value;
        }

        filter.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return Results.Ok(new ApiResponse<SavedSearchFilterDto>(
            Success: true,
            Data: MapSavedFilterToDto(filter)));
    }

    private static async Task<IResult> DeleteSavedFilterAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var filter = await context.SavedSearchFilters
            .FirstOrDefaultAsync(f => f.Id == id);

        if (filter == null)
        {
            return Results.NoContent();
        }

        context.SavedSearchFilters.Remove(filter);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> PinEmailAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var email = await context.EmailMessages
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.Id, e.ConversationId })
            .FirstOrDefaultAsync();

        if (email == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Email not found"));
        }

        var existing = await context.PinnedEmailThreads
            .FirstOrDefaultAsync(p => p.EmailMessageId == id);

        if (existing != null)
        {
            return Results.Ok(new ApiResponse<PinEmailResponse>(
                Success: true,
                Data: new PinEmailResponse(existing.EmailMessageId, existing.ConversationId, true, existing.CreatedAt)));
        }

        var pin = new PinnedEmailThread
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            EmailMessageId = id,
            ConversationId = email.ConversationId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = tenantContext.UserId
        };

        context.PinnedEmailThreads.Add(pin);
        await context.SaveChangesAsync();

        return Results.Ok(new ApiResponse<PinEmailResponse>(
            Success: true,
            Data: new PinEmailResponse(pin.EmailMessageId, pin.ConversationId, true, pin.CreatedAt)));
    }

    private static async Task<IResult> UnpinEmailAsync(
        Guid id,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var existing = await context.PinnedEmailThreads
            .FirstOrDefaultAsync(p => p.EmailMessageId == id);

        if (existing != null)
        {
            context.PinnedEmailThreads.Remove(existing);
            await context.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> PinConversationAsync(
        Guid conversationId,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var exists = await context.EmailMessages
            .AsNoTracking()
            .AnyAsync(e => e.ConversationId == conversationId &&
                           e.UserId == tenantContext.UserId &&
                           e.TenantId == tenantContext.TenantId);

        if (!exists)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Conversation not found"));
        }

        var existing = await context.PinnedEmailThreads
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.EmailMessageId == null);

        if (existing != null)
        {
            return Results.Ok(new ApiResponse<PinEmailResponse>(
                Success: true,
                Data: new PinEmailResponse(existing.EmailMessageId, existing.ConversationId, true, existing.CreatedAt)));
        }

        var pin = new PinnedEmailThread
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            ConversationId = conversationId,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = tenantContext.UserId
        };

        context.PinnedEmailThreads.Add(pin);
        await context.SaveChangesAsync();

        return Results.Ok(new ApiResponse<PinEmailResponse>(
            Success: true,
            Data: new PinEmailResponse(pin.EmailMessageId, pin.ConversationId, true, pin.CreatedAt)));
    }

    private static async Task<IResult> UnpinConversationAsync(
        Guid conversationId,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (!TryEnsureTenant(tenantContext, out var errorResult))
        {
            return errorResult!;
        }

        var existing = await context.PinnedEmailThreads
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.EmailMessageId == null);

        if (existing != null)
        {
            context.PinnedEmailThreads.Remove(existing);
            await context.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    private static IQueryable<EmailWithRank> AttachPinnedMetadata(
        IQueryable<EmailWithRank> source,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        var pinnedQuery = context.PinnedEmailThreads
            .AsNoTracking()
            .Where(p => p.TenantId == tenantContext.TenantId && p.UserId == tenantContext.UserId);

        return source.Select(x => new EmailWithRank
        {
            Email = x.Email,
            Rank = x.Rank,
            IsPinned = pinnedQuery.Any(p =>
                (p.EmailMessageId.HasValue && p.EmailMessageId == x.Email.Id) ||
                (p.ConversationId.HasValue &&
                 x.Email.ConversationId.HasValue &&
                 p.ConversationId == x.Email.ConversationId))
        });
    }

    private sealed class EmailWithRank
    {
        public EmailMessage Email { get; set; } = default!;
        public double? Rank { get; set; }
        public bool IsPinned { get; set; }
    }

    private static string? SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var sanitized = ScriptTagRegex.Replace(html, string.Empty);
        sanitized = StyleTagRegex.Replace(sanitized, string.Empty);
        sanitized = DisallowedTagRegex.Replace(sanitized, string.Empty);
        sanitized = DisallowedAttributeRegex.Replace(sanitized, match =>
            match.Value.StartsWith(" href", StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : string.Empty);

        return sanitized;
    }

    private static bool TryEnsureTenant(TenantContext tenantContext, out IResult? error)
    {
        if (tenantContext.TenantId == Guid.Empty || tenantContext.UserId == Guid.Empty)
        {
            error = Results.Unauthorized();
            return false;
        }

        error = null;
        return true;
    }

    private static SavedSearchFilterDto MapSavedFilterToDto(SavedSearchFilter entity)
    {
        var definition = DeserializeDefinition(entity.DefinitionJson);
        return new SavedSearchFilterDto(
            entity.Id,
            entity.Name,
            definition,
            entity.OrderIndex,
            entity.IsFavorite,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static SavedSearchFilterDefinitionDto DeserializeDefinition(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptySavedFilterDefinition;
        }

        try
        {
            return JsonSerializer.Deserialize<SavedSearchFilterDefinitionDto>(json, SavedFilterSerializerOptions)
                   ?? EmptySavedFilterDefinition;
        }
        catch
        {
            return EmptySavedFilterDefinition;
        }
    }

    private static IQueryable<EmailWithRank> BuildSearchProjection(
        EvermailDbContext context,
        IQueryable<EmailMessage> baseQuery,
        string? q,
        IReadOnlyList<string> searchTerms,
        string? fullTextCondition,
        int page,
        int pageSize,
        ref bool isFullTextSearch)
    {
        if (!isFullTextSearch)
        {
            return BuildFallbackProjection(baseQuery, q, searchTerms);
        }

        var windowMultiplier = 5;
        var ftsWindow = Math.Clamp(page * pageSize * windowMultiplier, pageSize, 5000);

        var fullTextParameter = new SqlParameter("@FullTextCondition", System.Data.SqlDbType.NVarChar, 4000)
        {
            Value = fullTextCondition ?? string.Empty
        };
        var windowParameter = new SqlParameter("@FullTextWindow", System.Data.SqlDbType.Int)
        {
            Value = ftsWindow
        };

        var rankQuery = context.FullTextSearchResults
            .FromSqlRaw(@"
                SELECT [KEY] AS EmailId, [RANK] AS Rank
                FROM CONTAINSTABLE(
                    EmailMessages,
                    SearchVector,
                    @FullTextCondition,
                    @FullTextWindow
                )", fullTextParameter, windowParameter)
            .AsNoTracking();

        return baseQuery.Join(
            rankQuery,
            email => email.Id,
            rank => rank.EmailId,
            (email, rank) => new EmailWithRank { Email = email, Rank = rank.Rank });
    }

    private static IQueryable<EmailWithRank> BuildFallbackProjection(
        IQueryable<EmailMessage> query,
        string? q,
        IReadOnlyList<string> searchTerms)
    {
        var terms = searchTerms?
            .Where(static term => !string.IsNullOrWhiteSpace(term))
            .Select(static term => term.Trim())
            .Where(static term => term.Length > 0)
            .ToList() ?? new List<string>();

        if (terms.Count == 0 && !string.IsNullOrWhiteSpace(q))
        {
            terms.Add(q.Trim());
        }

        foreach (var term in terms)
        {
            query = ApplyFallbackTermFilter(query, term);
        }

        return query.Select(e => new EmailWithRank { Email = e, Rank = null });
    }

    private static IQueryable<EmailMessage> ApplyFallbackTermFilter(IQueryable<EmailMessage> query, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return query;
        }

        var pattern = $"%{EscapeLikePattern(term)}%";

        return query.Where(e =>
            (e.Subject != null && EF.Functions.Like(e.Subject, pattern)) ||
            (e.TextBody != null && EF.Functions.Like(e.TextBody, pattern)) ||
            (e.HtmlBody != null && EF.Functions.Like(e.HtmlBody, pattern)) ||
            (e.RecipientsSearch != null && EF.Functions.Like(e.RecipientsSearch, pattern)) ||
            (e.Snippet != null && EF.Functions.Like(e.Snippet, pattern)) ||
            EF.Functions.Like(e.FromAddress, pattern) ||
            (e.FromName != null && EF.Functions.Like(e.FromName, pattern)) ||
            EF.Functions.Like(e.SearchVector, pattern));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("[", "[[]", StringComparison.Ordinal)
             .Replace("%", "[%]", StringComparison.Ordinal)
             .Replace("_", "[_]", StringComparison.Ordinal);

    private static IQueryable<EmailWithRank> ApplySorting(
        IQueryable<EmailWithRank> source,
        string sortBy,
        string sortOrder,
        bool hasRank)
    {
        var ascending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
        var pinnedFirst = source.OrderByDescending(x => x.IsPinned);

        return sortBy.ToLowerInvariant() switch
        {
            "subject" => ascending
                ? pinnedFirst.ThenBy(x => x.Email.Subject)
                : pinnedFirst.ThenByDescending(x => x.Email.Subject),
            "from" => ascending
                ? pinnedFirst.ThenBy(x => x.Email.FromAddress)
                : pinnedFirst.ThenByDescending(x => x.Email.FromAddress),
            "rank" when hasRank => ascending
                ? pinnedFirst.ThenBy(x => x.Rank ?? 0).ThenByDescending(x => x.Email.Date)
                : pinnedFirst.ThenByDescending(x => x.Rank ?? 0).ThenByDescending(x => x.Email.Date),
            _ => ascending
                ? pinnedFirst.ThenBy(x => x.Email.Date)
                : pinnedFirst.ThenByDescending(x => x.Email.Date)
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
        var segments = new List<string>();
        var lastWasOperator = true;
        var pendingNot = false;

        foreach (Match match in matches)
        {
            var token = match.Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var isQuoted = token.Length >= 2 && token.StartsWith("\"", StringComparison.Ordinal) && token.EndsWith("\"", StringComparison.Ordinal);
            var normalized = isQuoted ? token[1..^1] : token;

            if (!isQuoted && BooleanOperators.Contains(normalized))
            {
                var upper = normalized.ToUpperInvariant();
                if (upper == "NOT")
                {
                    pendingNot = true;
                }
                else if (segments.Count > 0 && !lastWasOperator)
                {
                    segments.Add(upper);
                    lastWasOperator = true;
                    pendingNot = false;
                }

                continue;
            }

            if (stopWords.Contains(normalized))
            {
                pendingNot = false;
                continue;
            }

            var escaped = normalized.Replace("\"", "\"\"", StringComparison.Ordinal);
            var literal = useInflectionalForms
                ? $"FORMSOF(INFLECTIONAL, \"{escaped}\")"
                : $"\"{escaped}\"";

            if (!lastWasOperator)
            {
                segments.Add("AND");
                lastWasOperator = true;
            }

            if (pendingNot)
            {
                if (segments.Count == 0 || segments[^1] == "AND" || segments[^1] == "OR")
                {
                    segments.Add("NOT");
                }
                else
                {
                    segments.Add("AND");
                    segments.Add("NOT");
                }

                pendingNot = false;
            }

            segments.Add(literal);
            lastWasOperator = false;
        }

        while (segments.Count > 0 && BooleanOperators.Contains(segments[^1]))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count == 0 ? null : string.Join(' ', segments);
    }

    private static SnippetResult BuildSnippetResult(EmailMessage email, IReadOnlyList<string> searchTerms)
    {
        var metadata = DetectMatchMetadata(email, searchTerms);

        if (searchTerms.Count == 0)
        {
            var fallback = email.Snippet ?? email.Subject ?? string.Empty;
            var sanitized = string.IsNullOrWhiteSpace(fallback) ? null : WebUtility.HtmlEncode(fallback);
            return new SnippetResult(
                string.IsNullOrWhiteSpace(fallback) ? null : fallback,
                sanitized,
                metadata.MatchFields,
                metadata.MatchSources,
                metadata.MatchedTerms,
                null,
                null);
        }

        var candidateTexts = new[]
        {
            email.TextBody,
            StripHtml(email.HtmlBody),
            email.Subject,
            email.Snippet,
            email.FromName,
            email.FromAddress
        };

        foreach (var text in candidateTexts)
        {
            var window = ExtractSnippetWindow(text, searchTerms);
            if (!string.IsNullOrWhiteSpace(window.Text))
            {
                return new SnippetResult(
                    window.Text,
                    BuildHighlightHtml(window.Text, searchTerms),
                    metadata.MatchFields,
                    metadata.MatchSources,
                    metadata.MatchedTerms,
                    window.Offset,
                    window.Length);
            }
        }

        var fallbackPlain = email.Snippet ?? email.Subject ?? string.Empty;
        return new SnippetResult(
            string.IsNullOrWhiteSpace(fallbackPlain) ? null : fallbackPlain,
            BuildHighlightHtml(fallbackPlain, searchTerms),
            metadata.MatchFields,
            metadata.MatchSources,
            metadata.MatchedTerms,
            null,
            null);
    }

    private static SnippetWindow ExtractSnippetWindow(string? source, IReadOnlyList<string> searchTerms)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return SnippetWindow.Empty;
        }

        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        var bestIndex = int.MaxValue;
        var bestTermLength = 0;

        foreach (var term in searchTerms)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            var index = compareInfo.IndexOf(source, term, CompareOptions.IgnoreCase);
            if (index >= 0 && index < bestIndex)
            {
                bestIndex = index;
                bestTermLength = term.Length;
            }
        }

        if (bestIndex == int.MaxValue)
        {
            return SnippetWindow.Empty;
        }

        const int radius = 80;
        var start = Math.Max(0, bestIndex - radius);
        var end = Math.Min(source.Length, bestIndex + bestTermLength + radius);
        var window = source[start..end].Trim();

        if (start > 0)
        {
            window = $"…{window}";
        }
        if (end < source.Length)
        {
            window = $"{window}…";
        }

        return new SnippetWindow(window, start, end - start);
    }

    private static string? BuildHighlightHtml(string? snippet, IReadOnlyList<string> searchTerms)
    {
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return null;
        }

        if (searchTerms.Count == 0)
        {
            return WebUtility.HtmlEncode(snippet);
        }

        var escapedTerms = searchTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(Regex.Escape)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (escapedTerms.Length == 0)
        {
            return WebUtility.HtmlEncode(snippet);
        }

        var regex = new Regex(string.Join("|", escapedTerms), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var sb = new System.Text.StringBuilder();
        var lastIndex = 0;

        foreach (Match match in regex.Matches(snippet))
        {
            if (match.Index > lastIndex)
            {
                sb.Append(WebUtility.HtmlEncode(snippet.Substring(lastIndex, match.Index - lastIndex)));
            }

            sb.Append("<mark class=\"search-hit\">");
            sb.Append(WebUtility.HtmlEncode(match.Value));
            sb.Append("</mark>");

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < snippet.Length)
        {
            sb.Append(WebUtility.HtmlEncode(snippet[lastIndex..]));
        }

        return sb.ToString();
    }

    private static MatchMetadata DetectMatchMetadata(EmailMessage email, IReadOnlyList<string> searchTerms)
    {
        if (searchTerms.Count == 0)
        {
            return MatchMetadata.Empty;
        }

        var fields = new List<string>(capacity: 4);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Track(string? sourceValue, string sourceKey, string friendlyName)
        {
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                return;
            }

            var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
            var matched = false;

            foreach (var term in searchTerms)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                if (compareInfo.IndexOf(sourceValue, term, CompareOptions.IgnoreCase) >= 0)
                {
                    matched = true;
                    matchedTerms.Add(term);
                }
            }

            if (!matched)
            {
                return;
            }

            if (!fields.Contains(friendlyName))
            {
                fields.Add(friendlyName);
            }

            sources.Add(sourceKey);
        }

        Track(email.Subject, "Subject", "Subject");
        Track(email.TextBody, "TextBody", "Body");
        Track(email.HtmlBody, "HtmlBody", "Body");
        Track(email.Snippet, "Snippet", "Body");
        Track(email.FromName, "SenderName", "Sender");
        Track(email.FromAddress, "SenderAddress", "Sender");
        Track(email.RecipientsSearch, "Recipients", "Recipients");

        return new MatchMetadata(
            fields.Count == 0 ? Array.Empty<string>() : fields,
            sources.Count == 0 ? Array.Empty<string>() : sources.ToList(),
            matchedTerms.Count == 0 ? Array.Empty<string>() : matchedTerms.ToList());
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        return Regex.Replace(html, "<.*?>", " ");
    }

    private static readonly HashSet<int> RecoverableFullTextErrors = new()
    {
        7600, // generic parsing issues
        7601, // noise/stop words
        7609, // full-text not enabled
        7619, // clause contains only ignored words
        7635, // invalid proximity / boolean syntax
        30053 // transient catalog issues
    };

    private static bool IsRecoverableFullTextError(SqlException exception)
    {
        if (RecoverableFullTextErrors.Contains(exception.Number))
        {
            return true;
        }

        foreach (SqlError error in exception.Errors)
        {
            if (RecoverableFullTextErrors.Contains(error.Number))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldDisableFullText(SqlException exception)
    {
        const int NotIndexed = 7601;
        const int NotEnabled = 7609;

        if (exception.Number == NotIndexed || exception.Number == NotEnabled)
        {
            return true;
        }

        foreach (SqlError error in exception.Errors)
        {
            if (error.Number == NotIndexed || error.Number == NotEnabled)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record SnippetResult(
        string? PlainText,
        string? HighlightHtml,
        IReadOnlyList<string> MatchFields,
        IReadOnlyList<string> MatchSources,
        IReadOnlyList<string> MatchedTerms,
        int? Offset,
        int? Length);

    private sealed record MatchMetadata(
        IReadOnlyList<string> MatchFields,
        IReadOnlyList<string> MatchSources,
        IReadOnlyList<string> MatchedTerms)
    {
        public static MatchMetadata Empty { get; } = new(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed record SnippetWindow(string? Text, int? Offset, int? Length)
    {
        public static SnippetWindow Empty { get; } = new(null, null, null);
    }
}

