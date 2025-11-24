using System;
using System.Threading;
using System.Threading.Tasks;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Services.Audit;

public sealed class AuditLogger(EvermailDbContext dbContext, TenantContext tenantContext, IHttpContextAccessor httpContextAccessor) : IAuditLogger
{
    private readonly EvermailDbContext _dbContext = dbContext;
    private readonly TenantContext _tenantContext = tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task LogAsync(
        string action,
        string? resourceType = null,
        Guid? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId == Guid.Empty)
        {
            return;
        }

        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ip = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
            var statusCode = httpContext?.Response?.StatusCode;

            var entry = new AuditLog
            {
                TenantId = _tenantContext.TenantId,
                UserId = _tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId,
                Action = Truncate($"{action}".Trim(), 100),
                ResourceType = Truncate(resourceType, 100),
                ResourceId = resourceId,
                IpAddress = Truncate(ip, 45),
                UserAgent = Truncate(userAgent, 500),
                Details = BuildDetails(details, statusCode),
                Timestamp = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Do not let audit logging failures break the request pipeline.
        }
    }

    private static string? BuildDetails(string? details, int? statusCode)
    {
        var normalizedDetails = details?.Trim();
        if (statusCode is null)
        {
            return normalizedDetails;
        }

        var statusFragment = $"status:{statusCode}";
        if (string.IsNullOrEmpty(normalizedDetails))
        {
            return statusFragment;
        }

        return $"{statusFragment} {normalizedDetails}";
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

