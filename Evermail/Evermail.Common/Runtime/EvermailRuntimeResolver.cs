using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Evermail.Common.Runtime;

public static class EvermailRuntimeResolver
{
    public static EvermailRuntimeMode ResolveMode(IConfiguration configuration, IHostEnvironment env)
    {
        var value = configuration[$"{EvermailRuntimeOptions.SectionName}:Mode"];

        if (!string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<EvermailRuntimeMode>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        // Safe defaults:
        // - Dev machines default to Local (Aspire SQL + Azurite).
        // - Deployed environments default to AzureProd.
        return env.IsDevelopment() ? EvermailRuntimeMode.Local : EvermailRuntimeMode.AzureProd;
    }

    public static string? ResolveConnectionString(
        IConfiguration configuration,
        EvermailRuntimeMode mode,
        string name)
    {
        if (mode == EvermailRuntimeMode.Local)
        {
            // Prefer the Aspire-provided environment variable even if Key Vault config exists.
            // This is what keeps local mode stable even when a Key Vault also has connection strings.
            var envVar = Environment.GetEnvironmentVariable($"ConnectionStrings__{name}");
            return !string.IsNullOrWhiteSpace(envVar) ? envVar : configuration.GetConnectionString(name);
        }

        // Azure modes prefer a dedicated *Azure key so we can keep Local stable.
        // Fall back to the legacy key name to avoid breaking existing Key Vault setups.
        return configuration.GetConnectionString($"{name}Azure")
               ?? configuration.GetConnectionString(name);
    }
}


