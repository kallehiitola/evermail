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
    private readonly QueueClient _ingestionQueue;
    private readonly QueueClient _deletionQueue;
    private const string IngestionQueueName = "mailbox-ingestion";
    private const string DeletionQueueName = "mailbox-deletion";

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        QueueServiceClient queueServiceClient)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _ingestionQueue = queueServiceClient.GetQueueClient(IngestionQueueName);
        _deletionQueue = queueServiceClient.GetQueueClient(DeletionQueueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mailbox worker started");

        await _ingestionQueue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        await _deletionQueue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        var ingestionTask = ProcessIngestionQueueAsync(stoppingToken);
        var deletionTask = ProcessDeletionQueueAsync(stoppingToken);

        await Task.WhenAll(ingestionTask, deletionTask);
    }

    private async Task ProcessIngestionQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _ingestionQueue.ReceiveMessagesAsync(
                    maxMessages: 10,
                    cancellationToken: cancellationToken);

                if (response.Value is { Length: > 0 })
                {
                    foreach (var message in response.Value)
                    {
                        await ProcessMailboxMessageAsync(message, cancellationToken);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling ingestion queue");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    private async Task ProcessDeletionQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _deletionQueue.ReceiveMessagesAsync(
                    maxMessages: 5,
                    cancellationToken: cancellationToken);

                if (response.Value is { Length: > 0 })
                {
                    foreach (var message in response.Value)
                    {
                        await ProcessDeletionMessageAsync(message, cancellationToken);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling deletion queue");
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }

    private async Task ProcessMailboxMessageAsync(
        Azure.Storage.Queues.Models.QueueMessage message,
        CancellationToken cancellationToken)
    {
        Guid mailboxId = Guid.Empty;
        Guid uploadId = Guid.Empty;

        try
        {
            var queueData = JsonSerializer.Deserialize<MailboxQueueMessage>(message.MessageText);
            if (queueData == null || queueData.MailboxId == Guid.Empty || queueData.UploadId == Guid.Empty)
            {
                _logger.LogWarning("Invalid ingestion queue message: {MessageText}", message.MessageText);
                await _ingestionQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                return;
            }

            mailboxId = queueData.MailboxId;
            uploadId = queueData.UploadId;
            _logger.LogInformation("Processing mailbox {MailboxId} upload {UploadId}", mailboxId, uploadId);

            using var scope = _serviceProvider.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<MailboxProcessingService>();

            await processingService.ProcessMailboxAsync(mailboxId, uploadId, cancellationToken);

            await _ingestionQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

            _logger.LogInformation("Successfully processed mailbox {MailboxId} upload {UploadId}", mailboxId, uploadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process mailbox {MailboxId} upload {UploadId}. Message will remain in queue for retry.",
                mailboxId,
                uploadId
            );
        }
    }

    private async Task ProcessDeletionMessageAsync(
        Azure.Storage.Queues.Models.QueueMessage message,
        CancellationToken cancellationToken)
    {
        Guid jobId = Guid.Empty;
        try
        {
            var queueData = JsonSerializer.Deserialize<MailboxDeletionMessage>(message.MessageText);
            if (queueData == null || queueData.JobId == Guid.Empty)
            {
                _logger.LogWarning("Invalid deletion queue message: {MessageText}", message.MessageText);
                await _deletionQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                return;
            }

            jobId = queueData.JobId;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EvermailDbContext>();
            var job = await context.MailboxDeletionQueue
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("Deletion job {JobId} not found, removing message", jobId);
                await _deletionQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                return;
            }

            if (job.ExecuteAfter > DateTime.UtcNow)
            {
                var delay = job.ExecuteAfter - DateTime.UtcNow;
                if (delay < TimeSpan.FromMinutes(1))
                {
                    delay = TimeSpan.FromMinutes(1);
                }

                await _deletionQueue.UpdateMessageAsync(
                    message.MessageId,
                    message.PopReceipt,
                    message.MessageText,
                    delay,
                    cancellationToken);
                return;
            }

            var deletionService = scope.ServiceProvider.GetRequiredService<MailboxDeletionService>();
            await deletionService.ExecuteDeletionJobAsync(jobId, cancellationToken);

            await _deletionQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deletion job {JobId}", jobId);
        }
    }

    private record MailboxQueueMessage(Guid MailboxId, Guid UploadId, DateTime EnqueuedAt);
    private record MailboxDeletionMessage(Guid JobId, Guid MailboxId, DateTime EnqueuedAt);
}
