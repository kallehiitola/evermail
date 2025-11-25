using Evermail.Common.DTOs.Audit;

namespace Evermail.Infrastructure.Services;

public interface IAuditLogQueryService
{
    Task<PagedAuditLogsResponse> GetLogsAsync(
        Guid tenantId,
        AuditLogQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> GetLogsForExportAsync(
        Guid tenantId,
        AuditLogQueryParameters parameters,
        int maxRows,
        CancellationToken cancellationToken = default);
}

