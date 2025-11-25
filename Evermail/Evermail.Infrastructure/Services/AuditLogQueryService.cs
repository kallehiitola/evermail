using System.Linq.Expressions;
using Evermail.Common.DTOs.Audit;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Services;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly EvermailDbContext _context;

    public AuditLogQueryService(EvermailDbContext context)
    {
        _context = context;
    }

    public async Task<PagedAuditLogsResponse> GetLogsAsync(
        Guid tenantId,
        AuditLogQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var (query, page, pageSize) = BuildQuery(tenantId, parameters);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(al => al.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);

        return new PagedAuditLogsResponse(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetLogsForExportAsync(
        Guid tenantId,
        AuditLogQueryParameters parameters,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var (query, _, _) = BuildQuery(tenantId, parameters);

        maxRows = Math.Clamp(maxRows, 1, 50_000);

        return await query
            .OrderByDescending(al => al.Timestamp)
            .Take(maxRows)
            .Select(MapToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    private (IQueryable<AuditLog> Query, int Page, int PageSize) BuildQuery(
        Guid tenantId,
        AuditLogQueryParameters parameters)
    {
        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 200);

        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.TenantId == tenantId);

        if (parameters.StartUtc.HasValue)
        {
            query = query.Where(al => al.Timestamp >= parameters.StartUtc.Value);
        }

        if (parameters.EndUtc.HasValue)
        {
            query = query.Where(al => al.Timestamp <= parameters.EndUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Action))
        {
            query = query.Where(al => al.Action == parameters.Action);
        }

        if (parameters.UserId.HasValue)
        {
            query = query.Where(al => al.UserId == parameters.UserId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.ResourceType))
        {
            query = query.Where(al => al.ResourceType == parameters.ResourceType);
        }

        return (query, page, pageSize);
    }

    private static readonly Expression<Func<AuditLog, AuditLogDto>> MapToDtoExpression = log => new AuditLogDto(
        log.Id,
        log.Timestamp,
        log.Action,
        log.UserId,
        log.User != null ? log.User.Email : null,
        log.ResourceType,
        log.ResourceId,
        log.IpAddress,
        log.UserAgent,
        log.Details);
}

