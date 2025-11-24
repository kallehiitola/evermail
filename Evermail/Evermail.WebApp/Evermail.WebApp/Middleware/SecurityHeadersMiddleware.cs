using System;
using System.Text;
using System.Threading.Tasks;

namespace Evermail.WebApp.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly IWebHostEnvironment _environment = environment;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["X-XSS-Protection"] = "1; mode=block";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), clipboard-read=(), clipboard-write=()";

        if (!_environment.IsDevelopment())
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        headers["Content-Security-Policy"] = BuildContentSecurityPolicy();

        await _next(context);
    }

    private string BuildContentSecurityPolicy()
    {
        var builder = new StringBuilder();
        builder.Append("default-src 'self'; ");
        builder.Append("img-src 'self' data:; ");

        // Scripts: allow inline handlers for now (Blazor event bindings emit them) plus dev sockets.
        builder.Append("script-src 'self' 'unsafe-inline'");
        if (_environment.IsDevelopment())
        {
            builder.Append(" https://localhost:*");
        }
        builder.Append("; ");

        // Stylesheets from CDN + Google Fonts.
        builder.Append("style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; ");

        // Fonts from CDN + Google Fonts.
        builder.Append("font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; ");

        // Connectivity (API, SignalR, Azure Blob uploads).
        builder.Append("connect-src 'self'");
        if (_environment.IsDevelopment())
        {
            builder.Append(" https://localhost:* wss://localhost:*");
        }
        builder.Append(" https://*.blob.core.windows.net; ");

        return builder.ToString();
    }
}

