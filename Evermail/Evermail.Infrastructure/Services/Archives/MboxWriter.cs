using System.IO;
using System.Linq;
using System.Text;
using MimeKit;

namespace Evermail.Infrastructure.Services.Archives;

internal static class MboxWriter
{
    public static async Task WriteMessageAsync(
        Stream destination,
        MimeMessage message,
        CancellationToken cancellationToken)
    {
        var fromAddress = message.From.Mailboxes.FirstOrDefault()?.Address ?? "MAILER-DAEMON";
        var fromLine = $"From {fromAddress} {DateTime.UtcNow:R}\r\n";
        await destination.WriteAsync(Encoding.ASCII.GetBytes(fromLine), cancellationToken);

        using var buffer = new MemoryStream();
        await message.WriteToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var writer = new StreamWriter(destination, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("From ", StringComparison.Ordinal))
            {
                line = ">" + line;
            }

            await writer.WriteLineAsync(line);
        }

        await writer.WriteLineAsync();
        await writer.FlushAsync();
    }
}

