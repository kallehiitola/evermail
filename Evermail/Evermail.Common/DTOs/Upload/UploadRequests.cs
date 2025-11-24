using System.Collections.Generic;

namespace Evermail.Common.DTOs.Upload;

public record InitiateUploadRequest(
    string FileName,
    long FileSizeBytes,
    string? FileType,  // optional hint (mbox | google-takeout-zip | microsoft-export-zip | outlook-pst | outlook-pst-zip | outlook-ost | outlook-ost-zip | eml | eml-zip)
    Guid? MailboxId = null,
    bool ClientSideEncryption = false
);

public record InitiateZeroAccessUploadRequest(
    string FileName,
    long FileSizeBytes,
    Guid? MailboxId = null,
    string Scheme = "zero-access/aes-gcm-chunked/v1"
);

public record DeterministicTokenSetDto(
    string TokenType,
    IReadOnlyList<string> Tokens
);

public record CompleteZeroAccessUploadRequest(
    Guid MailboxId,
    Guid UploadId,
    string Scheme,
    string KeyFingerprint,
    string MetadataJson,
    long OriginalSizeBytes,
    long CipherSizeBytes,
    IReadOnlyList<DeterministicTokenSetDto>? TokenSets = null
);

public record InitiateUploadResponse(
    string UploadUrl,  // Azure Blob SAS URL
    string BlobPath,
    Guid MailboxId,
    Guid UploadId,
    DateTime ExpiresAt
);

public record InitiateZeroAccessUploadResponse(
    string UploadUrl,
    string BlobPath,
    Guid MailboxId,
    Guid UploadId,
    DateTime ExpiresAt,
    string TokenSalt
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

