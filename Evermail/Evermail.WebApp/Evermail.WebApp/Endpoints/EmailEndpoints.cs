using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Email;
using Evermail.Common.Search;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class EmailEndpoints
{
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
            var snippet = BuildSnippetResult(e, searchTerms);

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
                e.HasAttachments,
                e.AttachmentCount,
                e.IsRead,
                hasAttachmentId,
                rank,
                e.ConversationId,
                e.Thread?.MessageCount ?? 1,
                e.ThreadDepth);
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
            email.HtmlBody,
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

    private sealed class EmailWithRank
    {
        public EmailMessage Email { get; set; } = default!;
        public double? Rank { get; set; }
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

        var rankQuery = context.FullTextSearchResults
            .FromSqlInterpolated($@"
                SELECT [KEY] AS EmailId, [RANK] AS Rank
                FROM CONTAINSTABLE(
                    EmailMessages,
                    SearchVector,
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
        var matchFields = DetectMatchFields(email, searchTerms);

        if (searchTerms.Count == 0)
        {
            var fallback = email.Snippet ?? email.Subject ?? string.Empty;
            var sanitized = string.IsNullOrWhiteSpace(fallback) ? null : WebUtility.HtmlEncode(fallback);
            return new SnippetResult(
                string.IsNullOrWhiteSpace(fallback) ? null : fallback,
                sanitized,
                matchFields);
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
            if (!string.IsNullOrWhiteSpace(window))
            {
                return new SnippetResult(
                    window,
                    BuildHighlightHtml(window, searchTerms),
                    matchFields);
            }
        }

        var fallbackPlain = email.Snippet ?? email.Subject ?? string.Empty;
        return new SnippetResult(
            string.IsNullOrWhiteSpace(fallbackPlain) ? null : fallbackPlain,
            BuildHighlightHtml(fallbackPlain, searchTerms),
            matchFields);
    }

    private static string? ExtractSnippetWindow(string? source, IReadOnlyList<string> searchTerms)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        var bestIndex = int.MaxValue;
        var bestTermLength = 0;

        foreach (var term in searchTerms)
        {
            var index = compareInfo.IndexOf(source, term, CompareOptions.IgnoreCase);
            if (index >= 0 && index < bestIndex)
            {
                bestIndex = index;
                bestTermLength = term.Length;
            }
        }

        if (bestIndex == int.MaxValue)
        {
            return null;
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

        return window;
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

    private static IReadOnlyList<string> DetectMatchFields(EmailMessage email, IReadOnlyList<string> searchTerms)
    {
        if (searchTerms.Count == 0)
        {
            return Array.Empty<string>();
        }

        var fields = new List<string>(capacity: 4);

        if (ContainsTerm(email.Subject, searchTerms))
        {
            fields.Add("Subject");
        }

        if (ContainsTerm(email.TextBody, searchTerms) || ContainsTerm(email.HtmlBody, searchTerms))
        {
            fields.Add("Body");
        }

        if (ContainsTerm(email.FromName, searchTerms) || ContainsTerm(email.FromAddress, searchTerms))
        {
            fields.Add("Sender");
        }

        if (ContainsTerm(email.RecipientsSearch, searchTerms))
        {
            fields.Add("Recipients");
        }

        return fields.Count == 0 ? Array.Empty<string>() : fields;
    }

    private static bool ContainsTerm(string? source, IReadOnlyList<string> searchTerms)
    {
        if (string.IsNullOrWhiteSpace(source) || searchTerms.Count == 0)
        {
            return false;
        }

        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        return searchTerms.Any(term => compareInfo.IndexOf(source, term, CompareOptions.IgnoreCase) >= 0);
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

    private sealed record SnippetResult(string? PlainText, string? HighlightHtml, IReadOnlyList<string> MatchFields);
}

