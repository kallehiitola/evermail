namespace Evermail.Common.DTOs.Auth;

public record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime TokenExpires,
    DateTime RefreshTokenExpires,
    UserDto User
);

public record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    bool TwoFactorEnabled
);

