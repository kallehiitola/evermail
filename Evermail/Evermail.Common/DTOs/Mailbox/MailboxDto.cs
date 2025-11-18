namespace Evermail.Common.DTOs.Mailbox;

public record MailboxDto(
    Guid Id,
    string? DisplayName,
    string FileName,
    long FileSizeBytes,
    string Status,
    bool UploadRemoved,
    bool IsPendingDeletion,
    int TotalEmails,
    int ProcessedEmails,
    int FailedEmails,
    long ProcessedBytes,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    string? ErrorMessage,
    MailboxUploadSummaryDto? LatestUpload
);

public record PagedMailboxesResponse(
    List<MailboxDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record MailboxUploadSummaryDto(
    Guid Id,
    string FileName,
    long FileSizeBytes,
    string Status,
    bool KeepEmails,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    DateTime? DeletedAt
);

public record MailboxUploadDto(
    Guid Id,
    string FileName,
    long FileSizeBytes,
    string Status,
    bool KeepEmails,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    DateTime? DeletedAt
);

public record MailboxUploadsResponse(
    List<MailboxUploadDto> Items
);

public record RenameMailboxRequest(string DisplayName);

public record DeleteMailboxRequest(
    bool DeleteUpload,
    bool DeleteEmails,
    bool PurgeNow,
    Guid? UploadId
);

public record DeleteMailboxResponse(
    Guid JobId,
    DateTime ExecuteAfter,
    string Status
);

