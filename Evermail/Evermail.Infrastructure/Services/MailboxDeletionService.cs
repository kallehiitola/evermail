using Azure.Storage.Blobs;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services;

public class MailboxDeletionService
{
    private readonly EvermailDbContext _context;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<MailboxDeletionService> _logger;

    private const string MailboxContainer = "mailbox-archives";

    public MailboxDeletionService(
        EvermailDbContext context,
        BlobServiceClient blobServiceClient,
        ILogger<MailboxDeletionService> logger)
    {
        _context = context;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task ExecuteDeletionJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.MailboxDeletionQueue
            .Include(j => j.Mailbox)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Mailbox deletion job {JobId} not found", jobId);
            return;
        }

        if (job.Status is not "Scheduled" and not "Running")
        {
            _logger.LogInformation("Mailbox deletion job {JobId} already processed ({Status})", jobId, job.Status);
            return;
        }

        if (job.ExecuteAfter > DateTime.UtcNow)
        {
            _logger.LogInformation("Mailbox deletion job {JobId} not due yet ({ExecuteAfter})", jobId, job.ExecuteAfter);
            return;
        }

        job.Status = "Running";
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var mailbox = await _context.Mailboxes
                .Include(m => m.Uploads)
                .FirstOrDefaultAsync(m => m.Id == job.MailboxId, cancellationToken);

            if (mailbox == null)
            {
                job.Status = "Failed";
                job.Notes = "Mailbox not found.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (job.DeleteUpload)
            {
                await DeleteUploadAsync(mailbox, job.MailboxUploadId, job.RequestedByUserId, !job.DeleteEmails, cancellationToken);
            }

            if (job.DeleteEmails)
            {
                await DeleteEmailsAsync(mailbox, cancellationToken);
            }

            await FinalizeMailboxStateAsync(mailbox, cancellationToken);

            job.Status = "Completed";
            job.ExecutedAt = DateTime.UtcNow;
            job.ExecutedByUserId = job.RequestedByUserId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute mailbox deletion job {JobId}", jobId);
            job.Status = "Failed";
            job.Notes = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task DeleteUploadAsync(Mailbox mailbox, Guid? uploadId, Guid requestedByUserId, bool keepEmailsFlag, CancellationToken cancellationToken)
    {
        var upload = uploadId.HasValue
            ? await _context.MailboxUploads.FirstOrDefaultAsync(u => u.Id == uploadId.Value, cancellationToken)
            : await _context.MailboxUploads
                .OrderByDescending(u => u.CreatedAt)
                .FirstOrDefaultAsync(u => u.MailboxId == mailbox.Id, cancellationToken);

        if (upload == null)
        {
            _logger.LogWarning("No upload found for mailbox {MailboxId}", mailbox.Id);
            return;
        }

        if (!string.IsNullOrWhiteSpace(upload.BlobPath))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(MailboxContainer);
            var blobClient = containerClient.GetBlobClient(upload.BlobPath);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        upload.Status = "Deleted";
        upload.DeletedAt = DateTime.UtcNow;
        upload.DeletedByUserId = requestedByUserId;
        upload.KeepEmails = keepEmailsFlag;
        upload.PurgeAfter = DateTime.UtcNow.AddDays(30);

        mailbox.UploadRemovedAt = DateTime.UtcNow;
        mailbox.UploadRemovedByUserId = requestedByUserId;
        mailbox.LatestUploadId = null;
        mailbox.BlobPath = string.Empty;
        mailbox.FileName = string.Empty;
        mailbox.FileSizeBytes = 0;
    }

    private async Task DeleteEmailsAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        await _context.EmailMessages
            .Where(e => e.MailboxId == mailbox.Id)
            .ExecuteDeleteAsync(cancellationToken);

        mailbox.TotalEmails = 0;
        mailbox.ProcessedEmails = 0;
        mailbox.FailedEmails = 0;
        mailbox.ProcessedBytes = 0;
        mailbox.Status = "Empty";
    }

    private async Task FinalizeMailboxStateAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        var hasEmails = await _context.EmailMessages.AnyAsync(e => e.MailboxId == mailbox.Id, cancellationToken);
        var uploadExists = await _context.MailboxUploads.AnyAsync(u => u.MailboxId == mailbox.Id && u.Status != "Deleted", cancellationToken);

        if (!hasEmails && !uploadExists)
        {
            mailbox.SoftDeletedAt = DateTime.UtcNow;
            mailbox.SoftDeletedByUserId = mailbox.UploadRemovedByUserId;
            mailbox.IsPendingDeletion = false;
            mailbox.Status = "Deleted";
            mailbox.PurgeAfter = null;
        }
        else
        {
            mailbox.IsPendingDeletion = hasEmails || uploadExists;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

