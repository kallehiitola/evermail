using System.Text.Json;
using Azure.Storage.Queues;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.IngestionWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueClient _queueClient;
    private const string QueueName = "mailbox-processing";

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        QueueServiceClient queueServiceClient)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _queueClient = queueServiceClient.GetQueueClient(QueueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mailbox processing worker started");

        // Ensure queue exists
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Poll queue for messages (receive up to 10 messages at a time)
                var response = await _queueClient.ReceiveMessagesAsync(
                    maxMessages: 10,
                    cancellationToken: stoppingToken);

                if (response.Value != null && response.Value.Length > 0)
                {
                    _logger.LogInformation("Received {Count} messages from queue", response.Value.Length);

                    foreach (var message in response.Value)
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                    }
                }
                else
                {
                    // No messages, wait before polling again
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling queue");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        Azure.Storage.Queues.Models.QueueMessage message,
        CancellationToken cancellationToken)
    {
        Guid mailboxId = Guid.Empty;

        try
        {
            // Deserialize queue message
            var queueData = JsonSerializer.Deserialize<MailboxQueueMessage>(message.MessageText);
            if (queueData == null || queueData.MailboxId == Guid.Empty)
            {
                _logger.LogWarning("Invalid queue message: {MessageText}", message.MessageText);
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                return;
            }

            mailboxId = queueData.MailboxId;
            _logger.LogInformation("Processing mailbox {MailboxId}", mailboxId);

            // Create a scope for this processing operation
            using var scope = _serviceProvider.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<MailboxProcessingService>();

            // Process the mailbox
            await processingService.ProcessMailboxAsync(mailboxId, cancellationToken);

            // Delete message from queue after successful processing
            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

            _logger.LogInformation("Successfully processed mailbox {MailboxId}", mailboxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process mailbox {MailboxId}. Message will remain in queue for retry.",
                mailboxId
            );

            // Message will remain in queue and become visible again after visibility timeout
            // This allows for automatic retry
            // In production, you might want to implement dead-letter queue after N retries
        }
    }

    private record MailboxQueueMessage
    {
        public Guid MailboxId { get; init; }
        public DateTime EnqueuedAt { get; init; }
    }
}
