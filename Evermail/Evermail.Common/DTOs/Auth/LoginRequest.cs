using System.ComponentModel.DataAnnotations;

namespace Evermail.Common.DTOs.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    string? TwoFactorCode = null
);

