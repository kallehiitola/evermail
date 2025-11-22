using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Azure.Storage.Blobs;
using Evermail.Common.Constants;
using Evermail.Domain.Entities;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.IO;

namespace Evermail.Infrastructure.Services.Archives;

public sealed class ArchivePreparationService : IArchivePreparationService
{
    private readonly ILogger<ArchivePreparationService> _logger;
    private readonly PstToMboxWriter _pstToMboxWriter;

    public ArchivePreparationService(
        ILogger<ArchivePreparationService> logger,
        PstToMboxWriter pstToMboxWriter)
    {
        _logger = logger;
        _pstToMboxWriter = pstToMboxWriter;
    }

    public async Task<ArchiveExtractionResult> PrepareAsync(
        string? sourceFormat,
        BlobClient blobClient,
        Mailbox mailbox,
        MailboxUpload upload,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var normalizedFormat = NormalizeFormat(sourceFormat, upload.FileName);

        _logger.LogInformation(
            "Preparing archive for mailbox {MailboxId} upload {UploadId}. DetectedFormat={Format} File={File}",
            mailbox.Id,
            upload.Id,
            normalizedFormat,
            upload.FileName);

        return normalizedFormat switch
        {
            EmailArchiveFormats.GoogleTakeoutZip => await PrepareZipMboxAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.MicrosoftExportZip => await PreparePstZipAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.OutlookPstZip => await PreparePstZipAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.OutlookOstZip => await PreparePstZipAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.OutlookPst => await PreparePstAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.OutlookOst => await PreparePstAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.Eml => await PrepareSingleEmlAsync(blobClient, maxUncompressedBytes, cancellationToken),
            EmailArchiveFormats.EmlZip => await PrepareEmlZipAsync(blobClient, maxUncompressedBytes, cancellationToken),
            _ => await PrepareMboxStreamAsync(blobClient, normalizedFormat, maxUncompressedBytes, cancellationToken)
        };
    }

    private async Task<ArchiveExtractionResult> PrepareMboxStreamAsync(
        BlobClient blobClient,
        string format,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        EnsureWithinPlan(properties.Value.ContentLength, maxUncompressedBytes);
        return new ArchiveExtractionResult(
            format,
            properties.Value.ContentLength,
            token => blobClient.OpenReadAsync(cancellationToken: token));
    }

    private async Task<ArchiveExtractionResult> PrepareZipMboxAsync(
        BlobClient blobClient,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var zipPath = await DownloadToTempFileAsync(blobClient, ".zip", cancellationToken);
        var mboxPath = CreateTempPath(".mbox");

        try
        {
            await using var output = CreateMboxWriter(mboxPath);
            await using var archiveStream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

            var mboxEntries = archive.Entries
                .Where(e => e.Length > 0 && e.FullName.EndsWith(".mbox", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (mboxEntries.Count == 0)
            {
                throw new InvalidOperationException("Uploaded .zip file does not contain any .mbox files.");
            }

            foreach (var entry in mboxEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Appending mbox entry {Entry} ({Size} bytes)", entry.FullName, entry.Length);

                await using var entryStream = entry.Open();
                await entryStream.CopyToAsync(output, cancellationToken);
                await output.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
        }
        catch
        {
            SafeDelete(mboxPath);
            SafeDelete(zipPath);
            throw;
        }

        var tempPaths = new List<string> { zipPath, mboxPath };
        return CreateMboxResult(mboxPath, tempPaths, maxUncompressedBytes);
    }

    private async Task<ArchiveExtractionResult> PreparePstAsync(
        BlobClient blobClient,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var pstPath = await DownloadToTempFileAsync(blobClient, ".pst", cancellationToken);
        return await ConvertPstToMboxAsync(
            pstPath,
            cancellationToken,
            maxUncompressedBytes,
            additionalTempFiles: new List<string> { pstPath });
    }

    private async Task<ArchiveExtractionResult> PreparePstZipAsync(
        BlobClient blobClient,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var zipPath = await DownloadToTempFileAsync(blobClient, ".zip", cancellationToken);
        var pstPath = CreateTempPath(".pst");

        try
        {
            await using var archiveStream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            var pstEntry = archive.Entries
                .FirstOrDefault(e => e.Length > 0 && e.FullName.EndsWith(".pst", StringComparison.OrdinalIgnoreCase));

            if (pstEntry == null)
            {
                throw new InvalidOperationException("Uploaded .zip file does not contain a .pst file.");
            }

            await using var entryStream = pstEntry.Open();
            await using var pstFile = File.Create(pstPath);
            await entryStream.CopyToAsync(pstFile, cancellationToken);
        }
        catch
        {
            SafeDelete(pstPath);
            SafeDelete(zipPath);
            throw;
        }

        return await ConvertPstToMboxAsync(
            pstPath,
            cancellationToken,
            maxUncompressedBytes,
            additionalTempFiles: new List<string> { zipPath, pstPath });
    }

    private async Task<ArchiveExtractionResult> PrepareSingleEmlAsync(
        BlobClient blobClient,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var emlPath = await DownloadToTempFileAsync(blobClient, ".eml", cancellationToken);
        var mboxPath = CreateTempPath(".mbox");

        try
        {
            await using var output = CreateMboxWriter(mboxPath);
            await using var emlStream = File.OpenRead(emlPath);
            var message = await MimeMessage.LoadAsync(emlStream, cancellationToken);
            await MboxWriter.WriteMessageAsync(output, message, cancellationToken);

            await output.FlushAsync(cancellationToken);
        }
        catch
        {
            SafeDelete(mboxPath);
            SafeDelete(emlPath);
            throw;
        }

        var tempPaths = new List<string> { emlPath, mboxPath };
        return CreateMboxResult(mboxPath, tempPaths, maxUncompressedBytes);
    }

    private async Task<ArchiveExtractionResult> PrepareEmlZipAsync(
        BlobClient blobClient,
        long maxUncompressedBytes,
        CancellationToken cancellationToken)
    {
        var zipPath = await DownloadToTempFileAsync(blobClient, ".zip", cancellationToken);
        var mboxPath = CreateTempPath(".mbox");

        try
        {
            await using var output = CreateMboxWriter(mboxPath);
            await using var archiveStream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

            var emlEntries = archive.Entries
                .Where(e => e.Length > 0 && e.FullName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (emlEntries.Count == 0)
            {
                throw new InvalidOperationException("Uploaded .zip file does not contain any .eml files.");
            }

            foreach (var entry in emlEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var entryStream = entry.Open();
                var message = await MimeMessage.LoadAsync(entryStream, cancellationToken);
                await MboxWriter.WriteMessageAsync(output, message, cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
        }
        catch
        {
            SafeDelete(mboxPath);
            SafeDelete(zipPath);
            throw;
        }

        var tempPaths = new List<string> { zipPath, mboxPath };
        return CreateMboxResult(mboxPath, tempPaths, maxUncompressedBytes);
    }

    private async Task<ArchiveExtractionResult> ConvertPstToMboxAsync(
        string pstPath,
        CancellationToken cancellationToken,
        long maxUncompressedBytes,
        IReadOnlyCollection<string>? additionalTempFiles = null)
    {
        var mboxPath = await _pstToMboxWriter.ConvertToMboxAsync(pstPath, cancellationToken);
        var tempPaths = new List<string>();
        if (additionalTempFiles is not null)
        {
            tempPaths.AddRange(additionalTempFiles);
        }
        tempPaths.Add(mboxPath);

        return CreateMboxResult(mboxPath, tempPaths, maxUncompressedBytes);
    }

    private static async Task<string> DownloadToTempFileAsync(
        BlobClient blobClient,
        string extension,
        CancellationToken cancellationToken)
    {
        var path = CreateTempPath(extension);

        try
        {
            await using var fileStream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 64,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await blobClient.DownloadToAsync(fileStream, cancellationToken);
        }
        catch
        {
            SafeDelete(path);
            throw;
        }

        return path;
    }

    private static FileStream CreateMboxWriter(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 1024 * 64,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static ArchiveExtractionResult CreateMboxResult(
        string mboxPath,
        IReadOnlyList<string> tempPaths,
        long maxUncompressedBytes)
    {
        var totalBytes = new FileInfo(mboxPath).Length;
        EnsureWithinPlan(totalBytes, maxUncompressedBytes);

        return new ArchiveExtractionResult(
            EmailArchiveFormats.Mbox,
            totalBytes,
            _ => Task.FromResult<Stream>(File.OpenRead(mboxPath)),
            tempPaths);
    }

    private static void EnsureWithinPlan(long bytes, long maxAllowedBytes)
    {
        if (maxAllowedBytes <= 0)
        {
            return;
        }

        if (bytes <= maxAllowedBytes)
        {
            return;
        }

        var actualGb = bytes / (1024d * 1024 * 1024);
        var allowedGb = maxAllowedBytes / (1024d * 1024 * 1024);
        throw new InvalidOperationException(
            $"Normalized archive is {actualGb:F2} GB which exceeds your plan limit ({allowedGb:F2} GB). Please upgrade or split the export before retrying.");
    }

    private static string NormalizeFormat(string? requestedFormat, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedFormat))
        {
            var normalized = requestedFormat.Trim().ToLowerInvariant();

            if (normalized is EmailArchiveFormats.AutoDetect or "auto")
            {
                goto ExtensionFallback;
            }

            return normalized switch
            {
                "pst" => EmailArchiveFormats.OutlookPst,
                "pst-zip" => EmailArchiveFormats.OutlookPstZip,
                "ost" => EmailArchiveFormats.OutlookOst,
                "ost-zip" => EmailArchiveFormats.OutlookOstZip,
                "zip" => EmailArchiveFormats.GoogleTakeoutZip,
                _ when IsKnownFormat(normalized) => normalized,
                _ => EmailArchiveFormats.Mbox
            };
        }

ExtensionFallback:
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".pst" => EmailArchiveFormats.OutlookPst,
            ".ost" => EmailArchiveFormats.OutlookOst,
            ".eml" => EmailArchiveFormats.Eml,
            ".zip" => EmailArchiveFormats.GoogleTakeoutZip,
            ".mbx" or ".mbox" => EmailArchiveFormats.Mbox,
            _ => EmailArchiveFormats.Mbox
        };
    }

    private static bool IsKnownFormat(string format) =>
        format is EmailArchiveFormats.Mbox
            or EmailArchiveFormats.GoogleTakeoutZip
            or EmailArchiveFormats.MicrosoftExportZip
            or EmailArchiveFormats.OutlookPst
            or EmailArchiveFormats.OutlookPstZip
            or EmailArchiveFormats.OutlookOst
            or EmailArchiveFormats.OutlookOstZip
            or EmailArchiveFormats.Eml
            or EmailArchiveFormats.EmlZip;

    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"evermail-archive-{Guid.NewGuid():N}{extension}");

    private static void SafeDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
