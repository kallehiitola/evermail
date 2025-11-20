using System.Collections.Generic;

namespace Evermail.Common.DTOs.Email;

public record EmailListItemDto(
    Guid Id,
    Guid MailboxId,
    string? Subject,
    string FromAddress,
    string? FromName,
    DateTime Date,
    string? Snippet,
    string? HighlightedSnippet,
    IReadOnlyList<string> MatchFields,
    bool HasAttachments,
    int AttachmentCount,
    bool IsRead,
    Guid? FirstAttachmentId = null,
    double? Rank = null,
    Guid? ConversationId = null,
    int ThreadSize = 1,
    int ThreadDepth = 0
);

public record EmailDetailDto(
    Guid Id,
    Guid MailboxId,
    string? MessageId,
    string? InReplyTo,
    string? References,
    string? Subject,
    string FromAddress,
    string? FromName,
    List<string> ToAddresses,
    List<string> ToNames,
    List<string> CcAddresses,
    List<string> CcNames,
    List<string> BccAddresses,
    List<string> BccNames,
    DateTime Date,
    string? Snippet,
    string? TextBody,
    string? HtmlBody,
    string? ReplyToAddress,
    string? SenderAddress,
    string? SenderName,
    string? ReturnPath,
    string? ListId,
    string? ThreadTopic,
    string? Importance,
    string? Priority,
    string? Categories,
    Guid? ConversationId,
    int ThreadSize,
    int ThreadDepth,
    bool HasAttachments,
    int AttachmentCount,
    bool IsRead,
    List<AttachmentDto> Attachments
);

public record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? DownloadUrl
);

public record PagedEmailsResponse(
    List<EmailListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    double? QueryTime = null,
    bool UsedFullTextSearch = false,
    bool FullTextHealthy = true
);

public record AttachmentDownloadDto(
    string DownloadUrl,
    string FileName,
    string ContentType,
    long SizeBytes
);

