using System.Net;
using Microsoft.AspNetCore.Http;

namespace Evermail.WebApp.Services.RateLimiting;

internal sealed record RateLimitPlan(string Name, int PermitLimit, TimeSpan Window, int QueueLimit)
{
    public bool IsUnlimited => PermitLimit <= 0;
}

internal static class RateLimitPlanCatalog
{
    private static readonly RateLimitPlan Free = new("Free", 300, TimeSpan.FromHours(1), 0);
    private static readonly RateLimitPlan Pro = new("Pro", 2_000, TimeSpan.FromHours(1), 0);
    private static readonly RateLimitPlan Team = new("Team", 20_000, TimeSpan.FromHours(1), 0);
    private static readonly RateLimitPlan Enterprise = new("Enterprise", 0, TimeSpan.FromHours(1), 0);
    private static readonly RateLimitPlan Anonymous = new("Anonymous", 600, TimeSpan.FromMinutes(1), 0);

    public static RateLimitPlan Resolve(string? tier) =>
        (tier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pro" => Pro,
            "team" => Team,
            "enterprise" => Enterprise,
            "anonymous" => Anonymous,
            _ => Free
        };
}

internal sealed record RateLimitPartitionKey(string Key, RateLimitPlan Plan)
{
    public static RateLimitPartitionKey Create(HttpContext httpContext)
    {
        var tierClaim = httpContext.User.FindFirst("subscription_tier")?.Value;
        var plan = httpContext.User.Identity?.IsAuthenticated == true
            ? RateLimitPlanCatalog.Resolve(tierClaim)
            : RateLimitPlanCatalog.Resolve("anonymous");

        var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        var identityKey = !string.IsNullOrEmpty(tenantId)
            ? $"tenant:{tenantId}:{plan.Name}"
            : $"ip:{httpContext.Connection.RemoteIpAddress ?? IPAddress.None}:{plan.Name}";

        return new RateLimitPartitionKey(identityKey, plan);
    }
}


