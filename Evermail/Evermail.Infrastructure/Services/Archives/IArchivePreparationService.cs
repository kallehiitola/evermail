using Azure.Storage.Blobs;
using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services.Archives;

public interface IArchivePreparationService
{
    Task<ArchiveExtractionResult> PrepareAsync(
        string? sourceFormat,
        BlobClient blobClient,
        Mailbox mailbox,
        MailboxUpload upload,
        long maxUncompressedBytes,
        CancellationToken cancellationToken);
}

