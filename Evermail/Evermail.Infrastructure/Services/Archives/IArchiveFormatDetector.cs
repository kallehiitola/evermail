using System.Threading;
using System.Threading.Tasks;

namespace Evermail.Infrastructure.Services.Archives;

public interface IArchiveFormatDetector
{
    Task<string> DetectFormatAsync(
        string blobPath,
        string originalFileName,
        CancellationToken cancellationToken = default);
}



