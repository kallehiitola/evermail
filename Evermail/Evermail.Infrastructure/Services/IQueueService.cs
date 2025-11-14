namespace Evermail.Infrastructure.Services;

public interface IQueueService
{
    /// <summary>
    /// Enqueues a mailbox for background processing by the IngestionWorker.
    /// </summary>
    Task EnqueueMailboxProcessingAsync(Guid mailboxId);
}

