using System.Text.Json;
using Azure.Storage.Queues;

namespace Evermail.Infrastructure.Services;

public class QueueService : IQueueService
{
    private readonly QueueClient _queueClient;
    private const string QueueName = "mailbox-processing";

    public QueueService(QueueServiceClient queueServiceClient)
    {
        _queueClient = queueServiceClient.GetQueueClient(QueueName);
    }

    public async Task EnqueueMailboxProcessingAsync(Guid mailboxId)
    {
        // Create queue if it doesn't exist
        await _queueClient.CreateIfNotExistsAsync();
        
        // Create message with mailbox ID and timestamp
        var message = JsonSerializer.Serialize(new
        {
            MailboxId = mailboxId,
            EnqueuedAt = DateTime.UtcNow
        });
        
        // Send message to queue
        await _queueClient.SendMessageAsync(message);
    }
}

