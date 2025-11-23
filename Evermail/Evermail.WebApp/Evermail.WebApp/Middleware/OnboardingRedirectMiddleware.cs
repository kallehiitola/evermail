using Evermail.Common.DTOs;
using Evermail.WebApp.Services.Onboarding;

namespace Evermail.WebApp.Middleware;

public sealed class OnboardingRedirectMiddleware
{
    private static readonly string[] AllowedPrefixes =
    [
        "/onboarding",
        "/login",
        "/register",
        "/logout",
        "/upload",
        "/admin",
        "/api/",
        "/_blazor",
        "/_framework",
        "/_content",
        "/css",
        "/js",
        "/images",
        "/favicon",
        "/assets",
        "/dev"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<OnboardingRedirectMiddleware> _logger;

    public OnboardingRedirectMiddleware(RequestDelegate next, ILogger<OnboardingRedirectMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOnboardingStatusService onboardingStatusService)
    {
        if (!ShouldInspectRequest(context))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true || user.IsInRole("SuperAdmin"))
        {
            await _next(context);
            return;
        }

        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId) || tenantId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        var completed = await onboardingStatusService.IsOnboardingCompleteAsync(tenantId, context.RequestAborted);
        if (completed)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/onboarding", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(
                new ApiResponse<object>(false, null, "Finish onboarding to access the API."),
                context.RequestAborted);
            return;
        }

        _logger.LogInformation("Redirecting tenant {TenantId} to onboarding from {Path}", tenantId, path);
        context.Response.Redirect("/onboarding");
    }

    private static bool ShouldInspectRequest(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (context.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in AllowedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

