using System.Security.Claims;

namespace Evermail.AdminApp.Auth;

public static class AdminAuthPolicy
{
    public const string SuperAdminRole = "SuperAdmin";

    public static bool IsAllowed(string? email, AdminAuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (options.AllowedEmails.Any(e => string.Equals(e?.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var at = normalizedEmail.LastIndexOf('@');
        if (at <= 0 || at >= normalizedEmail.Length - 1)
        {
            return false;
        }

        var domain = normalizedEmail[(at + 1)..];
        return options.AllowedDomains.Any(d => string.Equals(d?.Trim(), domain, StringComparison.OrdinalIgnoreCase));
    }

    public static string? GetEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
               ?? principal.FindFirstValue("email")
               ?? principal.FindFirstValue("preferred_username")
               ?? principal.FindFirstValue("upn");
    }
}


