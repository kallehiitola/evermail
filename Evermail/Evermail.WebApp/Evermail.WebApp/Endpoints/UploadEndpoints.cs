using Evermail.Common.Constants;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Upload;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.Infrastructure.Services.Archives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Evermail.WebApp.Endpoints;

public static class UploadEndpoints
{
    private static readonly HashSet<string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        EmailArchiveFormats.Mbox,
        EmailArchiveFormats.GoogleTakeoutZip,
        EmailArchiveFormats.MicrosoftExportZip,
        EmailArchiveFormats.OutlookPst,
        EmailArchiveFormats.OutlookPstZip,
        EmailArchiveFormats.OutlookOst,
        EmailArchiveFormats.OutlookOstZip,
        EmailArchiveFormats.Eml,
        EmailArchiveFormats.EmlZip,
        EmailArchiveFormats.ClientEncrypted
    };

    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/initiate", InitiateUploadAsync)
            .RequireAuthorization();
        
        group.MapPost("/complete", CompleteUploadAsync)
            .RequireAuthorization();

        group.MapPost("/complete/zero-access", CompleteZeroAccessUploadAsync)
            .RequireAuthorization();

        group.MapPost("/encrypted/initiate", InitiateZeroAccessUploadAsync)
            .RequireAuthorization();

        group.MapPost("/encrypted/complete", CompleteZeroAccessUploadAsync)
            .RequireAuthorization();
        
        return group;
    }

    internal static async Task<IResult> InitiateUploadInternalAsync(
        InitiateUploadRequest request,
        Guid? overrideMailboxId,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }
        try
        {
            var (response, _, _) = await PrepareUploadAsync(
                request,
                overrideMailboxId ?? request.MailboxId,
                blobService,
                context,
                tenantContext);

            return Results.Ok(new ApiResponse<InitiateUploadResponse>(true, response));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, ex.Message));
        }
    }
    
    private static Task<IResult> InitiateUploadAsync(
        InitiateUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
        => InitiateUploadInternalAsync(request, null, blobService, context, tenantContext);

    private static async Task<IResult> InitiateZeroAccessUploadAsync(
        InitiateZeroAccessUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var coreRequest = new InitiateUploadRequest(
            request.FileName,
            request.FileSizeBytes,
            EmailArchiveFormats.ClientEncrypted,
            request.MailboxId,
            ClientSideEncryption: true);

        try
        {
            var (response, mailbox, _) = await PrepareUploadAsync(
                coreRequest,
                request.MailboxId,
                blobService,
                context,
                tenantContext);

            if (string.IsNullOrWhiteSpace(mailbox.ZeroAccessTokenSalt))
            {
                mailbox.ZeroAccessTokenSalt = GenerateTokenSalt();
                await context.SaveChangesAsync();
            }

            var zeroAccessResponse = new InitiateZeroAccessUploadResponse(
                response.UploadUrl,
                response.BlobPath,
                response.MailboxId,
                response.UploadId,
                response.ExpiresAt,
                mailbox.ZeroAccessTokenSalt!);

            return Results.Ok(new ApiResponse<InitiateZeroAccessUploadResponse>(true, zeroAccessResponse));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, ex.Message));
        }
    }

    private static async Task<IResult> CompleteUploadAsync(
        CompleteUploadRequest request,
        EvermailDbContext context,
        IQueueService queueService,
        IMailboxEncryptionStateService encryptionStateService,
        IArchiveFormatDetector archiveFormatDetector,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        // Validate tenant is authenticated
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        // Get mailbox and verify ownership
        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == request.MailboxId && m.TenantId == tenantContext.TenantId);
        
        if (mailbox == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Mailbox not found or you don't have permission to access it"
            ));
        }
        
        if (request.UploadId == Guid.Empty)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "UploadId is required"
            ));
        }

        var upload = await context.MailboxUploads
            .FirstOrDefaultAsync(u => u.Id == request.UploadId && u.MailboxId == mailbox.Id && u.TenantId == tenantContext.TenantId);

        if (upload == null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Upload not found or you don't have permission to access it"
            ));
        }

        if (upload.IsClientEncrypted)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Client-side encrypted uploads must be completed via /upload/complete/zero-access"));
        }

        string detectedFormat;
        try
        {
            detectedFormat = await archiveFormatDetector.DetectFormatAsync(
                upload.BlobPath,
                upload.FileName,
                cancellationToken);
        }
        catch (ArchiveFormatDetectionException ex)
        {
            upload.Status = "Failed";
            upload.ErrorMessage = ex.Message;
            mailbox.Status = "Failed";
            mailbox.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: ex.Message
            ));
        }

        mailbox.SourceFormat = detectedFormat;
        upload.SourceFormat = detectedFormat;
        upload.ErrorMessage = null;
        upload.Status = "Queued";
        upload.ProcessingStartedAt = null;
        mailbox.LatestUploadId = upload.Id;
        mailbox.Status = "Queued";
        mailbox.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var encryptionState = await encryptionStateService.CreateAsync(
            tenantContext.TenantId,
            mailbox.Id,
            upload.Id,
            tenantContext.UserId);

        // Send message to queue for background processing
        await queueService.EnqueueMailboxProcessingAsync(mailbox.Id, upload.Id, encryptionState.Id);
        
        return Results.Ok(new ApiResponse<CompleteUploadResponse>(
            Success: true,
            Data: new CompleteUploadResponse(
                mailbox.Id,
                upload.Id,
                "Queued"
            )
        ));
    }

    private static async Task<IResult> CompleteZeroAccessUploadAsync(
        CompleteZeroAccessUploadRequest request,
        EvermailDbContext context,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Scheme) ||
            string.IsNullOrWhiteSpace(request.MetadataJson) ||
            string.IsNullOrWhiteSpace(request.KeyFingerprint))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "scheme, metadata, and keyFingerprint are required"));
        }

        var mailbox = await context.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == request.MailboxId && m.TenantId == tenantContext.TenantId, cancellationToken);

        if (mailbox is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Mailbox not found or you don't have permission to access it."));
        }

        var upload = await context.MailboxUploads
            .FirstOrDefaultAsync(u => u.Id == request.UploadId && u.MailboxId == mailbox.Id && u.TenantId == tenantContext.TenantId, cancellationToken);

        if (upload is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Upload not found or you don't have permission to access it."));
        }

        if (!upload.IsClientEncrypted)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Upload is not marked for client-side encryption."));
        }

        upload.Status = "Encrypted";
        upload.ErrorMessage = null;
        upload.ProcessingStartedAt ??= DateTime.UtcNow;
        upload.ProcessingCompletedAt = DateTime.UtcNow;
        upload.FileSizeBytes = request.OriginalSizeBytes;
        upload.NormalizedSizeBytes = request.OriginalSizeBytes;
        upload.ProcessedBytes = request.CipherSizeBytes;
        upload.EncryptionScheme = request.Scheme;
        upload.EncryptionMetadataJson = request.MetadataJson;
        upload.EncryptionKeyFingerprint = request.KeyFingerprint;

        mailbox.FileSizeBytes = request.OriginalSizeBytes;
        mailbox.NormalizedSizeBytes = request.OriginalSizeBytes;
        mailbox.ProcessedBytes = request.CipherSizeBytes;
        mailbox.IsClientEncrypted = true;
        mailbox.EncryptionScheme = request.Scheme;
        mailbox.EncryptionMetadataJson = request.MetadataJson;
        mailbox.EncryptionKeyFingerprint = request.KeyFingerprint;
        mailbox.SourceFormat = EmailArchiveFormats.ClientEncrypted;
        mailbox.Status = "Encrypted";
        mailbox.ErrorMessage = null;
        mailbox.ProcessingStartedAt ??= DateTime.UtcNow;
        mailbox.ProcessingCompletedAt = DateTime.UtcNow;
        mailbox.UpdatedAt = DateTime.UtcNow;
        mailbox.LatestUploadId = upload.Id;

        await PersistZeroAccessTokensAsync(
            mailbox.Id,
            tenantContext,
            context,
            request.TokenSets,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ApiResponse<CompleteUploadResponse>(
            Success: true,
            Data: new CompleteUploadResponse(
                mailbox.Id,
                upload.Id,
                "Encrypted"
            )
        ));
    }

    private const int MaxDeterministicTokensPerType = 512;

    private static async Task PersistZeroAccessTokensAsync(
        Guid mailboxId,
        TenantContext tenantContext,
        EvermailDbContext context,
        IReadOnlyList<DeterministicTokenSetDto>? tokenSets,
        CancellationToken cancellationToken)
    {
        var existing = await context.ZeroAccessMailboxTokens
            .Where(t => t.MailboxId == mailboxId && t.TenantId == tenantContext.TenantId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            context.ZeroAccessMailboxTokens.RemoveRange(existing);
        }

        if (tokenSets is null || tokenSets.Count == 0)
        {
            return;
        }

        foreach (var set in tokenSets)
        {
            if (set?.Tokens is null || set.Tokens.Count == 0)
            {
                continue;
            }

            var tokenType = string.IsNullOrWhiteSpace(set.TokenType)
                ? "tag"
                : set.TokenType.Trim().ToLowerInvariant();

            tokenType = tokenType.Length > 50 ? tokenType[..50] : tokenType;

            var uniqueTokens = new HashSet<string>(StringComparer.Ordinal);

            foreach (var token in set.Tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var normalizedToken = token.Trim();
                if (normalizedToken.Length == 0)
                {
                    continue;
                }

                normalizedToken = normalizedToken.Length > 512 ? normalizedToken[..512] : normalizedToken;

                if (!uniqueTokens.Add(normalizedToken))
                {
                    continue;
                }

                context.ZeroAccessMailboxTokens.Add(new ZeroAccessMailboxToken
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    MailboxId = mailboxId,
                    TokenType = tokenType,
                    TokenValue = normalizedToken,
                    CreatedAt = DateTime.UtcNow
                });

                if (uniqueTokens.Count >= MaxDeterministicTokensPerType)
                {
                    break;
                }
            }
        }
    }

    private static async Task<UploadInitializationResult> PrepareUploadAsync(
        InitiateUploadRequest request,
        Guid? overrideMailboxId,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);

        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name == tenant.SubscriptionTier);

        if (plan is null)
        {
            throw new InvalidOperationException("Subscription plan not found.");
        }

        var fileSizeGb = request.FileSizeBytes / (1024.0 * 1024.0 * 1024.0);
        if (fileSizeGb > plan.MaxFileSizeGB)
        {
            throw new InvalidOperationException($"File size ({fileSizeGb:F2} GB) exceeds your plan limit ({plan.MaxFileSizeGB} GB). Please upgrade your subscription.");
        }

        var normalizedFormat = NormalizeFileType(request.FileType);
        if (!string.IsNullOrWhiteSpace(normalizedFormat) && !AllowedFileTypes.Contains(normalizedFormat))
        {
            throw new InvalidOperationException($"Invalid file type. Allowed types: {string.Join(", ", AllowedFileTypes)}");
        }

        var pendingFormat = normalizedFormat ?? EmailArchiveFormats.AutoDetect;
        var isClientEncrypted = request.ClientSideEncryption;
        if (isClientEncrypted)
        {
            pendingFormat = EmailArchiveFormats.ClientEncrypted;
        }

        var userExists = await context.Users.AnyAsync(u => u.Id == tenantContext.UserId);
        if (!userExists)
        {
            throw new InvalidOperationException($"User not found. UserId: {tenantContext.UserId}");
        }

        Mailbox mailbox;
        if (overrideMailboxId.HasValue)
        {
            mailbox = await context.Mailboxes
                .FirstOrDefaultAsync(m => m.Id == overrideMailboxId.Value && m.TenantId == tenantContext.TenantId)
                ?? throw new InvalidOperationException("Mailbox not found.");
        }
        else
        {
            mailbox = new Mailbox
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                UserId = tenantContext.UserId,
                DisplayName = request.FileName,
                FileName = request.FileName,
                FileSizeBytes = request.FileSizeBytes,
                SourceFormat = pendingFormat,
                BlobPath = string.Empty,
                Status = isClientEncrypted ? "EncryptedPending" : "Pending",
                IsClientEncrypted = isClientEncrypted,
                CreatedAt = DateTime.UtcNow
            };

            context.Mailboxes.Add(mailbox);
            await context.SaveChangesAsync();
        }

        mailbox.SourceFormat = pendingFormat;
        mailbox.IsClientEncrypted = mailbox.IsClientEncrypted || isClientEncrypted;
        mailbox.FileName = request.FileName;
        mailbox.FileSizeBytes = request.FileSizeBytes;
        mailbox.Status = isClientEncrypted ? "EncryptedPending" : "Pending";
        mailbox.UploadRemovedAt = null;
        mailbox.UploadRemovedByUserId = null;
        mailbox.IsPendingDeletion = false;
        mailbox.PurgeAfter = null;

        var upload = new MailboxUpload
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            MailboxId = mailbox.Id,
            UploadedByUserId = tenantContext.UserId,
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            SourceFormat = pendingFormat,
            BlobPath = string.Empty,
            Status = isClientEncrypted ? "EncryptedPending" : "Pending",
            CreatedAt = DateTime.UtcNow,
            IsClientEncrypted = isClientEncrypted
        };

        context.MailboxUploads.Add(upload);
        mailbox.LatestUploadId = upload.Id;
        await context.SaveChangesAsync();

        var sasInfo = await blobService.GenerateUploadSasTokenAsync(
            tenantContext.TenantId,
            mailbox.Id,
            request.FileName,
            TimeSpan.FromHours(2));

        mailbox.BlobPath = sasInfo.BlobPath;
        upload.BlobPath = sasInfo.BlobPath;
        await context.SaveChangesAsync();

        var response = new InitiateUploadResponse(
            sasInfo.SasUrl,
            sasInfo.BlobPath,
            mailbox.Id,
            upload.Id,
            sasInfo.ExpiresAt);

        return new UploadInitializationResult(response, mailbox, upload);
    }

    private static string GenerateTokenSalt()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }

    private static string? NormalizeFileType(string? fileType)
    {
        if (string.IsNullOrWhiteSpace(fileType) ||
            fileType.Equals(EmailArchiveFormats.AutoDetect, StringComparison.OrdinalIgnoreCase) ||
            fileType.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = fileType.Trim().ToLowerInvariant();

        return normalized switch
        {
            "mbx" => EmailArchiveFormats.Mbox,
            "google" or "google-zip" => EmailArchiveFormats.GoogleTakeoutZip,
            "microsoft" or "microsoft-zip" => EmailArchiveFormats.MicrosoftExportZip,
            "pst" => EmailArchiveFormats.OutlookPst,
            "pst-zip" or "outlook-zip" => EmailArchiveFormats.OutlookPstZip,
            "ost" => EmailArchiveFormats.OutlookOst,
            "ost-zip" or "outlook-ost-zip" => EmailArchiveFormats.OutlookOstZip,
            "eml" => EmailArchiveFormats.Eml,
            "eml-zip" or "maildir" => EmailArchiveFormats.EmlZip,
            _ => normalized
        };
    }

    private sealed record UploadInitializationResult(
        InitiateUploadResponse Response,
        Mailbox Mailbox,
        MailboxUpload Upload);
}

