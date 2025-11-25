using System;
using Evermail.WebApp.Services.RateLimiting;
using MudBlazor;

namespace Evermail.WebApp.Services.Notifications;

public sealed class MudToastService : IToastService, IDisposable
{
    private readonly ISnackbar _snackbar;
    private readonly IRateLimitNotifier _notifier;
    private DateTimeOffset _lastRateLimitToastUtc = DateTimeOffset.MinValue;

    public MudToastService(ISnackbar snackbar, IRateLimitNotifier notifier)
    {
        _snackbar = snackbar;
        _notifier = notifier;
        _notifier.RateLimitExceeded += OnRateLimitExceeded;
    }

    public void Success(string message, string? detail = null) =>
        Show(message, detail, Severity.Success);

    public void Info(string message, string? detail = null) =>
        Show(message, detail, Severity.Info);

    public void Warning(string message, string? detail = null) =>
        Show(message, detail, Severity.Warning);

    public void Error(string message, string? detail = null) =>
        Show(message, detail, Severity.Error);

    public void ShowRateLimit(TimeSpan? retryAfter, string? serverMessage = null)
    {
        if (DateTimeOffset.UtcNow - _lastRateLimitToastUtc < TimeSpan.FromSeconds(10))
        {
            return;
        }

        var hint = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
            ? $"Please try again in {Math.Ceiling(retryAfter.Value.TotalSeconds)} seconds."
            : "Please try again shortly.";

        var detail = string.IsNullOrWhiteSpace(serverMessage)
            ? hint
            : $"{serverMessage} {hint}";

        Show("Request limit reached", detail, Severity.Warning);
        _lastRateLimitToastUtc = DateTimeOffset.UtcNow;
    }

    private void Show(string message, string? detail, Severity severity)
    {
        var text = string.IsNullOrWhiteSpace(detail)
            ? message
            : $"{message}\n{detail}";

        _snackbar.Add(text, severity, config =>
        {
            config.ShowTransitionDuration = 150;
            config.HideTransitionDuration = 150;
            config.VisibleStateDuration = severity is Severity.Error ? 8000 : 5000;
        });
    }
    private void OnRateLimitExceeded(object? sender, RateLimitNotification notification) =>
        ShowRateLimit(notification.RetryAfter, notification.ServerMessage);

    public void Dispose()
    {
        _notifier.RateLimitExceeded -= OnRateLimitExceeded;
    }
}

