using System.Text.Json;
using Azure.Storage.Queues;

namespace Evermail.Infrastructure.Services;

public class QueueService : IQueueService
{
    private readonly QueueClient _ingestionQueue;
    private readonly QueueClient _deletionQueue;
    private const string IngestionQueueName = "mailbox-ingestion";
    private const string DeletionQueueName = "mailbox-deletion";

    public QueueService(QueueServiceClient queueServiceClient)
    {
        _ingestionQueue = queueServiceClient.GetQueueClient(IngestionQueueName);
        _deletionQueue = queueServiceClient.GetQueueClient(DeletionQueueName);
    }

    public async Task EnqueueMailboxProcessingAsync(Guid mailboxId, Guid mailboxUploadId, Guid mailboxEncryptionStateId)
    {
        await _ingestionQueue.CreateIfNotExistsAsync();

        var message = JsonSerializer.Serialize(new MailboxProcessingMessage(
            mailboxId,
            mailboxUploadId,
            mailboxEncryptionStateId,
            DateTime.UtcNow));

        await _ingestionQueue.SendMessageAsync(message);
    }

    public async Task EnqueueMailboxDeletionAsync(Guid deletionJobId, Guid mailboxId)
    {
        await _deletionQueue.CreateIfNotExistsAsync();

        var message = JsonSerializer.Serialize(new MailboxDeletionMessage(
            deletionJobId,
            mailboxId,
            DateTime.UtcNow));

        await _deletionQueue.SendMessageAsync(
            message,
            visibilityTimeout: TimeSpan.Zero,
            timeToLive: TimeSpan.FromSeconds(-1));
    }

    private record MailboxProcessingMessage(Guid MailboxId, Guid UploadId, Guid EncryptionStateId, DateTime EnqueuedAt);
    private record MailboxDeletionMessage(Guid JobId, Guid MailboxId, DateTime EnqueuedAt);
}

