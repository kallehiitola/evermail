using System.Text.Json;
using Azure.Storage.Blobs;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services;

/// <summary>
/// Service for processing mbox files and extracting email messages.
/// Uses streaming to handle large files without loading everything into memory.
/// </summary>
public class MailboxProcessingService
{
    private readonly EvermailDbContext _context;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<MailboxProcessingService> _logger;
    private const string ContainerName = "mailbox-archives";
    private const int BatchSize = 500;

    public MailboxProcessingService(
        EvermailDbContext context,
        BlobServiceClient blobServiceClient,
        ILogger<MailboxProcessingService> logger)
    {
        _context = context;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task ProcessMailboxAsync(
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        // Get mailbox from database (need tracking for updates)
        var mailbox = await _context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == mailboxId, cancellationToken);

        if (mailbox == null)
        {
            _logger.LogError("Mailbox {MailboxId} not found", mailboxId);
            throw new InvalidOperationException($"Mailbox {mailboxId} not found");
        }

        // Update status to Processing
        mailbox.Status = "Processing";
        mailbox.ProcessingStartedAt = DateTime.UtcNow;
        mailbox.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // Download blob from Azure Storage
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(mailbox.BlobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                throw new FileNotFoundException($"Blob not found: {mailbox.BlobPath}");
            }

            // Stream parse with MimeKit
            await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            
            // Get actual blob size (may differ from FileSizeBytes if file was compressed)
            var blobProperties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var totalFileSize = blobProperties.Value.ContentLength;
            
            await ParseMboxStreamAsync(stream, mailbox, totalFileSize, cancellationToken);

            // Update status to Completed
            mailbox.Status = "Completed";
            mailbox.ProcessingCompletedAt = DateTime.UtcNow;
            mailbox.UpdatedAt = DateTime.UtcNow;
            _context.Mailboxes.Update(mailbox);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Completed processing mailbox {MailboxId}: {Total} emails processed, {Failed} failed",
                mailboxId,
                mailbox.TotalEmails,
                mailbox.FailedEmails
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process mailbox {MailboxId}", mailboxId);

            // Update status to Failed
            mailbox.Status = "Failed";
            mailbox.ErrorMessage = ex.Message;
            mailbox.ProcessingCompletedAt = DateTime.UtcNow;
            mailbox.UpdatedAt = DateTime.UtcNow;
            _context.Mailboxes.Update(mailbox);
            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    private async Task ParseMboxStreamAsync(
        Stream stream,
        Mailbox mailbox,
        long totalFileSizeBytes,
        CancellationToken cancellationToken)
    {
        var parser = new MimeParser(stream, MimeFormat.Mbox);

        var batch = new List<(EmailMessage Email, MimeMessage Mime)>();
        int totalProcessed = 0;
        int successfulCount = 0;
        int failedCount = 0;

        while (!parser.IsEndOfStream)
        {
            try
            {
                var mimeMessage = await parser.ParseMessageAsync(cancellationToken);
                var emailMessage = MapToEmailMessage(mimeMessage, mailbox);
                batch.Add((emailMessage, mimeMessage));

                totalProcessed++;
                successfulCount++;

                // Save batch every 500 messages and update progress
                if (batch.Count >= BatchSize)
                {
                    // Get current stream position (bytes read so far)
                    var bytesProcessed = stream.Position;
                    
                    await SaveBatchWithAttachmentsAsync(
                        batch, 
                        mailbox, 
                        totalProcessed, 
                        successfulCount, 
                        bytesProcessed,
                        totalFileSizeBytes,
                        cancellationToken);
                    batch.Clear();

                    _logger.LogInformation(
                        "Processed {Count} messages for mailbox {MailboxId}",
                        totalProcessed,
                        mailbox.Id
                    );
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to parse message #{MessageNumber} in mailbox {MailboxId}",
                    totalProcessed + 1,
                    mailbox.Id
                );
            }
        }

        // Save remaining batch
        if (batch.Count > 0)
        {
            var finalPosition = stream.Position;
            await SaveBatchWithAttachmentsAsync(
                batch, 
                mailbox, 
                totalProcessed, 
                successfulCount,
                finalPosition,
                totalFileSizeBytes,
                cancellationToken);
        }

        // Final update of mailbox statistics - now we know the actual total
        mailbox.TotalEmails = totalProcessed;
        mailbox.ProcessedEmails = successfulCount;
        mailbox.FailedEmails = failedCount;
        _context.Mailboxes.Update(mailbox);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private EmailMessage MapToEmailMessage(MimeMessage mimeMessage, Mailbox mailbox)
    {
        // Extract text and HTML bodies
        var textBody = mimeMessage.TextBody ?? string.Empty;
        var htmlBody = mimeMessage.HtmlBody ?? string.Empty;
        
        // Create snippet (first 200 chars of text body)
        var snippet = textBody.Length > 200 
            ? textBody[..200].Replace("\r\n", " ").Replace("\n", " ").Trim() 
            : textBody.Trim();

        // Extract sender
        var fromMailbox = mimeMessage.From.Mailboxes.FirstOrDefault();
        var fromAddress = fromMailbox?.Address ?? string.Empty;
        var fromName = fromMailbox?.Name ?? string.Empty;

        // Extract recipients
        var toAddresses = mimeMessage.To.Mailboxes.Select(m => m.Address).ToList();
        var toNames = mimeMessage.To.Mailboxes.Select(m => m.Name ?? m.Address).ToList();
        var ccAddresses = mimeMessage.Cc.Mailboxes.Select(m => m.Address).ToList();
        var ccNames = mimeMessage.Cc.Mailboxes.Select(m => m.Name ?? m.Address).ToList();
        var bccAddresses = mimeMessage.Bcc.Mailboxes.Select(m => m.Address).ToList();
        var bccNames = mimeMessage.Bcc.Mailboxes.Select(m => m.Name ?? m.Address).ToList();

        return new EmailMessage
        {
            Id = Guid.NewGuid(),
            TenantId = mailbox.TenantId,
            UserId = mailbox.UserId,
            MailboxId = mailbox.Id,

            // SMTP Headers
            MessageId = mimeMessage.MessageId ?? Guid.NewGuid().ToString(),
            InReplyTo = mimeMessage.InReplyTo,
            References = mimeMessage.References?.ToString(),

            // Basic fields
            Subject = mimeMessage.Subject ?? string.Empty,
            Date = mimeMessage.Date.UtcDateTime,

            // Sender
            FromAddress = fromAddress,
            FromName = fromName,

            // Recipients (stored as JSON arrays)
            ToAddresses = JsonSerializer.Serialize(toAddresses),
            ToNames = JsonSerializer.Serialize(toNames),
            CcAddresses = JsonSerializer.Serialize(ccAddresses),
            CcNames = JsonSerializer.Serialize(ccNames),
            BccAddresses = JsonSerializer.Serialize(bccAddresses),
            BccNames = JsonSerializer.Serialize(bccNames),

            // Content
            Snippet = snippet,
            TextBody = textBody,
            HtmlBody = htmlBody,

            // Metadata
            HasAttachments = mimeMessage.Attachments.Any(),
            AttachmentCount = mimeMessage.Attachments.Count(),

            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task ProcessAttachmentsAsync(
        MimeMessage mimeMessage,
        EmailMessage emailMessage,
        Mailbox mailbox,
        CancellationToken cancellationToken)
    {
        var attachments = new List<Attachment>();

        foreach (var attachment in mimeMessage.Attachments)
        {
            try
            {
                if (attachment is MimePart mimePart)
                {
                    var attachmentEntity = new Attachment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = mailbox.TenantId,
                        EmailMessageId = emailMessage.Id,
                        FileName = mimePart.FileName ?? "attachment",
                        ContentType = mimePart.ContentType.MimeType ?? "application/octet-stream",
                        SizeBytes = 0, // Will be set after upload
                        BlobPath = string.Empty, // Will be set after upload
                        IsInline = mimePart.IsAttachment == false,
                        ContentId = mimePart.ContentId,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Upload attachment to blob storage
                    var attachmentBlobPath = $"{mailbox.TenantId}/{mailbox.Id}/{emailMessage.Id}/{attachmentEntity.Id}_{attachmentEntity.FileName}";
                    var containerClient = _blobServiceClient.GetBlobContainerClient("attachments");
                    await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                    var blobClient = containerClient.GetBlobClient(attachmentBlobPath);

                    // Decode attachment content to blob storage
                    await using var attachmentStream = new MemoryStream();
                    mimePart.Content.DecodeTo(attachmentStream);
                    attachmentStream.Position = 0;
                    await blobClient.UploadAsync(attachmentStream, overwrite: true, cancellationToken);

                    // Get blob properties to get actual size
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                    attachmentEntity.SizeBytes = properties.Value.ContentLength;
                    attachmentEntity.BlobPath = attachmentBlobPath;

                    attachments.Add(attachmentEntity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to process attachment {FileName} for email {EmailId}",
                    attachment is MimePart mp ? mp.FileName : "unknown",
                    emailMessage.Id
                );
            }
        }

        if (attachments.Any())
        {
            _context.Attachments.AddRange(attachments);
        }
    }

    private async Task SaveBatchWithAttachmentsAsync(
        List<(EmailMessage Email, MimeMessage Mime)> batch,
        Mailbox mailbox,
        int totalProcessed,
        int successfulCount,
        long bytesProcessed,
        long totalFileSizeBytes,
        CancellationToken cancellationToken)
    {
        // Save email messages first
        var emails = batch.Select(b => b.Email).ToList();
        _context.EmailMessages.AddRange(emails);
        await _context.SaveChangesAsync(cancellationToken);

        // Process attachments for saved emails
        foreach (var (email, mimeMessage) in batch)
        {
            if (mimeMessage.Attachments.Any())
            {
                await ProcessAttachmentsAsync(mimeMessage, email, mailbox, cancellationToken);
            }
        }

        // Save attachments
        await _context.SaveChangesAsync(cancellationToken);

        // Update mailbox progress
        // TotalEmails stays 0 until processing completes (we don't know total yet)
        // ProcessedEmails shows how many we've successfully processed
        // ProcessedBytes tracks actual bytes read for progress calculation
        // Progress percentage is calculated from file size: (ProcessedBytes / FileSizeBytes) * 100
        mailbox.ProcessedEmails = successfulCount;
        mailbox.ProcessedBytes = bytesProcessed;
        mailbox.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

