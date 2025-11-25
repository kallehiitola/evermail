using System;

namespace Evermail.WebApp.Services.RateLimiting;

public sealed class RateLimitNotifier : IRateLimitNotifier
{
    public event EventHandler<RateLimitNotification>? RateLimitExceeded;

    public void Publish(RateLimitNotification notification)
    {
        RateLimitExceeded?.Invoke(this, notification);
    }
}

