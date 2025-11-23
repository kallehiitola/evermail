using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Evermail.Common.Constants;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services.Archives;

public sealed class ArchiveFormatDetector : IArchiveFormatDetector
{
    private const string ContainerName = "mailbox-archives";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ArchiveFormatDetector> _logger;

    public ArchiveFormatDetector(
        BlobServiceClient blobServiceClient,
        ILogger<ArchiveFormatDetector> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string> DetectFormatAsync(
        string blobPath,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("Blob path is required.", nameof(blobPath));
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new ArchiveFormatDetectionException("We couldn't find the uploaded file. Please try uploading again.");
        }

        var extensionHint = GuessFromFileName(originalFileName);
        if (!string.IsNullOrWhiteSpace(extensionHint) &&
            extensionHint is not EmailArchiveFormats.Mbox and not EmailArchiveFormats.Eml)
        {
            _logger.LogInformation("Detected archive format {Format} from extension for blob {BlobPath}", extensionHint, blobPath);
            return extensionHint;
        }

        await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

        if (IsZipStream(stream))
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var zipFormat = DetectZipEntries(archive);
            if (zipFormat is not null)
            {
                _logger.LogInformation("Detected archive format {Format} from ZIP contents for blob {BlobPath}", zipFormat, blobPath);
                return zipFormat;
            }
        }
        else
        {
            stream.Seek(0, SeekOrigin.Begin);
            if (LooksLikePst(stream))
            {
                var resolved = extensionHint == EmailArchiveFormats.OutlookOst
                    ? EmailArchiveFormats.OutlookOst
                    : EmailArchiveFormats.OutlookPst;
                _logger.LogInformation("Detected archive format {Format} from PST header for blob {BlobPath}", resolved, blobPath);
                return resolved;
            }

            stream.Seek(0, SeekOrigin.Begin);
            if (LooksLikeMbox(stream))
            {
                _logger.LogInformation("Detected archive format {Format} from mbox content for blob {BlobPath}", EmailArchiveFormats.Mbox, blobPath);
                return EmailArchiveFormats.Mbox;
            }

            stream.Seek(0, SeekOrigin.Begin);
            if (LooksLikeEml(stream))
            {
                _logger.LogInformation("Detected archive format {Format} from EML content for blob {BlobPath}", EmailArchiveFormats.Eml, blobPath);
                return EmailArchiveFormats.Eml;
            }
        }

        throw new ArchiveFormatDetectionException(
            "We couldn't recognize this archive. Upload a .mbox, .pst, .ost, or .eml file, or a ZIP containing those formats.");
    }

    private static string? GuessFromFileName(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty)?.ToLowerInvariant();

        return extension switch
        {
            ".pst" => EmailArchiveFormats.OutlookPst,
            ".ost" => EmailArchiveFormats.OutlookOst,
            ".mbox" => EmailArchiveFormats.Mbox,
            ".mbx" => EmailArchiveFormats.Mbox,
            ".eml" => EmailArchiveFormats.Eml,
            ".zip" => null,
            _ => null
        };
    }

    private static bool IsZipStream(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[4];
        var bytesRead = stream.Read(signature);
        stream.Seek(-bytesRead, SeekOrigin.Current);

        return bytesRead == 4 &&
               signature[0] == 0x50 &&
               signature[1] == 0x4B &&
               signature[2] == 0x03 &&
               signature[3] == 0x04;
    }

    private static string? DetectZipEntries(ZipArchive archive)
    {
        var entries = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Take(256)
            .ToList();

        if (entries.Any(e => e.FullName.EndsWith(".pst", StringComparison.OrdinalIgnoreCase)))
        {
            return EmailArchiveFormats.MicrosoftExportZip;
        }

        if (entries.Any(e => e.FullName.EndsWith(".ost", StringComparison.OrdinalIgnoreCase)))
        {
            return EmailArchiveFormats.OutlookOstZip;
        }

        if (entries.Any(e => e.FullName.EndsWith(".mbox", StringComparison.OrdinalIgnoreCase) ||
                             e.FullName.EndsWith(".mbx", StringComparison.OrdinalIgnoreCase)))
        {
            return EmailArchiveFormats.GoogleTakeoutZip;
        }

        if (entries.Any(e => e.FullName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)))
        {
            return EmailArchiveFormats.EmlZip;
        }

        return null;
    }

    private static bool LooksLikePst(Stream stream)
    {
        Span<byte> header = stackalloc byte[4];
        var bytesRead = stream.Read(header);
        stream.Seek(-bytesRead, SeekOrigin.Current);

        // Unicode PST/OST header starts with "!BDN"
        return bytesRead == 4 &&
               header[0] == 0x21 &&
               header[1] == 0x42 &&
               header[2] == 0x44 &&
               header[3] == 0x4E;
    }

    private static bool LooksLikeMbox(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var firstLine = reader.ReadLine();
        stream.Seek(0, SeekOrigin.Begin);
        return firstLine?.StartsWith("From ", StringComparison.Ordinal) == true;
    }

    private static bool LooksLikeEml(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerSample = reader.ReadLine();
        var inspected = 0;

        while (headerSample != null && inspected < 50)
        {
            if (headerSample.StartsWith("From:", StringComparison.OrdinalIgnoreCase) ||
                headerSample.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase) ||
                headerSample.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return true;
            }

            headerSample = reader.ReadLine();
            inspected++;
        }

        stream.Seek(0, SeekOrigin.Begin);
        return false;
    }
}



