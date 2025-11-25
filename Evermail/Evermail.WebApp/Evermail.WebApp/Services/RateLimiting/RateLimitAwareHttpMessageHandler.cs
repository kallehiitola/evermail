using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Evermail.WebApp.Services.RateLimiting;

public sealed class RateLimitAwareHttpMessageHandler(IRateLimitNotifier notifier) : DelegatingHandler
{
    private readonly IRateLimitNotifier _notifier = notifier;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            string? serverMessage = null;

            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                try
                {
                    var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(payload);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.String)
                    {
                        serverMessage = errorElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed payloads; best-effort notification only.
                }
            }

            _notifier.Publish(new RateLimitNotification(retryAfter, serverMessage));
        }

        return response;
    }
}

