namespace Evermail.Common.DTOs.Email;

public record EmailListItemDto(
    Guid Id,
    Guid MailboxId,
    string? Subject,
    string FromAddress,
    string? FromName,
    DateTime Date,
    string? Snippet,
    bool HasAttachments,
    int AttachmentCount,
    bool IsRead,
    Guid? FirstAttachmentId = null
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
    double? QueryTime = null
);

public record AttachmentDownloadDto(
    string DownloadUrl,
    string FileName,
    string ContentType,
    long SizeBytes
);

