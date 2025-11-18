using System.Security.Claims;
using Evermail.Domain.Entities;
using Evermail.WebApp.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Evermail.WebApp.Middleware;

/// <summary>
/// Development-only middleware that lets the Browser tool inspect protected pages
/// by impersonating a predefined user whenever <c>?ai=1</c> is appended to the URL.
/// </summary>
public sealed class AiImpersonationMiddleware
{
    private const string AuthenticationScheme = "ai-impersonation";

    private readonly RequestDelegate _next;
    private readonly ILogger<AiImpersonationMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<AiImpersonationOptions> _optionsMonitor;

    public AiImpersonationMiddleware(
        RequestDelegate next,
        ILogger<AiImpersonationMiddleware> logger,
        IHostEnvironment environment,
        IOptionsMonitor<AiImpersonationOptions> optionsMonitor)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled || context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (!ShouldImpersonate(context.Request, options))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.UserEmail))
        {
            _logger.LogWarning("AI impersonation requested but no user email configured.");
            await _next(context);
            return;
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(options.UserEmail);
        if (user is null)
        {
            _logger.LogWarning("AI impersonation user {Email} was not found.", options.UserEmail);
            await _next(context);
            return;
        }

        var claims = await BuildClaimsAsync(userManager, user);
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        context.User = new ClaimsPrincipal(identity);

        _logger.LogInformation(
            "AI impersonation enabled for {Email} on {Path}.",
            options.UserEmail,
            context.Request.Path);

        await _next(context);
    }

    private static async Task<IEnumerable<Claim>> BuildClaimsAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? "AI User")
        };

        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;
    }

    private static bool ShouldImpersonate(HttpRequest request, AiImpersonationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TriggerQueryKey))
        {
            return false;
        }

        var triggerKey = options.TriggerQueryKey;
        var triggerValue = options.TriggerValue ?? "1";

        foreach (var kvp in request.Query)
        {
            if (string.Equals(kvp.Key, triggerKey, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value.Any(value =>
                    string.Equals(value, triggerValue, StringComparison.OrdinalIgnoreCase));
            }
        }

        return false;
    }
}

