namespace Evermail.Common.DTOs.Mailbox;

public record MailboxDto(
    Guid Id,
    string FileName,
    long FileSizeBytes,
    string Status,
    int TotalEmails,
    int ProcessedEmails,
    int FailedEmails,
    long ProcessedBytes,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    string? ErrorMessage
);

public record PagedMailboxesResponse(
    List<MailboxDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

