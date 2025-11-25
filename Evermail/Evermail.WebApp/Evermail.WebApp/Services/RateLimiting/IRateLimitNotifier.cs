using System;

namespace Evermail.WebApp.Services.RateLimiting;

public interface IRateLimitNotifier
{
    event EventHandler<RateLimitNotification>? RateLimitExceeded;

    void Publish(RateLimitNotification notification);
}

