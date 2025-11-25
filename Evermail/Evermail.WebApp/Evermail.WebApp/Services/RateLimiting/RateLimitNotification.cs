using System;

namespace Evermail.WebApp.Services.RateLimiting;

public sealed record RateLimitNotification(TimeSpan? RetryAfter, string? ServerMessage);

