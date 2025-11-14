namespace Evermail.Common.DTOs.Upload;

public record InitiateUploadRequest(
    string FileName,
    long FileSizeBytes,
    string FileType  // "mbox", "google-takeout-zip", "microsoft-export-zip"
);

public record InitiateUploadResponse(
    string UploadUrl,  // Azure Blob SAS URL
    string BlobPath,
    Guid MailboxId,
    DateTime ExpiresAt
);

public record CompleteUploadRequest(
    Guid MailboxId
);

public record CompleteUploadResponse(
    Guid MailboxId,
    string Status
);

