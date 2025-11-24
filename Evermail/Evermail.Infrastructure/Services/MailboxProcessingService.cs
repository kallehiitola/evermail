using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services.Archives;
using Evermail.Infrastructure.Services.Encryption;
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
    private readonly IMailboxEncryptionStateService _encryptionStateService;
    private readonly IKeyWrappingService _keyWrappingService;
    private readonly IArchivePreparationService _archivePreparationService;
    private const string ContainerName = "mailbox-archives";
    private const int BatchSize = 500;

    public MailboxProcessingService(
        EvermailDbContext context,
        BlobServiceClient blobServiceClient,
        ILogger<MailboxProcessingService> logger,
        IMailboxEncryptionStateService encryptionStateService,
        IKeyWrappingService keyWrappingService,
        IArchivePreparationService archivePreparationService)
    {
        _context = context;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _encryptionStateService = encryptionStateService;
        _keyWrappingService = keyWrappingService;
        _archivePreparationService = archivePreparationService;
    }

    public async Task ProcessMailboxAsync(
        Guid mailboxId,
        Guid mailboxUploadId,
        Guid? mailboxEncryptionStateId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting ingestion for mailbox {MailboxId} upload {UploadId}",
            mailboxId,
            mailboxUploadId);

        // Get mailbox from database (need tracking for updates)
        var mailbox = await _context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == mailboxId, cancellationToken);

        if (mailbox == null)
        {
            _logger.LogError("Mailbox {MailboxId} not found", mailboxId);
            throw new InvalidOperationException($"Mailbox {mailboxId} not found");
        }

        var upload = await _context.MailboxUploads
            .FirstOrDefaultAsync(u => u.Id == mailboxUploadId && u.MailboxId == mailboxId, cancellationToken);

        if (upload == null)
        {
            _logger.LogError("Mailbox upload {UploadId} not found for mailbox {MailboxId}", mailboxUploadId, mailboxId);
            throw new InvalidOperationException($"Mailbox upload {mailboxUploadId} not found");
        }

        if (mailbox.IsClientEncrypted || upload.IsClientEncrypted)
        {
            _logger.LogInformation(
                "Mailbox {MailboxId} upload {UploadId} is client-side encrypted. Skipping server-side ingestion.",
                mailboxId,
                mailboxUploadId);

            var now = DateTime.UtcNow;
            mailbox.Status = "Encrypted";
            mailbox.ErrorMessage = null;
            mailbox.ProcessingStartedAt ??= now;
            mailbox.ProcessingCompletedAt ??= now;
            mailbox.UpdatedAt = now;

            upload.Status = "Encrypted";
            upload.ErrorMessage = null;
            upload.ProcessingStartedAt ??= now;
            upload.ProcessingCompletedAt ??= now;

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        // Update status to Processing
        mailbox.Status = "Processing";
        mailbox.ProcessingStartedAt = DateTime.UtcNow;
        mailbox.UpdatedAt = DateTime.UtcNow;
        mailbox.LatestUploadId = mailboxUploadId;

        upload.Status = "Processing";
        upload.ProcessingStartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        byte[]? decryptedDek = null;
        string? unwrapRequestId = null;
        string? unwrapMetadata = null;

        try
        {
            var maxFileSizeGb = await (
                from tenant in _context.Tenants.AsNoTracking()
                where tenant.Id == mailbox.TenantId
                join plan in _context.SubscriptionPlans.AsNoTracking()
                    on tenant.SubscriptionTier equals plan.Name into planGroup
                from plan in planGroup.DefaultIfEmpty()
                select plan != null ? (int?)plan.MaxFileSizeGB : null
            ).FirstOrDefaultAsync(cancellationToken) ?? 1;

            if (maxFileSizeGb <= 0)
            {
                maxFileSizeGb = 1;
            }

            var maxUncompressedBytes = maxFileSizeGb * 1024L * 1024L * 1024L;

            if (mailboxEncryptionStateId.HasValue)
            {
                var encryptionState = await _context.MailboxEncryptionStates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == mailboxEncryptionStateId.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Encryption state not found for upload.");

                var tenantSettings = await _context.TenantEncryptionSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == mailbox.TenantId, cancellationToken)
                    ?? throw new InvalidOperationException("Tenant encryption settings missing.");

                var unwrapResult = await _keyWrappingService.UnwrapDataKeyAsync(
                    tenantSettings,
                    encryptionState.WrappedDek,
                    cancellationToken);

                decryptedDek = unwrapResult.PlaintextDek;
                unwrapRequestId = unwrapResult.ProviderRequestId;
                unwrapMetadata = unwrapResult.ProviderMetadataJson;
            }

            // Download blob from Azure Storage
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(mailbox.BlobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                throw new FileNotFoundException($"Blob not found: {mailbox.BlobPath}");
            }

            _logger.LogInformation(
                "Mailbox {MailboxId}: streaming blob {BlobPath}",
                mailbox.Id,
                mailbox.BlobPath);

            var sourceFormat = upload.SourceFormat ?? mailbox.SourceFormat;
            await using var preparedArchive = await _archivePreparationService.PrepareAsync(
                sourceFormat,
                blobClient,
                mailbox,
                upload,
                maxUncompressedBytes,
                cancellationToken);

            await using var preparedStream = await preparedArchive.OpenReadAsync(cancellationToken);
            var totalFileSize = preparedArchive.TotalBytes;

            mailbox.NormalizedSizeBytes = totalFileSize;
            upload.NormalizedSizeBytes = totalFileSize;

            _logger.LogInformation(
                "Mailbox {MailboxId}: normalized archive to {Format} ({SizeBytes} bytes)",
                mailbox.Id,
                preparedArchive.Format,
                totalFileSize);

            var existingHashes = await _context.EmailMessages
                .Where(e => e.MailboxId == mailboxId && e.ContentHash != null)
                .Select(e => e.ContentHash!)
                .ToListAsync(cancellationToken);

            var hashSet = new HashSet<string>(existingHashes.Select(Convert.ToHexString), StringComparer.Ordinal);

            if (hashSet.Count > 0)
            {
                _logger.LogInformation(
                    "Mailbox {MailboxId}: loaded {HashCount} existing content hashes",
                    mailbox.Id,
                    hashSet.Count);
            }

            await ParseMboxStreamAsync(preparedStream, mailbox, upload, totalFileSize, hashSet, cancellationToken);

            // Update status to Completed
            mailbox.Status = "Completed";
            mailbox.ProcessingCompletedAt = DateTime.UtcNow;
            mailbox.UpdatedAt = DateTime.UtcNow;

            upload.Status = "Completed";
            upload.ProcessingCompletedAt = DateTime.UtcNow;
            upload.TotalEmails = mailbox.TotalEmails;
            upload.ProcessedEmails = mailbox.ProcessedEmails;
            upload.FailedEmails = mailbox.FailedEmails;

            await _context.SaveChangesAsync(cancellationToken);

            if (mailboxEncryptionStateId.HasValue)
            {
                await _encryptionStateService.RecordKeyReleaseAsync(
                    mailboxEncryptionStateId.Value,
                    "ingestion-worker",
                    unwrapRequestId,
                    unwrapMetadata,
                    cancellationToken);
            }

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

            upload.Status = "Failed";
            upload.ErrorMessage = ex.Message;
            upload.ProcessingCompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
        finally
        {
            if (decryptedDek is not null)
            {
                CryptographicOperations.ZeroMemory(decryptedDek);
            }
        }
    }

    private async Task ParseMboxStreamAsync(
        Stream stream,
        Mailbox mailbox,
        MailboxUpload upload,
        long totalFileSizeBytes,
        HashSet<string> existingHashes,
        CancellationToken cancellationToken)
    {
        var parser = new MimeParser(stream, MimeFormat.Mbox);

        var batch = new List<EmailBatchItem>();
        var threadCache = new Dictionary<string, EmailThread>(StringComparer.OrdinalIgnoreCase);
        int totalProcessed = 0;
        int successfulCount = 0;
        int failedCount = 0;

        while (!parser.IsEndOfStream)
        {
            try
            {
                var mimeMessage = await parser.ParseMessageAsync(cancellationToken);
                var emailMessage = MapToEmailMessage(mimeMessage, mailbox, upload);
                var contentHash = ComputeContentHash(mailbox.Id, mimeMessage, emailMessage);
                var hashKey = Convert.ToHexString(contentHash);

                if (existingHashes.Contains(hashKey))
                {
                    continue;
                }

                existingHashes.Add(hashKey);
                emailMessage.ContentHash = contentHash;

                var recipients = BuildRecipientEntities(mimeMessage, emailMessage, mailbox);
                var participantSet = BuildParticipantSet(emailMessage, recipients);
                var conversationKey = DetermineConversationKey(mimeMessage, emailMessage);
                var thread = await GetOrCreateThreadAsync(
                    conversationKey,
                    mimeMessage,
                    emailMessage,
                    mailbox,
                    participantSet,
                    threadCache,
                    cancellationToken);

                emailMessage.ConversationId = thread.Id;
                emailMessage.ConversationKey = conversationKey;
                emailMessage.ThreadDepth = CalculateThreadDepth(mimeMessage);

                batch.Add(new EmailBatchItem(emailMessage, mimeMessage, recipients, thread));

                totalProcessed++;
                successfulCount++;

                if (batch.Count >= BatchSize)
                {
                    var bytesProcessed = stream.Position;

                    await SaveBatchWithAttachmentsAsync(
                        batch,
                        mailbox,
                        upload,
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

        if (batch.Count > 0)
        {
            var finalPosition = stream.Position;
            await SaveBatchWithAttachmentsAsync(
                batch,
                mailbox,
                upload,
                totalProcessed,
                successfulCount,
                finalPosition,
                totalFileSizeBytes,
                cancellationToken);
        }

        mailbox.TotalEmails = totalProcessed;
        mailbox.ProcessedEmails = successfulCount;
        mailbox.FailedEmails = failedCount;
        _context.Mailboxes.Update(mailbox);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private EmailMessage MapToEmailMessage(MimeMessage mimeMessage, Mailbox mailbox, MailboxUpload upload)
    {
        var textBody = mimeMessage.TextBody ?? string.Empty;
        var htmlBody = mimeMessage.HtmlBody ?? string.Empty;

        var snippet = textBody.Length > 200
            ? textBody[..200].Replace("\r\n", " ").Replace("\n", " ").Trim()
            : textBody.Trim();

        var fromMailbox = mimeMessage.From.Mailboxes.FirstOrDefault();
        var fromAddress = fromMailbox?.Address ?? string.Empty;
        var fromName = fromMailbox?.Name ?? string.Empty;

        var replyToMailbox = mimeMessage.ReplyTo.Mailboxes.FirstOrDefault();
        var senderMailbox = mimeMessage.Sender;

        var toAddresses = mimeMessage.To.Mailboxes.Select(m => m.Address).ToList();
        var toNames = mimeMessage.To.Mailboxes.Select(m => m.Name ?? m.Address).ToList();
        var ccAddresses = mimeMessage.Cc.Mailboxes.Select(m => m.Address).ToList();
        var ccNames = mimeMessage.Cc.Mailboxes.Select(m => m.Name ?? m.Address).ToList();
        var bccAddresses = mimeMessage.Bcc.Mailboxes.Select(m => m.Address).ToList();
        var bccNames = mimeMessage.Bcc.Mailboxes.Select(m => m.Name ?? m.Address).ToList();

        var recipientsSearchTokens = new List<string>();
        recipientsSearchTokens.AddRange(toAddresses);
        recipientsSearchTokens.AddRange(toNames);
        recipientsSearchTokens.AddRange(ccAddresses);
        recipientsSearchTokens.AddRange(ccNames);
        recipientsSearchTokens.AddRange(bccAddresses);
        recipientsSearchTokens.AddRange(bccNames);
        if (!string.IsNullOrWhiteSpace(replyToMailbox?.Address))
        {
            recipientsSearchTokens.Add(replyToMailbox!.Address);
        }
        if (!string.IsNullOrWhiteSpace(replyToMailbox?.Name))
        {
            recipientsSearchTokens.Add(replyToMailbox!.Name!);
        }
        if (!string.IsNullOrWhiteSpace(senderMailbox?.Address))
        {
            recipientsSearchTokens.Add(senderMailbox!.Address);
        }
        if (!string.IsNullOrWhiteSpace(senderMailbox?.Name))
        {
            recipientsSearchTokens.Add(senderMailbox!.Name!);
        }

        var searchVector = BuildSearchVector(
            mimeMessage.Subject,
            fromAddress,
            fromName,
            recipientsSearchTokens,
            textBody,
            htmlBody);

        return new EmailMessage
        {
            Id = Guid.NewGuid(),
            TenantId = mailbox.TenantId,
            UserId = mailbox.UserId,
            MailboxId = mailbox.Id,

            MessageId = mimeMessage.MessageId ?? Guid.NewGuid().ToString(),
            InReplyTo = mimeMessage.InReplyTo,
            References = mimeMessage.References?.ToString(),

            Subject = mimeMessage.Subject ?? string.Empty,
            Date = mimeMessage.Date.UtcDateTime,

            FromAddress = fromAddress,
            FromName = fromName,
            ReplyToAddress = replyToMailbox?.Address,
            SenderAddress = senderMailbox?.Address,
            SenderName = senderMailbox?.Name,
            ReturnPath = mimeMessage.Headers["Return-Path"],
            ListId = mimeMessage.Headers["List-Id"],
            ThreadTopic = mimeMessage.Headers["Thread-Topic"],
            Importance = mimeMessage.Headers["Importance"] ?? mimeMessage.Headers["X-Importance"],
            Priority = mimeMessage.Headers["X-Priority"] ?? mimeMessage.Headers["Priority"],
            Categories = mimeMessage.Headers["Categories"],

            ToAddresses = JsonSerializer.Serialize(toAddresses),
            ToNames = JsonSerializer.Serialize(toNames),
            CcAddresses = JsonSerializer.Serialize(ccAddresses),
            CcNames = JsonSerializer.Serialize(ccNames),
            BccAddresses = JsonSerializer.Serialize(bccAddresses),
            BccNames = JsonSerializer.Serialize(bccNames),
            RecipientsSearch = string.Join(' ', recipientsSearchTokens.Where(t => !string.IsNullOrWhiteSpace(t))),
            SearchVector = searchVector,

            Snippet = snippet,
            TextBody = textBody,
            HtmlBody = htmlBody,

            HasAttachments = mimeMessage.Attachments.Any(),
            AttachmentCount = mimeMessage.Attachments.Count(),

            CreatedAt = DateTime.UtcNow,
            MailboxUploadId = upload.Id
        };
    }

    private const int MaxSearchVectorLength = 64000;
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);

    private static string BuildSearchVector(
        string? subject,
        string fromAddress,
        string? fromName,
        IEnumerable<string> recipientTokens,
        string textBody,
        string htmlBody)
    {
        var builder = new StringBuilder();

        AppendWithLimit(builder, subject);
        AppendWithLimit(builder, fromName);
        AppendWithLimit(builder, fromAddress);

        foreach (var token in recipientTokens)
        {
            AppendWithLimit(builder, token);
        }

        AppendWithLimit(builder, textBody);

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var stripped = StripHtmlToText(htmlBody);
            AppendWithLimit(builder, stripped);
        }

        return builder.ToString();
    }

    private static void AppendWithLimit(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || builder.Length >= MaxSearchVectorLength)
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        var remaining = MaxSearchVectorLength - builder.Length;
        if (remaining <= 0)
        {
            return;
        }

        if (trimmed.Length > remaining)
        {
            builder.Append(trimmed.AsSpan(0, remaining));
        }
        else
        {
            builder.Append(trimmed);
        }
    }

    private static string StripHtmlToText(string? html) =>
        string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : HtmlTagRegex.Replace(html, " ");

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
        List<EmailBatchItem> batch,
        Mailbox mailbox,
        MailboxUpload upload,
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
        foreach (var item in batch)
        {
            if (item.Mime.Attachments.Any())
            {
                await ProcessAttachmentsAsync(item.Mime, item.Email, mailbox, cancellationToken);
            }
        }

        // Save attachments
        await _context.SaveChangesAsync(cancellationToken);

        var recipientRows = batch.SelectMany(b => b.Recipients).ToList();
        if (recipientRows.Count > 0)
        {
            _context.EmailRecipients.AddRange(recipientRows);
        }

        UpdateThreadStats(batch);

        await _context.SaveChangesAsync(cancellationToken);

        mailbox.ProcessedEmails = successfulCount;
        mailbox.ProcessedBytes = bytesProcessed;
        mailbox.UpdatedAt = DateTime.UtcNow;
        upload.ProcessedEmails = successfulCount;
        upload.TotalEmails = totalProcessed;
        upload.ProcessedBytes = bytesProcessed;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Mailbox {MailboxId}: flushed batch ({BatchSize} messages). TotalProcessed={TotalProcessed}, Successful={Successful}, Bytes={BytesProcessed}/{TotalBytes}",
            mailbox.Id,
            batch.Count,
            totalProcessed,
            successfulCount,
            bytesProcessed,
            totalFileSizeBytes);
    }

    private List<EmailRecipient> BuildRecipientEntities(
        MimeMessage mimeMessage,
        EmailMessage emailMessage,
        Mailbox mailbox)
    {
        var recipients = new List<EmailRecipient>();

        AddRecipientEntities(recipients, emailMessage.Id, mailbox, mimeMessage.To.Mailboxes, "To");
        AddRecipientEntities(recipients, emailMessage.Id, mailbox, mimeMessage.Cc.Mailboxes, "Cc");
        AddRecipientEntities(recipients, emailMessage.Id, mailbox, mimeMessage.Bcc.Mailboxes, "Bcc");

        if (mimeMessage.ReplyTo?.Mailboxes.Any() == true)
        {
            AddRecipientEntities(recipients, emailMessage.Id, mailbox, mimeMessage.ReplyTo.Mailboxes, "ReplyTo");
        }

        if (mimeMessage.Sender != null)
        {
            AddRecipientEntities(recipients, emailMessage.Id, mailbox, new[] { mimeMessage.Sender }, "Sender");
        }

        return recipients;
    }

    private static void AddRecipientEntities(
        ICollection<EmailRecipient> recipients,
        Guid emailMessageId,
        Mailbox mailbox,
        IEnumerable<MailboxAddress> addresses,
        string type)
    {
        foreach (var address in addresses)
        {
            if (string.IsNullOrWhiteSpace(address.Address))
            {
                continue;
            }

            recipients.Add(new EmailRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = mailbox.TenantId,
                EmailMessageId = emailMessageId,
                RecipientType = type,
                Address = address.Address,
                DisplayName = string.IsNullOrWhiteSpace(address.Name) ? null : address.Name,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static HashSet<string> BuildParticipantSet(
        EmailMessage emailMessage,
        List<EmailRecipient> recipients)
    {
        var participants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(emailMessage.FromAddress))
        {
            participants.Add(emailMessage.FromAddress);
        }

        if (!string.IsNullOrWhiteSpace(emailMessage.SenderAddress))
        {
            participants.Add(emailMessage.SenderAddress!);
        }

        if (!string.IsNullOrWhiteSpace(emailMessage.ReplyToAddress))
        {
            participants.Add(emailMessage.ReplyToAddress!);
        }

        foreach (var recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Address))
            {
                participants.Add(recipient.Address);
            }
        }

        return participants;
    }

    private static string DetermineConversationKey(MimeMessage mimeMessage, EmailMessage email)
    {
        if (mimeMessage.References?.Count > 0)
        {
            return NormalizeMessageId(mimeMessage.References[0]);
        }

        if (!string.IsNullOrWhiteSpace(mimeMessage.InReplyTo))
        {
            return NormalizeMessageId(mimeMessage.InReplyTo);
        }

        if (!string.IsNullOrWhiteSpace(mimeMessage.MessageId))
        {
            return NormalizeMessageId(mimeMessage.MessageId);
        }

        return email.Id.ToString("N");
    }

    private async Task<EmailThread> GetOrCreateThreadAsync(
        string conversationKey,
        MimeMessage mimeMessage,
        EmailMessage email,
        Mailbox mailbox,
        HashSet<string> participants,
        Dictionary<string, EmailThread> threadCache,
        CancellationToken cancellationToken)
    {
        if (threadCache.TryGetValue(conversationKey, out var cachedThread))
        {
            return cachedThread;
        }

        var thread = await _context.EmailThreads
            .FirstOrDefaultAsync(t => t.TenantId == mailbox.TenantId && t.ConversationKey == conversationKey, cancellationToken);

        if (thread == null)
        {
            thread = new EmailThread
            {
                Id = Guid.NewGuid(),
                TenantId = mailbox.TenantId,
                ConversationKey = conversationKey,
                RootMessageId = mimeMessage.MessageId ?? email.MessageId,
                Subject = email.Subject,
                FirstMessageDate = email.Date,
                LastMessageDate = email.Date,
                MessageCount = 0,
                ParticipantsSummary = SerializeParticipants(participants),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.EmailThreads.AddAsync(thread, cancellationToken);
        }

        threadCache[conversationKey] = thread;
        return thread;
    }

    private static void UpdateThreadStats(IEnumerable<EmailBatchItem> batch)
    {
        var now = DateTime.UtcNow;

        foreach (var group in batch.GroupBy(b => b.Thread.Id))
        {
            var thread = group.First().Thread;
            var messageCountDelta = group.Count();
            thread.MessageCount += messageCountDelta;

            var maxDate = group.Max(item => item.Email.Date);
            var minDate = group.Min(item => item.Email.Date);

            if (thread.FirstMessageDate == default || minDate < thread.FirstMessageDate)
            {
                thread.FirstMessageDate = minDate;
            }

            if (maxDate > thread.LastMessageDate)
            {
                thread.LastMessageDate = maxDate;
            }

            var participants = DeserializeParticipants(thread.ParticipantsSummary);
            foreach (var item in group)
            {
                var participantSet = BuildParticipantSet(item.Email, item.Recipients);
                foreach (var participant in participantSet)
                {
                    participants.Add(participant);
                }
            }

            thread.ParticipantsSummary = SerializeParticipants(participants);
            thread.UpdatedAt = now;
        }
    }

    private static HashSet<string> DeserializeParticipants(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return new HashSet<string>(values.Where(v => !string.IsNullOrWhiteSpace(v)), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string SerializeParticipants(HashSet<string> participants)
        => JsonSerializer.Serialize(participants.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static int CalculateThreadDepth(MimeMessage mimeMessage)
    {
        if (mimeMessage.References?.Count > 0)
        {
            return mimeMessage.References.Count;
        }

        return string.IsNullOrWhiteSpace(mimeMessage.InReplyTo) ? 0 : 1;
    }

    private static string NormalizeMessageId(string messageId)
    {
        return messageId
            .Trim()
            .Trim('<', '>')
            .ToLowerInvariant();
    }

    private sealed record EmailBatchItem(
        EmailMessage Email,
        MimeMessage Mime,
        List<EmailRecipient> Recipients,
        EmailThread Thread);

    private static byte[] ComputeContentHash(Guid mailboxId, MimeMessage mimeMessage, EmailMessage email)
    {
        var builder = new StringBuilder();
        builder.Append(mailboxId);
        builder.Append(mimeMessage.MessageId ?? string.Empty);
        builder.Append(mimeMessage.Subject ?? string.Empty);
        builder.Append(mimeMessage.Date.UtcDateTime.ToString("O"));
        builder.Append(email.FromAddress);
        builder.Append(email.Snippet ?? string.Empty);
        builder.Append(email.TextBody ?? string.Empty);
        builder.Append(email.HtmlBody ?? string.Empty);

        return SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    }
}

