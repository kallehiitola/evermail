using System.ComponentModel.DataAnnotations;

namespace Evermail.WebApp.Configuration;

/// <summary>
/// Options controlling the AI browser impersonation helper.
/// This helper is intended for local development only so the Browser tool can inspect protected routes.
/// </summary>
public sealed class AiImpersonationOptions
{
    public const string SectionName = "AiImpersonation";

    /// <summary>
    /// Enables the impersonation helper. Defaults to false for safety.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Query string key that triggers impersonation (e.g. "ai").
    /// </summary>
    [Required]
    public string TriggerQueryKey { get; set; } = "ai";

    /// <summary>
    /// Value for the trigger key (e.g. "1").
    /// </summary>
    [Required]
    public string TriggerValue { get; set; } = "1";

    /// <summary>
    /// Email address of the user that should be impersonated when the helper is active.
    /// </summary>
    [Required]
    public string UserEmail { get; set; } = string.Empty;
}

