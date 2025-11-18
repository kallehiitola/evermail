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

    public async Task EnqueueMailboxProcessingAsync(Guid mailboxId, Guid mailboxUploadId)
    {
        await _ingestionQueue.CreateIfNotExistsAsync();

        var message = JsonSerializer.Serialize(new MailboxProcessingMessage(
            mailboxId,
            mailboxUploadId,
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

        await _deletionQueue.SendMessageAsync(message);
    }

    private record MailboxProcessingMessage(Guid MailboxId, Guid UploadId, DateTime EnqueuedAt);
    private record MailboxDeletionMessage(Guid JobId, Guid MailboxId, DateTime EnqueuedAt);
}

