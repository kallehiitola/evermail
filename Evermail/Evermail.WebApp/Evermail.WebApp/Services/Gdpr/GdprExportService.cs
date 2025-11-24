using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.WebApp.Services.Gdpr;

public interface IGdprExportService
{
    Task<UserDataExport> CreateExportAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<UserDataExport?> GetExportAsync(Guid exportId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<string?> TryCreateDownloadUrlAsync(UserDataExport export, CancellationToken cancellationToken = default);
}

public sealed class GdprExportService : IGdprExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly byte[] NewLine = Encoding.UTF8.GetBytes("\n");

    private readonly EvermailDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<GdprExportService> _logger;

    public GdprExportService(
        EvermailDbContext context,
        IBlobStorageService blobStorageService,
        ILogger<GdprExportService> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<UserDataExport> CreateExportAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var export = new UserDataExport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };

        _context.UserDataExports.Add(export);
        await _context.SaveChangesAsync(cancellationToken);

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"evermail-export-{export.Id:N}.zip");

        try
        {
            await using (var archiveStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteJsonEntryAsync(archive, "profile.json", await BuildProfileAsync(tenantId, userId, cancellationToken), cancellationToken);
                await WriteJsonEntryAsync(archive, "mailboxes.json", await LoadMailboxesAsync(tenantId, cancellationToken), cancellationToken);
                await WriteJsonEntryAsync(archive, "uploads.json", await LoadUploadsAsync(tenantId, cancellationToken), cancellationToken);
                await WriteNdjsonAsync(archive, "emails.ndjson", BuildEmailStream(tenantId), cancellationToken);
                await WriteNdjsonAsync(archive, "audit-logs.ndjson", BuildAuditLogStream(tenantId), cancellationToken);

                await archiveStream.FlushAsync(cancellationToken);
                if (archiveStream.CanSeek)
                {
                    archiveStream.Seek(0, SeekOrigin.Begin);
                }

                export.FileSizeBytes = archiveStream.Length;
                var blobPath = await _blobStorageService.UploadExportAsync(tenantId, export.Id, archiveStream, cancellationToken);
                export.BlobPath = blobPath;
            }

            export.Status = "Completed";
            export.CompletedAt = DateTime.UtcNow;
            export.ExpiresAt = export.CompletedAt?.AddDays(7);
            export.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate GDPR export for tenant {TenantId} user {UserId}", tenantId, userId);
            export.Status = "Failed";
            export.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            await _context.SaveChangesAsync(cancellationToken);
            TryDeleteFile(tempFilePath);
        }

        return export;
    }

    public Task<UserDataExport?> GetExportAsync(Guid exportId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.UserDataExports
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == exportId && e.TenantId == tenantId && e.UserId == userId, cancellationToken);
    }

    public async Task<string?> TryCreateDownloadUrlAsync(UserDataExport export, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(export.BlobPath))
        {
            return null;
        }

        return await _blobStorageService.GenerateDownloadSasTokenAsync(export.BlobPath, TimeSpan.FromMinutes(15));
    }

    private async Task<object> BuildProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var tenantTask = _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.SubscriptionTier,
                t.MaxStorageGB,
                t.MaxUsers,
                t.CreatedAt,
                t.SecurityPreference
            })
            .FirstAsync(cancellationToken);

        var userTask = _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.CreatedAt,
                u.LastLoginAt,
                u.TwoFactorEnabled
            })
            .FirstAsync(cancellationToken);

        await Task.WhenAll(tenantTask, userTask);

        return new
        {
            generatedAt = DateTime.UtcNow,
            user = userTask.Result,
            tenant = tenantTask.Result
        };
    }

    private Task<List<object>> LoadMailboxesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _context.Mailboxes
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .Select(m => new
            {
                m.Id,
                DisplayName = m.DisplayName,
                m.Status,
                m.FileName,
                m.FileSizeBytes,
                m.NormalizedSizeBytes,
                m.IsClientEncrypted,
                m.EncryptionScheme,
                m.CreatedAt,
                m.UpdatedAt,
                m.ProcessingStartedAt,
                m.ProcessingCompletedAt
            } as object)
            .ToListAsync(cancellationToken);

    private Task<List<object>> LoadUploadsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _context.MailboxUploads
            .AsNoTracking()
            .Where(mu => mu.TenantId == tenantId)
            .Select(mu => new
            {
                mu.Id,
                mu.MailboxId,
                mu.FileName,
                mu.FileSizeBytes,
                mu.NormalizedSizeBytes,
                mu.Status,
                mu.IsClientEncrypted,
                mu.EncryptionScheme,
                mu.CreatedAt,
                mu.ProcessingStartedAt,
                mu.ProcessingCompletedAt,
                mu.ErrorMessage
            } as object)
            .ToListAsync(cancellationToken);

    private IAsyncEnumerable<object> BuildEmailStream(Guid tenantId) =>
        _context.EmailMessages
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Date)
            .Select(e => new
            {
                e.Id,
                e.MailboxId,
                e.MailboxUploadId,
                e.Subject,
                e.FromAddress,
                e.FromName,
                e.ToAddresses,
                e.CcAddresses,
                e.BccAddresses,
                e.Date,
                e.Snippet,
                e.TextBody,
                e.HtmlBody,
                e.HasAttachments,
                e.AttachmentCount,
                e.IsRead,
                e.Categories,
                e.Importance,
                e.Priority,
                e.CreatedAt,
                Attachments = e.Attachments.Select(a => new
                {
                    a.Id,
                    a.FileName,
                    a.ContentType,
                    a.SizeBytes,
                    a.BlobPath,
                    a.IsInline,
                    a.ContentId
                }).ToList()
            } as object)
            .AsSplitQuery()
            .AsAsyncEnumerable();

    private IAsyncEnumerable<object> BuildAuditLogStream(Guid tenantId) =>
        _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.Timestamp)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.UserId,
                a.IpAddress,
                a.UserAgent,
                a.Details,
                a.Timestamp
            } as object)
            .AsAsyncEnumerable();

    private static async Task WriteJsonEntryAsync<T>(ZipArchive archive, string entryName, T payload, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, payload, JsonOptions, cancellationToken);
    }

    private static async Task WriteNdjsonAsync<T>(ZipArchive archive, string entryName, IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            await JsonSerializer.SerializeAsync(entryStream, item, JsonOptions, cancellationToken);
            await entryStream.WriteAsync(NewLine, cancellationToken);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Swallow clean-up failures.
        }
    }
}


