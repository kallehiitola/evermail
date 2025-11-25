using System;

namespace Evermail.WebApp.Services.Notifications;

public interface IToastService
{
    void Success(string message, string? detail = null);
    void Info(string message, string? detail = null);
    void Warning(string message, string? detail = null);
    void Error(string message, string? detail = null);
    void ShowRateLimit(TimeSpan? retryAfter, string? serverMessage = null);
}

