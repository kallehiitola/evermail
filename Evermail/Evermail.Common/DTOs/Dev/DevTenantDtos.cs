namespace Evermail.Common.DTOs.Dev;

public record DevTenantDto(
    Guid Id,
    string Name,
    string Slug,
    DateTime CreatedAt,
    int UserCount,
    IReadOnlyList<DevTenantUserDto> Users,
    bool PlanConfirmed,
    bool EncryptionConfigured,
    bool PaymentAcknowledged,
    bool HasMailbox);

public record DevTenantUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsAdmin,
    bool IsSuperAdmin,
    DateTime CreatedAt);

