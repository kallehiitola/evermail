using System.Threading;
using System.Threading.Tasks;

namespace Evermail.WebApp.Services.Audit;

public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string? resourceType = null,
        Guid? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}

