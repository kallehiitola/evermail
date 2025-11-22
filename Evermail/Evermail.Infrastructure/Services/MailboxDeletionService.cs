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

        _logger.LogInformation(
            "Executing mailbox deletion job {JobId} (MailboxId={MailboxId}, DeleteUpload={DeleteUpload}, DeleteEmails={DeleteEmails}, ExecuteAfter={ExecuteAfter:O})",
            job.Id,
            job.MailboxId,
            job.DeleteUpload,
            job.DeleteEmails,
            job.ExecuteAfter);

        job.Status = "Running";
        await _context.SaveChangesAsync(cancellationToken);

        var forceDeletion = ShouldForceDeletion(job);
        var warnings = new List<string>();

        async Task ExecuteStepAsync(string stepName, Func<Task> action, Action? onFailure = null)
        {
            try
            {
                await action();
            }
            catch (Exception stepEx)
            {
                if (!forceDeletion)
                {
                    throw;
                }

                warnings.Add($"{stepName}: {stepEx.Message}");
                _logger.LogWarning(
                    stepEx,
                    "Forced purge step {StepName} failed for job {JobId}",
                    stepName,
                    job.Id);

                onFailure?.Invoke();
            }
        }

        Mailbox? mailbox = null;

        try
        {
            mailbox = await _context.Mailboxes
                .Include(m => m.Uploads)
                .FirstOrDefaultAsync(m => m.Id == job.MailboxId, cancellationToken);

            if (mailbox == null)
            {
                job.Status = "Failed";
                job.Notes = "Mailbox not found.";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            job.Mailbox = mailbox;

            if (job.DeleteUpload)
            {
                _logger.LogInformation("Job {JobId}: deleting uploads", job.Id);
                await ExecuteStepAsync(
                    "delete uploads",
                    () => DeleteUploadAsync(mailbox, job.MailboxUploadId, job.RequestedByUserId, !job.DeleteEmails, cancellationToken));
                _logger.LogInformation("Job {JobId}: delete uploads step completed", job.Id);
            }

            if (job.DeleteEmails)
            {
                _logger.LogInformation("Job {JobId}: deleting emails", job.Id);
                await ExecuteStepAsync(
                    "delete emails",
                    () => DeleteEmailsAsync(mailbox, cancellationToken));
                _logger.LogInformation("Job {JobId}: delete emails step completed", job.Id);
            }

            // Ensure subsequent queries observe the latest mailbox/upload state
            await _context.SaveChangesAsync(cancellationToken);

            await ExecuteStepAsync(
                "finalize mailbox state",
                () => FinalizeMailboxStateAsync(mailbox, forceDeletion, job.RequestedByUserId, cancellationToken),
                () => ForceMarkMailboxDeleted(mailbox, job.RequestedByUserId));

            if (forceDeletion && warnings.Count > 0)
            {
                ForceMarkMailboxDeleted(mailbox, job.RequestedByUserId);
            }

            job.Status = "Completed";
            job.ExecutedAt = DateTime.UtcNow;
            job.ExecutedByUserId = job.RequestedByUserId;

            if (warnings.Count > 0)
            {
                job.Notes = AppendNotes(job.Notes, $"Forced purge completed with warnings: {string.Join("; ", warnings)}");
                _logger.LogWarning("Job {JobId} completed with warnings: {Warnings}", job.Id, string.Join("; ", warnings));
            }
            else
            {
                _logger.LogInformation("Job {JobId} completed successfully", job.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (!forceDeletion)
            {
                _logger.LogError(ex, "Failed to execute mailbox deletion job {JobId}", jobId);
                job.Status = "Failed";
                job.Notes = ex.Message;
                await _context.SaveChangesAsync(cancellationToken);
                throw;
            }

            warnings.Add(ex.Message);
            _logger.LogWarning(ex, "Forced purge completed with warnings for job {JobId}", jobId);

            if (mailbox != null)
            {
                ForceMarkMailboxDeleted(mailbox, job.RequestedByUserId);
            }

            job.Status = "Completed";
            job.ExecutedAt = DateTime.UtcNow;
            job.ExecutedByUserId = job.RequestedByUserId;
            job.Notes = AppendNotes(job.Notes, $"Forced purge completed with warnings: {string.Join("; ", warnings)}");
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DeleteUploadAsync(Mailbox mailbox, Guid? uploadId, Guid requestedByUserId, bool keepEmailsFlag, CancellationToken cancellationToken)
    {
        var query = _context.MailboxUploads.Where(u => u.MailboxId == mailbox.Id);
        List<MailboxUpload> uploads;

        if (uploadId.HasValue)
        {
            uploads = await query
                .Where(u => u.Id == uploadId.Value)
                .ToListAsync(cancellationToken);
        }
        else
        {
            uploads = await query
                .Where(u => u.Status == null || u.Status != "Deleted")
                .ToListAsync(cancellationToken);
        }

        if (uploads.Count == 0)
        {
            _logger.LogWarning("No uploads found for mailbox {MailboxId} (UploadId: {UploadId})", mailbox.Id, uploadId);
            return;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(MailboxContainer);

        _logger.LogInformation(
            "Deleting {Count} uploads for mailbox {MailboxId} (RequestedBy={RequestedBy})",
            uploads.Count,
            mailbox.Id,
            requestedByUserId);

        foreach (var upload in uploads)
        {
            if (!string.IsNullOrWhiteSpace(upload.BlobPath))
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(upload.BlobPath);
                    await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete blob {BlobPath} for upload {UploadId}",
                        upload.BlobPath,
                        upload.Id);
                }
            }

            upload.Status = "Deleted";
            upload.DeletedAt = DateTime.UtcNow;
            upload.DeletedByUserId = requestedByUserId;
            upload.KeepEmails = keepEmailsFlag;
            upload.PurgeAfter = DateTime.UtcNow.AddDays(30);
        }

        var deletedUploadIds = uploads.Select(u => u.Id).ToList();
        var hasRemainingUploads = await _context.MailboxUploads
            .AnyAsync(
                u => u.MailboxId == mailbox.Id &&
                     (u.Status == null || u.Status != "Deleted") &&
                     !deletedUploadIds.Contains(u.Id),
                cancellationToken);

        if (!hasRemainingUploads)
        {
            mailbox.UploadRemovedAt = DateTime.UtcNow;
            mailbox.UploadRemovedByUserId = requestedByUserId;
            mailbox.LatestUploadId = null;
            mailbox.BlobPath = string.Empty;
            mailbox.FileName = string.Empty;
            mailbox.FileSizeBytes = 0;
            mailbox.NormalizedSizeBytes = 0;

            _logger.LogInformation(
                "Mailbox {MailboxId}: all uploads removed; cleared blob metadata",
                mailbox.Id);
        }
    }

    private async Task DeleteEmailsAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        var emailQuery = _context.EmailMessages
            .Where(e => e.MailboxId == mailbox.Id);

        var deletedCount = await emailQuery.CountAsync(cancellationToken);
        await emailQuery.ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Mailbox {MailboxId}: deleted {Count} email rows",
            mailbox.Id,
            deletedCount);

        mailbox.TotalEmails = 0;
        mailbox.ProcessedEmails = 0;
        mailbox.FailedEmails = 0;
        mailbox.ProcessedBytes = 0;
        mailbox.Status = "Empty";
    }

    private async Task FinalizeMailboxStateAsync(
        Mailbox mailbox,
        bool forceDeletion,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        if (forceDeletion)
        {
            ForceMarkMailboxDeleted(mailbox, requestedByUserId);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Mailbox {MailboxId}: force deletion finalized", mailbox.Id);
            return;
        }

        var hasEmails = await _context.EmailMessages.AnyAsync(e => e.MailboxId == mailbox.Id, cancellationToken);
        var uploadExists = await _context.MailboxUploads
            .AnyAsync(u => u.MailboxId == mailbox.Id && (u.Status == null || u.Status != "Deleted"), cancellationToken);

        mailbox.IsPendingDeletion = false;
        mailbox.PurgeAfter = null;

        if (!hasEmails && !uploadExists)
        {
            mailbox.SoftDeletedAt = DateTime.UtcNow;
            mailbox.SoftDeletedByUserId = mailbox.UploadRemovedByUserId;
            mailbox.Status = "Deleted";
        }
        else if (!hasEmails && uploadExists)
        {
            mailbox.Status = "Empty";
        }
        else if (hasEmails && mailbox.Status == "Deleted")
        {
            mailbox.Status = "Completed";
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Mailbox {MailboxId}: finalize complete (HasEmails={HasEmails}, UploadExists={UploadExists}, Status={Status})",
            mailbox.Id,
            hasEmails,
            uploadExists,
            mailbox.Status);
    }

    private static bool ShouldForceDeletion(MailboxDeletionQueue job) =>
        job.ExecuteAfter <= job.RequestedAt.AddMinutes(1);

    private static void ForceMarkMailboxDeleted(Mailbox mailbox, Guid requestedByUserId)
    {
        var utcNow = DateTime.UtcNow;

        mailbox.UploadRemovedAt = utcNow;
        mailbox.UploadRemovedByUserId = requestedByUserId;
        mailbox.LatestUploadId = null;
        mailbox.BlobPath = string.Empty;
        mailbox.FileName = string.Empty;
        mailbox.FileSizeBytes = 0;
        mailbox.NormalizedSizeBytes = 0;

        mailbox.TotalEmails = 0;
        mailbox.ProcessedEmails = 0;
        mailbox.FailedEmails = 0;
        mailbox.ProcessedBytes = 0;

        mailbox.IsPendingDeletion = false;
        mailbox.PurgeAfter = null;
        mailbox.SoftDeletedAt = utcNow;
        mailbox.SoftDeletedByUserId = requestedByUserId;
        mailbox.Status = "Deleted";
        mailbox.UpdatedAt = utcNow;
    }

    private static string AppendNotes(string? existing, string addition) =>
        string.IsNullOrWhiteSpace(existing)
            ? addition
            : $"{existing}{Environment.NewLine}{addition}";
}

