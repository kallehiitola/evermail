using System.IO;

namespace Evermail.Infrastructure.Services.Archives;

public sealed class ArchiveExtractionResult : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openStreamFactory;
    private readonly IReadOnlyList<string> _tempPaths;

    public ArchiveExtractionResult(
        string format,
        long totalBytes,
        Func<CancellationToken, Task<Stream>> openStreamFactory,
        IReadOnlyList<string>? tempPaths = null)
    {
        Format = format;
        TotalBytes = totalBytes;
        _openStreamFactory = openStreamFactory;
        _tempPaths = tempPaths ?? Array.Empty<string>();
    }

    public string Format { get; }
    public long TotalBytes { get; }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
        => _openStreamFactory(cancellationToken);

    public ValueTask DisposeAsync()
    {
        foreach (var path in _tempPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
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
                // Best-effort cleanup. Residual temp files will be purged by OS policies.
            }
        }

        return ValueTask.CompletedTask;
    }
}

