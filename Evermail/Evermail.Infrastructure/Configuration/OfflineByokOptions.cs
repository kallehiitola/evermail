namespace Evermail.Infrastructure.Configuration;

public class OfflineByokOptions
{
    /// <summary>
    /// Base64-encoded 256-bit master key used to encrypt offline BYOK material at rest.
    /// </summary>
    public string? MasterKey { get; set; }
}





