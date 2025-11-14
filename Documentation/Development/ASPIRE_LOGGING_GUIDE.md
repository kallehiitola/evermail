# Aspire Dashboard Logging Guide

> How to view and debug logs in Aspire 13.0

---

## 📊 Aspire Dashboard Overview

The Aspire Dashboard provides **real-time monitoring** of your distributed application with:
- **Resources** - App state, endpoints, environment variables
- **Console** - Raw console output from each project
- **Structured** - Structured logs (filterable, searchable) ✨ **Most Useful**
- **Traces** - Distributed tracing across services
- **Metrics** - Performance metrics and counters

---

## 🔍 How to View Logs

### Step 1: Open Aspire Dashboard

When you run `aspire run`, the dashboard opens automatically at:
```
https://localhost:17134
```

Or click the dashboard URL shown in the console output.

### Step 2: Navigate to Logs

**Option A: Structured Logs** (Recommended)
1. Click **"Structured"** tab in the left navigation
2. See all logs from all resources in a table format
3. Supports filtering, search, and log level filtering

**Option B: Console Logs** (Raw Output)
1. Click **"Console"** tab in the left navigation
2. See raw console output from each project
3. Good for seeing startup messages, errors

### Step 3: Filter Logs

**Filter by Resource:**
- Click the **"Resource"** dropdown
- Select **"webapp"** to see only WebApp logs
- Select **"ingestionworker"** for worker logs
- Select **"adminapp"** for admin dashboard logs

**Filter by Log Level:**
- **Trace** - Verbose debug info
- **Debug** - Debug messages
- **Information** - General info (default)
- **Warning** - ⚠️ Warnings (like our 404 logs)
- **Error** - ❌ Errors
- **Critical** - 🚨 Critical failures

**Search Logs:**
- Use the search box to find specific text
- Example: Search for "404" to find not-found routes
- Example: Search for "OAuth" to see authentication logs

### Step 4: Expand Log Details

- Click the **"View"** button on the right of any log entry
- See full log details, including:
  - Timestamp
  - Log level
  - Message
  - Exception details (if error)
  - Structured properties
  - Source context

---

## 🧪 Testing 404 Logging

### Add Diagnostic Logs

I've added logging when routes aren't found:

```razor
@inject ILogger<Routes> Logger

<NotFound>
    @{
        Logger.LogWarning("🔍 404 - Route not found: {Path}", NavigationManager.Uri);
    }
    ...
</NotFound>
```

### View 404 Logs in Aspire

1. **Start Aspire:**
   ```bash
   cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
   aspire run
   ```

2. **Open Dashboard:**
   - URL: `https://localhost:17134`

3. **Navigate to invalid route:**
   - In WebApp, go to: `/whatever`, `/email`, `/random`

4. **Check Structured Logs:**
   - Dashboard → **"Structured"** tab
   - Filter: **Resource = "webapp"**
   - Filter: **Log Level = "Warning"**
   - **Look for**: "🔍 404 - Route not found: https://localhost:7136/whatever"

5. **If you DON'T see the log:**
   - The route might be matching something
   - Or there's an error preventing the NotFound from rendering
   - Check Console logs for errors

---

## 🔧 Common Logging Patterns

### In Razor Components

```razor
@using Microsoft.Extensions.Logging
@inject ILogger<MyComponent> Logger

@code {
    protected override void OnInitialized()
    {
        Logger.LogInformation("Component initialized");
    }

    private async Task HandleAction()
    {
        try
        {
            Logger.LogInformation("Starting action...");
            // ... do work
            Logger.LogInformation("Action completed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Action failed");
        }
    }
}
```

### In API Endpoints

```csharp
app.MapGet("/api/test", (ILogger<Program> logger) =>
{
    logger.LogInformation("Test endpoint called");
    return Results.Ok("Hello");
});
```

### In Services

```csharp
public class EmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessEmail(string emailId)
    {
        _logger.LogInformation("Processing email {EmailId}", emailId);
        
        try
        {
            // Process...
            _logger.LogInformation("Email {EmailId} processed successfully", emailId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email {EmailId}", emailId);
            throw;
        }
    }
}
```

---

## 📝 Log Levels - When to Use

| Level | Use When | Example |
|-------|----------|---------|
| **Trace** | Very detailed debug info | Variable values, loop iterations |
| **Debug** | Debug information | Method entry/exit, validation checks |
| **Information** | General flow | "User logged in", "Email sent" |
| **Warning** | Unexpected but handled | "404 not found", "Retry attempt" |
| **Error** | Recoverable errors | "Database timeout", "API call failed" |
| **Critical** | Fatal errors | "Database unavailable", "App crash" |

---

## 🎯 Logging for OAuth (Already Added)

I've added comprehensive logging to OAuth flows:

**In OAuthEndpoints.cs:**
```csharp
Console.WriteLine($"✅ New user registered via Google OAuth: {email}");
Console.WriteLine($"✅ Existing user logged in via Google OAuth: {email}");
Console.WriteLine($"❌ Failed to create user: {errors}");
Console.WriteLine($"❌ Error during OAuth registration: {ex.Message}");
```

**In Program.cs:**
```csharp
Console.WriteLine("✅ Google OAuth configured");
Console.WriteLine("⚠️  Google OAuth not configured (missing credentials)");
Console.WriteLine("✅ Microsoft OAuth configured");
Console.WriteLine("⚠️  Microsoft OAuth not configured (missing credentials)");
```

**View These Logs:**
- Dashboard → **"Console"** tab
- Select **"webapp"** resource
- See startup messages showing which OAuth providers are configured

---

## 🔍 Debugging Tips

### Check App Startup Logs

1. **Dashboard → Console → webapp**
2. Look for:
   ```
   ✅ Google OAuth configured
   ⚠️  Microsoft OAuth not configured (missing credentials)
   Waiting for database... (attempt 1/10)
   ```

### Check OAuth Flow Logs

1. **Click "Sign in with Google"**
2. **Dashboard → Structured → webapp**
3. Look for:
   ```
   ✅ New user registered via Google OAuth: user@gmail.com
   ✅ Existing user logged in via Google OAuth: user@gmail.com
   ```

### Check for Errors

1. **Dashboard → Structured**
2. **Filter: Log Level = Error or Critical**
3. Look for exceptions, stack traces

### Check HTTP Requests

1. **Dashboard → Traces**
2. See request flow through your app
3. Find slow endpoints
4. Identify failed requests

---

## 🚀 Quick Diagnostic Commands

### View All Logs for WebApp

```
Dashboard → Structured → Filter: Resource = "webapp"
```

### View Only Errors

```
Dashboard → Structured → Filter: Log Level = Error
```

### View Only Your Custom Logs

```
Dashboard → Structured → Search: "404" or "OAuth" or "✅"
```

### View Real-Time Console Output

```
Dashboard → Console → Select "webapp"
```

---

## 🐛 Troubleshooting Common Issues

### Issue: Blank 404 Page

**Check Logs:**
```
Dashboard → Structured → Search: "404"
```

**Expected:**
```
🔍 404 - Route not found: https://localhost:7136/whatever
```

**If no log appears:**
- The route might be matching something unexpectedly
- Check Console logs for rendering errors
- Look for exceptions in Structured logs

### Issue: OAuth Not Working

**Check Startup Logs:**
```
Dashboard → Console → webapp
```

**Look for:**
```
✅ Google OAuth configured     ← Should see this
⚠️  Microsoft OAuth not configured  ← Expected if no credentials
```

**Check OAuth Flow:**
```
Dashboard → Structured → Search: "OAuth"
```

**Look for:**
```
✅ New user registered via Google OAuth: user@gmail.com
```

### Issue: Database Connection

**Check Console Logs:**
```
Dashboard → Console → webapp
```

**Look for:**
```
Waiting for database... (attempt 1/10)
Waiting for database... (attempt 2/10)
```

**If stuck:** SQL Server container isn't ready yet (wait ~30 seconds)

---

## 📊 Structured Logging Properties

Structured logs include extracted properties:

```csharp
Logger.LogInformation("User {UserId} accessed email {EmailId}", userId, emailId);
```

**In Dashboard:**
- **Message**: "User abc-123 accessed email def-456"
- **Properties**:
  - `UserId`: "abc-123"
  - `EmailId`: "def-456"
- **Filterable** by these properties!

---

## ✅ Current Logging in Evermail

### Routes.razor
```csharp
Logger.LogWarning("🔍 404 - Route not found: {Path}", NavigationManager.Uri);
```

### OAuthEndpoints.cs
```csharp
Console.WriteLine($"✅ New user registered via Google OAuth: {email}");
Console.WriteLine($"✅ Existing user logged in via Google OAuth: {email}");
Console.WriteLine($"❌ Failed to create user: {errors}");
```

### Program.cs
```csharp
Console.WriteLine("✅ Google OAuth configured");
Console.WriteLine("⚠️  Microsoft OAuth not configured");
Console.WriteLine($"Waiting for database... (attempt {i + 1}/{maxRetries})");
```

---

## 🎯 Next Steps

### Add More Logging (Phase 1)

When implementing email parsing:

```csharp
// In MboxParser
_logger.LogInformation("Starting mbox parse: {FileName}, Size: {SizeBytes}", fileName, size);
_logger.LogInformation("Parsed {EmailCount} emails from {FileName}", count, fileName);
_logger.LogError(ex, "Failed to parse mbox file {FileName}", fileName);

// In IngestionWorker
_logger.LogInformation("Processing queue message: {MessageId}", messageId);
_logger.LogWarning("Retry attempt {Attempt} for {MessageId}", attempt, messageId);
```

### Performance Metrics

```csharp
// Track processing time
var sw = Stopwatch.StartNew();
// ... do work
_logger.LogInformation("Email search took {ElapsedMs}ms", sw.ElapsedMilliseconds);
```

---

## 📖 Resources

**Aspire Dashboard:**
- [Dashboard Overview](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/overview)
- [Dashboard Explore](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/explore)

**Logging:**
- [.NET Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [ASP.NET Core Logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/)

**OpenTelemetry:**
- [OpenTelemetry with Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/telemetry)

---

## 🎬 Quick Start

**To see logs for your WebApp right now:**

1. **Start Aspire:**
   ```bash
   aspire run
   ```

2. **Open Dashboard:**
   ```
   https://localhost:17134
   ```

3. **Go to Structured Logs:**
   - Click **"Structured"** tab
   - Filter: **Resource = "webapp"**
   - Filter: **Log Level = "Information"** (or All)

4. **Trigger a 404:**
   - In WebApp, navigate to: `/whatever`
   - Go back to Dashboard
   - Search for: "404"
   - **Should see**: "🔍 404 - Route not found: https://localhost:7136/whatever"

5. **If blank page persists:**
   - Check Console logs for errors
   - Look for exceptions in Structured logs
   - The blank page might be a rendering error we'll see in logs

---

**Dashboard is your debugging best friend!** 🕵️

All console output, structured logs, traces, and metrics are collected automatically by Aspire.

