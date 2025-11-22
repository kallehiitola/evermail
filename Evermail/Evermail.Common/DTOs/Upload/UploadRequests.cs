namespace Evermail.Common.DTOs.Upload;

public record InitiateUploadRequest(
    string FileName,
    long FileSizeBytes,
    string? FileType,  // optional hint (mbox | google-takeout-zip | microsoft-export-zip | outlook-pst | outlook-pst-zip | outlook-ost | outlook-ost-zip | eml | eml-zip)
    Guid? MailboxId = null
);

public record InitiateUploadResponse(
    string UploadUrl,  // Azure Blob SAS URL
    string BlobPath,
    Guid MailboxId,
    Guid UploadId,
    DateTime ExpiresAt
);

public record CompleteUploadRequest(
    Guid MailboxId,
    Guid UploadId
);

public record CompleteUploadResponse(
    Guid MailboxId,
    Guid UploadId,
    string Status
);

