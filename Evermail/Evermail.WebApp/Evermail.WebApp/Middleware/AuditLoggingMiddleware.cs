using System;
using System.Linq;
using System.Threading.Tasks;
using Evermail.WebApp.Services.Audit;

namespace Evermail.WebApp.Middleware;

public sealed class AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
{
    private static readonly string[] LoggedMethods = ["POST", "PUT", "PATCH", "DELETE"];

    private readonly RequestDelegate _next = next;
    private readonly ILogger<AuditLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, IAuditLogger auditLogger)
    {
        if (!ShouldLog(context.Request))
        {
            await _next(context);
            return;
        }

        var action = $"{context.Request.Method} {context.Request.Path.Value?.ToLowerInvariant()}";
        var resourceType = ExtractResourceType(context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            try
            {
                await auditLogger.LogAsync(
                    action: action,
                    resourceType: resourceType,
                    resourceId: null,
                    details: null,
                    cancellationToken: context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist audit log for action {Action}", action);
            }
        }
    }

    private static bool ShouldLog(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api/v1", out var remaining))
        {
            return false;
        }

        if (remaining.StartsWithSegments("/dev", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LoggedMethods.Contains(request.Method.ToUpperInvariant());
    }

    private static string? ExtractResourceType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments is null || segments.Length < 2)
        {
            return null;
        }

        // segments[0] == "api", segments[1] == "v1", resource should be segments[2].
        return segments.Length >= 3 ? segments[2] : null;
    }
}

