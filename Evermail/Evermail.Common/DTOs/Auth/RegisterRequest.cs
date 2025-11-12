using System.ComponentModel.DataAnnotations;

namespace Evermail.Common.DTOs.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(12)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, MaxLength(256)] string TenantName
);

