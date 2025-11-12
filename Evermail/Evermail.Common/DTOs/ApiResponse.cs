namespace Evermail.Common.DTOs;

public record ApiResponse<T>(
    bool Success,
    T? Data = default,
    string? Error = null,
    Dictionary<string, string[]>? ValidationErrors = null
);

