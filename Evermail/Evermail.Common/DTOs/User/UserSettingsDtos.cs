using System.Collections.Generic;

namespace Evermail.Common.DTOs.User;

public record UserDisplaySettingsDto(
    string DateFormat,
    string ResultDensity,
    bool AutoScrollToKeyword,
    bool MatchNavigatorEnabled,
    bool KeyboardShortcutsEnabled
);

public record UserProfileDto(
    Guid UserId,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    bool TwoFactorEnabled,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string TenantName,
    string SubscriptionTier,
    int MaxStorageGb,
    int MaxUsers,
    long StorageBytesUsed,
    int MailboxCount,
    IReadOnlyList<string> Roles
);

public record UpdateUserDisplaySettingsRequest(
    string? DateFormat,
    string? ResultDensity,
    bool? AutoScrollToKeyword,
    bool? MatchNavigatorEnabled,
    bool? KeyboardShortcutsEnabled
);

public record SavedSearchFilterDefinitionDto(
    string? Query,
    Guid? MailboxId,
    string? From,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool? HasAttachments,
    string? Recipient,
    Guid? ConversationId
);

public record SavedSearchFilterDto(
    Guid Id,
    string Name,
    SavedSearchFilterDefinitionDto Definition,
    int OrderIndex,
    bool IsFavorite,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateSavedSearchFilterRequest(
    string Name,
    SavedSearchFilterDefinitionDto Definition,
    int? OrderIndex,
    bool IsFavorite
);

public record UpdateSavedSearchFilterRequest(
    string? Name,
    SavedSearchFilterDefinitionDto? Definition,
    int? OrderIndex,
    bool? IsFavorite
);

public record PinEmailResponse(
    Guid? EmailId,
    Guid? ConversationId,
    bool IsPinned,
    DateTime? PinnedAt
);

