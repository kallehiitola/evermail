namespace Evermail.AdminApp.Auth;

public sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public string[] AllowedEmails { get; init; } = [];

    public string[] AllowedDomains { get; init; } = [];
}


