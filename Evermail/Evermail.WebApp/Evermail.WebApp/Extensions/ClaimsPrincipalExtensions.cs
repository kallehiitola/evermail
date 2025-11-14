using System.Security.Claims;

namespace Evermail.WebApp.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the user's email from JWT claims, checking multiple possible claim types.
    /// JWT tokens can use different claim type URIs depending on the issuer.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
            ?? user.Identity?.Name;
    }

    /// <summary>
    /// Gets the user ID from JWT claims.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal user)
    {
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets the tenant ID from JWT claims.
    /// </summary>
    public static string? GetTenantId(this ClaimsPrincipal user)
    {
        return user.FindFirst("tenant_id")?.Value;
    }

    /// <summary>
    /// Gets the user's display name from JWT claims.
    /// </summary>
    public static string? GetDisplayName(this ClaimsPrincipal user)
    {
        var givenName = user.FindFirst("given_name")?.Value ?? user.FindFirst(ClaimTypes.GivenName)?.Value;
        var familyName = user.FindFirst("family_name")?.Value ?? user.FindFirst(ClaimTypes.Surname)?.Value;

        if (!string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(familyName))
        {
            return $"{givenName} {familyName}";
        }

        return givenName ?? familyName ?? user.GetEmail() ?? "User";
    }
}

