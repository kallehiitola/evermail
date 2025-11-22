using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.IO;
using MimeKit.Text;
using MimeKit.Utils;
using XstReader;

namespace Evermail.Infrastructure.Services.Archives;

public sealed class PstToMboxWriter
{
    private readonly ILogger<PstToMboxWriter> _logger;

    public PstToMboxWriter(ILogger<PstToMboxWriter> logger)
    {
        _logger = logger;
    }

    public async Task<string> ConvertToMboxAsync(string pstPath, CancellationToken cancellationToken)
    {
        var outputPath = CreateTempPath(".mbox");

        await using var outputStream = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 64,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var xstFile = new XstFile(pstPath);
        var rootFolder = xstFile.ReadFolderTree();
        var folders = EnumerateFolders(rootFolder).ToList();

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Message> messages;
            try
            {
                messages = xstFile.ReadMessages(folder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read PST folder {Folder}", folder.Path);
                continue;
            }

            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    xstFile.ReadMessageDetails(message);
                    var mimeMessage = ConvertToMimeMessage(xstFile, message);
                    await MboxWriter.WriteMessageAsync(outputStream, mimeMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to convert PST message {Folder}/{Subject}",
                        folder.Path,
                        message.Subject);
                }
            }
        }

        await outputStream.FlushAsync(cancellationToken);
        return outputPath;
    }

    private static IEnumerable<Folder> EnumerateFolders(Folder root)
    {
        var stack = new Stack<Folder>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var folder = stack.Pop();
            yield return folder;

            for (var i = folder.Folders.Count - 1; i >= 0; i--)
            {
                stack.Push(folder.Folders[i]);
            }
        }
    }

    private MimeMessage ConvertToMimeMessage(XstFile xstFile, Message message)
    {
        var mimeMessage = new MimeMessage
        {
            MessageId = MimeUtils.GenerateMessageId("pst.evermail.local"),
            Subject = message.Subject ?? string.Empty,
            Date = ConvertToDateTimeOffset(message.Date)
        };

        var fromAddress = CreateMailboxAddress(message.From, null);
        mimeMessage.From.Add(fromAddress);

        if (message.Recipients.Any())
        {
            foreach (var recipient in message.Recipients)
            {
                var address = CreateMailboxAddress(recipient.DisplayName, recipient.EmailAddress);
                switch (recipient.RecipientType)
                {
                    case RecipientType.To:
                        mimeMessage.To.Add(address);
                        break;
                    case RecipientType.Cc:
                        mimeMessage.Cc.Add(address);
                        break;
                    case RecipientType.Bcc:
                        mimeMessage.Bcc.Add(address);
                        break;
                    default:
                        mimeMessage.To.Add(address);
                        break;
                }
            }
        }
        else
        {
            AddFallbackRecipients(mimeMessage.To, message.To);
            AddFallbackRecipients(mimeMessage.Cc, message.Cc);
        }

        var bodyEntity = BuildBodyEntity(message);
        var attachments = BuildAttachments(xstFile, message.Attachments);

        if (attachments.Count == 0)
        {
            mimeMessage.Body = bodyEntity;
        }
        else
        {
            var mixed = new Multipart("mixed") { bodyEntity };
            foreach (var attachment in attachments)
            {
                mixed.Add(attachment);
            }
            mimeMessage.Body = mixed;
        }

        return mimeMessage;
    }

    private static MimeEntity BuildBodyEntity(Message message)
    {
        var html = message.GetBodyAsHtmlString();
        var text = message.Body;

        if (!string.IsNullOrEmpty(html) && !string.IsNullOrEmpty(text))
        {
            var alternative = new MultipartAlternative
            {
                new TextPart(TextFormat.Plain) { Text = text },
                new TextPart(TextFormat.Html) { Text = html }
            };
            return alternative;
        }

        if (!string.IsNullOrEmpty(html))
        {
            return new TextPart(TextFormat.Html) { Text = html };
        }

        if (!string.IsNullOrEmpty(text))
        {
            return new TextPart(TextFormat.Plain) { Text = text };
        }

        return new TextPart(TextFormat.Plain) { Text = string.Empty };
    }

    private List<MimePart> BuildAttachments(XstFile xstFile, IEnumerable<Attachment> attachments)
    {
        var parts = new List<MimePart>();

        foreach (var attachment in attachments.Where(a => a.IsFile))
        {
            try
            {
                using var buffer = new MemoryStream();
                xstFile.SaveAttachment(buffer, attachment);
                var contentStream = new MemoryStream(buffer.ToArray());

                var contentType = ResolveContentType(attachment.MimeTag);
                var part = new MimePart(contentType)
                {
                    Content = new MimeContent(contentStream, ContentEncoding.Default),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachment.FileName ?? "attachment",
                    ContentId = attachment.HasContentId ? attachment.ContentId : null
                };

                var disposition = attachment.HasContentId
                    ? new ContentDisposition(ContentDisposition.Inline)
                    : new ContentDisposition(ContentDisposition.Attachment);

                disposition.FileName = part.FileName;
                part.ContentDisposition = disposition;

                parts.Add(part);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to convert PST attachment {AttachmentName}",
                    attachment.FileName ?? attachment.LongFileName ?? "attachment");
            }
        }

        return parts;
    }

    private static MailboxAddress CreateMailboxAddress(string? displayName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (MailboxAddress.TryParse(email, out var parsed))
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    parsed.Name = displayName;
                }
                return parsed;
            }
        }

        if (!string.IsNullOrWhiteSpace(displayName) &&
            MailboxAddress.TryParse(displayName, out var displayParsed))
        {
            return displayParsed;
        }

        var fallbackAddress = $"unknown-{Guid.NewGuid():N}@pst.local";
        return new MailboxAddress(displayName ?? "Unknown Sender", fallbackAddress);
    }

    private static void AddFallbackRecipients(InternetAddressList list, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            list.Add(CreateMailboxAddress(entry, null));
        }
    }

    private static DateTimeOffset ConvertToDateTimeOffset(DateTime? dateTime)
    {
        if (dateTime is null)
        {
            return DateTimeOffset.UtcNow;
        }

        var unspecified = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeSpan.Zero);
    }

    private static ContentType ResolveContentType(string? mimeTag)
    {
        if (!string.IsNullOrWhiteSpace(mimeTag))
        {
            try
            {
                return ContentType.Parse(mimeTag);
            }
            catch
            {
                // Fall back to default type.
            }
        }

        return new ContentType("application", "octet-stream");
    }

    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"evermail-pst-{Guid.NewGuid():N}{extension}");
}

