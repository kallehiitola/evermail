namespace Evermail.Infrastructure.Services;

/// <summary>
/// Service for Two-Factor Authentication (2FA) using Time-based One-Time Passwords (TOTP).
/// Implements RFC 6238 standard for generating and validating TOTP codes.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Generates a new TOTP secret key for a user.
    /// </summary>
    string GenerateSecret();
    
    /// <summary>
    /// Generates a QR code URL for easy setup in authenticator apps (Google Authenticator, Authy, etc.).
    /// </summary>
    string GenerateQrCodeUrl(string email, string secret, string issuer = "Evermail");
    
    /// <summary>
    /// Validates a TOTP code against the user's secret.
    /// </summary>
    bool ValidateCode(string secret, string code);
    
    /// <summary>
    /// Generates backup codes for account recovery if 2FA device is lost.
    /// </summary>
    List<string> GenerateBackupCodes(int count = 10);
}

