namespace Evermail.Common.DTOs.Audit;

public record AuditLogDto(
    Guid Id,
    DateTime Timestamp,
    string Action,
    Guid? UserId,
    string? UserEmail,
    string? ResourceType,
    Guid? ResourceId,
    string? IpAddress,
    string? UserAgent,
    string? Details);

public record AuditLogQueryParameters(
    int Page = 1,
    int PageSize = 50,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    string? Action = null,
    Guid? UserId = null,
    string? ResourceType = null);

public record PagedAuditLogsResponse(
    IReadOnlyList<AuditLogDto> Items,
    int Page,
    int PageSize,
    int Total);

public record GdprExportJobDto(
    Guid Id,
    string RequestedByEmail,
    string TargetUserEmail,
    string Status,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    DateTime? ExpiresAt,
    long? FileSizeBytes,
    string? Sha256,
    string? DownloadUrl);

public record GdprDeletionJobDto(
    Guid Id,
    string RequestedByEmail,
    string TargetUserEmail,
    string Status,
    DateTime RequestedAt,
    DateTime? CompletedAt);

public record ComplianceGdprJobsResponse(
    IReadOnlyList<GdprExportJobDto> Exports,
    IReadOnlyList<GdprDeletionJobDto> Deletions);

