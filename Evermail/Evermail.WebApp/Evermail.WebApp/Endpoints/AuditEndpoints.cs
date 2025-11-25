using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Audit;
using Evermail.Common.DTOs.User;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.WebApp.Services.Audit;
using Evermail.WebApp.Services.Gdpr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class AuditEndpoints
{
    private const int ExportRowLimit = 10_000;

    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/logs", GetAuditLogsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        group.MapGet("/logs/export", ExportAuditLogsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return group;
    }

    public static RouteGroupBuilder MapComplianceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/gdpr-jobs", GetGdprJobsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return group;
    }

    private static async Task<IResult> GetAuditLogsAsync(
        [AsParameters] AuditLogQuery query,
        IAuditLogQueryService queryService,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!EnsureTenant(tenantContext, out var failure))
        {
            return failure!;
        }

        var parameters = query.ToParameters();
        var logs = await queryService.GetLogsAsync(tenantContext.TenantId, parameters, cancellationToken);
        return Results.Ok(new ApiResponse<PagedAuditLogsResponse>(true, logs));
    }

    private static async Task<IResult> ExportAuditLogsAsync(
        [AsParameters] AuditLogQuery query,
        IAuditLogQueryService queryService,
        TenantContext tenantContext,
        IAuditLogger auditLogger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!EnsureTenant(tenantContext, out var failure))
        {
            return failure!;
        }

        var parameters = query.ToParameters();
        var rows = await queryService.GetLogsForExportAsync(
            tenantContext.TenantId,
            parameters,
            ExportRowLimit,
            cancellationToken);

        var csvBytes = BuildCsv(rows);
        var hash = Convert.ToHexString(SHA256.HashData(csvBytes));
        httpContext.Response.Headers["X-Export-Hash"] = hash;

        await auditLogger.LogAsync(
            "AuditLogCsvExported",
            "AuditLog",
            null,
            $"count:{rows.Count}",
            cancellationToken);

        var fileName = $"evermail-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return Results.File(csvBytes, "text/csv", fileName);
    }

    private static async Task<IResult> GetGdprJobsAsync(
        EvermailDbContext context,
        IGdprExportService exportService,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!EnsureTenant(tenantContext, out var failure))
        {
            return failure!;
        }

        var exports = await context.UserDataExports
            .AsNoTracking()
            .Where(e => e.TenantId == tenantContext.TenantId)
            .OrderByDescending(e => e.RequestedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var deletions = await context.UserDeletionJobs
            .AsNoTracking()
            .Where(d => d.TenantId == tenantContext.TenantId)
            .OrderByDescending(d => d.RequestedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var userIds = exports
            .SelectMany(e => new[] { e.UserId, e.RequestedByUserId })
            .Concat(deletions.SelectMany(d => new[] { d.UserId, d.RequestedByUserId }))
            .Distinct()
            .ToList();

        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, Email = u.Email ?? "unknown" })
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);

        var exportDtos = new List<GdprExportJobDto>(exports.Count);
        foreach (var export in exports)
        {
            var downloadUrl = await exportService.TryCreateDownloadUrlAsync(export, cancellationToken);
            exportDtos.Add(new GdprExportJobDto(
                export.Id,
                LookupEmail(users, export.RequestedByUserId),
                LookupEmail(users, export.UserId),
                export.Status,
                export.RequestedAt,
                export.CompletedAt,
                export.ExpiresAt,
                export.FileSizeBytes,
                export.Sha256,
                downloadUrl));
        }

        var deletionDtos = deletions
            .Select(d => new GdprDeletionJobDto(
                d.Id,
                LookupEmail(users, d.RequestedByUserId),
                LookupEmail(users, d.UserId),
                d.Status,
                d.RequestedAt,
                d.CompletedAt))
            .ToList();

        var payload = new ComplianceGdprJobsResponse(exportDtos, deletionDtos);
        return Results.Ok(new ApiResponse<ComplianceGdprJobsResponse>(true, payload));
    }

    private static bool EnsureTenant(TenantContext tenantContext, out IResult? failureResult)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            failureResult = Results.Unauthorized();
            return false;
        }

        failureResult = null;
        return true;
    }

    private static string LookupEmail(IReadOnlyDictionary<Guid, string> users, Guid userId) =>
        users.TryGetValue(userId, out var email) ? email : "unknown";

    private static byte[] BuildCsv(IReadOnlyList<AuditLogDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp,action,userEmail,resourceType,resourceId,ipAddress,userAgent,details");

        foreach (var row in rows)
        {
            var line = string.Join(',',
                FormatCsvField(row.Timestamp.ToString("o", CultureInfo.InvariantCulture)),
                FormatCsvField(row.Action),
                FormatCsvField(row.UserEmail),
                FormatCsvField(row.ResourceType),
                FormatCsvField(row.ResourceId?.ToString()),
                FormatCsvField(row.IpAddress),
                FormatCsvField(row.UserAgent),
                FormatCsvField(row.Details));
            builder.AppendLine(line);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string FormatCsvField(string? value)
    {
        var safe = value ?? string.Empty;
        safe = safe.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }

    private sealed record AuditLogQuery(
        int Page = 1,
        int PageSize = 50,
        DateTime? StartUtc = null,
        DateTime? EndUtc = null,
        string? Action = null,
        Guid? UserId = null,
        string? ResourceType = null)
    {
        public AuditLogQueryParameters ToParameters() =>
            new(Page, PageSize, StartUtc, EndUtc, Action, UserId, ResourceType);
    }
}

