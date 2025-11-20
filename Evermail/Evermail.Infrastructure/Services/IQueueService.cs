namespace Evermail.Infrastructure.Services;

public interface IQueueService
{
    /// <summary>
    /// Enqueues a mailbox upload for background processing by the IngestionWorker.
    /// </summary>
    Task EnqueueMailboxProcessingAsync(Guid mailboxId, Guid mailboxUploadId, Guid mailboxEncryptionStateId);

    /// <summary>
    /// Enqueues a mailbox deletion job for background processing.
    /// </summary>
    Task EnqueueMailboxDeletionAsync(Guid deletionJobId, Guid mailboxId);
}

