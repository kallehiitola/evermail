# Evermail - Security Documentation

## Security Overview

Evermail handles sensitive email data and must maintain the highest security standards. This document outlines our security architecture, threat model, and compliance measures.

## Threat Model

### Assets to Protect
1. **Email Content**: Subject lines, body text, attachments
2. **User Credentials**: Passwords, 2FA secrets, JWT tokens
3. **Payment Information**: Managed by Stripe (PCI-DSS compliant)
4. **Metadata**: Email addresses, contact lists, timestamps
5. **API Keys**: Stripe keys, Azure OpenAI keys

### Threat Actors
1. **External Attackers**: Attempt unauthorized access to data
2. **Malicious Insiders**: Employees or contractors with elevated privileges
3. **Competing Tenants**: Multi-tenancy boundary violations
4. **Automated Bots**: Credential stuffing, brute force, scraping

### Attack Vectors
- SQL Injection
- Cross-Site Scripting (XSS)
- Cross-Site Request Forgery (CSRF)
- Authentication bypass
- Tenant isolation bypass
- File upload vulnerabilities (.mbox parsing exploits)
- API abuse and DDoS

## Authentication & Authorization

### User Authentication

#### Password Requirements
- Minimum 12 characters
- Must include: uppercase, lowercase, number, special character
- Hashed with **Argon2id** (not bcrypt due to better GPU resistance)
- Salted per-user

**Implementation** (ASP.NET Core Identity):
```csharp
services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = 100000; // Higher than default
});

services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});
```

#### Two-Factor Authentication (2FA)
- **TOTP** (Time-based One-Time Password) using Google Authenticator, Authy, etc.
- Backup codes (10 single-use codes)
- Enforced for admin accounts
- Optional but encouraged for all users

**Flow**:
1. User enables 2FA in settings
2. Server generates TOTP secret, displays QR code
3. User scans with authenticator app
4. User submits first code to verify setup
5. Server stores encrypted secret in database
6. Future logins require password + TOTP code

#### JWT Tokens
- **Access Token**: 15-minute expiration, contains user claims
- **Refresh Token**: 30-day expiration, stored in HTTP-only cookie
- Signed with **ES256** (ECDSA) instead of HS256 (more secure)
- Claims: `UserId`, `TenantId`, `Roles`, `Email`, `iat`, `exp`

**Token Generation**:
```csharp
var tokenHandler = new JsonWebTokenHandler();
var key = ECDsa.Create();
key.ImportECPrivateKey(keyBytes, out _);

var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(new[]
    {
        new Claim("sub", user.Id.ToString()),
        new Claim("tenant_id", user.TenantId.ToString()),
        new Claim("email", user.Email),
        new Claim("role", string.Join(",", user.Roles))
    }),
    Expires = DateTime.UtcNow.AddMinutes(15),
    Issuer = "https://api.evermail.com",
    Audience = "evermail-webapp",
    SigningCredentials = new SigningCredentials(
        new ECDsaSecurityKey(key), 
        SecurityAlgorithms.EcdsaSha256
    )
};

var token = tokenHandler.CreateToken(tokenDescriptor);
```

### Multi-Tenant Isolation

#### Database-Level Isolation
Every query **MUST** filter by `TenantId` to prevent cross-tenant data leakage.

**EF Core Global Query Filter**:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply to all tenant-scoped entities
    modelBuilder.Entity<EmailMessage>()
        .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    
    modelBuilder.Entity<Mailbox>()
        .HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
    
    modelBuilder.Entity<Attachment>()
        .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
}
```

**Tenant Context Resolver**:
```csharp
public class TenantContext
{
    public string TenantId { get; set; }
    public string UserId { get; set; }
}

// Resolve from JWT claims
services.AddScoped<TenantContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (httpContext?.User?.Identity?.IsAuthenticated != true)
        throw new UnauthorizedException("User not authenticated");
    
    var tenantId = httpContext.User.FindFirstValue("tenant_id");
    var userId = httpContext.User.FindFirstValue("sub");
    
    if (string.IsNullOrEmpty(tenantId))
        throw new SecurityException("TenantId claim missing");
    
    return new TenantContext { TenantId = tenantId, UserId = userId };
});
```

#### Blob Storage Isolation
- **Container Structure**: `mbox-archives/{tenantId}/`, `attachments/{tenantId}/`
- **SAS Tokens**: Scoped to tenant prefix with 15-minute expiry
- **Access Validation**: Verify tenant ownership before generating SAS token

```csharp
public async Task<string> GetAttachmentDownloadUrlAsync(Guid attachmentId)
{
    // 1. Verify attachment belongs to current tenant
    var attachment = await _context.Attachments
        .FirstOrDefaultAsync(a => a.Id == attachmentId);
    
    if (attachment == null || attachment.TenantId != _tenantContext.TenantId)
        throw new NotFoundException("Attachment not found");
    
    // 2. Generate short-lived SAS token
    var blobClient = _blobContainerClient.GetBlobClient(attachment.BlobPath);
    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = blobClient.BlobContainerName,
        BlobName = blobClient.Name,
        Resource = "b",
        StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
    };
    sasBuilder.SetPermissions(BlobSasPermissions.Read);
    
    var sasToken = blobClient.GenerateSasUri(sasBuilder);
    return sasToken.ToString();
}
```

### Role-Based Access Control (RBAC)

**Roles**:
- `User`: Standard user (read/write own data)
- `Admin`: Tenant admin (manage users, view billing)
- `SuperAdmin`: Platform admin (view all tenants, system config)

**Authorization**:
```csharp
[Authorize(Roles = "Admin")]
public class TenantManagementController : ControllerBase
{
    [HttpPost("users")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
    {
        // Admin can only invite users to their own tenant
        if (request.TenantId != _tenantContext.TenantId)
            return Forbid();
        
        // ... invite logic
    }
}

[Authorize(Roles = "SuperAdmin")]
public class PlatformAdminController : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        // SuperAdmin can view all tenants
        var tenants = await _context.Tenants.ToListAsync();
        return Ok(tenants);
    }
}
```

### Blazor UI Authorization Pattern
- ❌ Do **not** add `@attribute [Authorize]` to Razor components. HTTP middleware must stay open so the Blazor router can render redirects and 404s.
- ✅ Global guard lives in `Components/Routes.razor` via `<AuthorizeRouteView>` and `<RedirectToLogin />`.
- ✅ Every protected page wraps its content with `<AuthorizeView>` (set `Roles="..."` when needed) and renders `<CheckAuthAndRedirect />` for `<NotAuthorized>`. This component re-checks auth client-side after hydration and sends the user to `/login?returnUrl=...`.
- ✅ `<RequiresAuth />` is the visual fallback while the redirect is happening.

This keeps UX consistent (always redirect to login) and avoids raw 401 HTML responses for interactive routes.

### Development-Only AI Impersonation Helper
- Purpose: let the `@Browser` automation inspect protected UI screens without performing the entire OAuth/password flow.
- Scope: **Development environment only**. The middleware is short-circuited automatically when `IHostEnvironment.IsDevelopment()` is `false`.
- Trigger: append `?ai=1` (configurable via `AiImpersonation:TriggerQueryKey/TriggerValue`) to any local URL, for example `https://localhost:7136/mailboxes?ai=1`.
- Behavior:
  - After `UseAuthentication`, middleware loads the configured user (default `kalle.hiitola@gmail.com`), builds the usual claims (`sub`, `tenant_id`, roles, etc.), and assigns them to `HttpContext.User` so TenantContext and RBAC behave normally during SSR.
  - A dev-only endpoint `GET /api/v1/dev/ai-auth?ai=1` mints a real JWT/refresh pair for that user. A tiny `AiImpersonationBootstrapper` component detects `?ai=1`, calls the endpoint, drops the tokens into `localStorage`, and notifies the `CustomAuthenticationStateProvider` so interactive requests and API calls succeed without manual login.
  - `CheckAuthAndRedirect` now waits longer (up to ~6 seconds) before sending Browser flows to `/login` whenever the AI flag is present, giving the bootstrapper time to hydrate.
- Configuration (add only to `appsettings.Development.json`):
  ```json
  "AiImpersonation": {
    "Enabled": true,
    "TriggerQueryKey": "ai",
    "TriggerValue": "1",
    "UserEmail": "kalle.hiitola@gmail.com"
  }
  ```
- Safety: leave `Enabled` as `false` (or remove the section entirely) in any shared dev/staging/prod environment to avoid accidental bypasses. The helper never runs in production, but still avoid merging the configuration section outside local dev.
- **How to revert when no longer needed**:
  1. Set `AiImpersonation.Enabled` to `false` (or delete the section) in `appsettings.Development.json`, then restart Aspire so the middleware/endpoint short-circuits immediately.
  2. Remove the `?ai=1` query string from any bookmarked URLs; the bootstrapper will skip token acquisition and previously stored tokens remain untouched.
  3. (Optional hard delete) Remove `AiImpersonationBootstrapper`, the `GET /api/v1/dev/ai-auth` endpoint, and the middleware registration in `Program.cs` the next time you tidy up. Those files/components are only referenced from the dev helper, so deleting them reverts everything to the standard auth flow.

## Data Protection

### Encryption at Rest

#### Database (Azure SQL)
- **Transparent Data Encryption (TDE)**: Enabled by default
- Encrypts all data files, log files, backups
- Uses AES-256 encryption
- Microsoft-managed keys (can upgrade to customer-managed keys for Enterprise tier)

**Verification**:
```sql
SELECT name, is_encrypted
FROM sys.databases
WHERE name = 'Evermail';
```

#### Blob Storage
- **Azure Storage Service Encryption (SSE)**: Enabled by default
- AES-256 encryption
- Microsoft-managed keys
- Optional: Customer-managed keys (CMK) in Key Vault for compliance tier

**For GDPR Archive Tier** (immutable storage):
```bash
az storage account blob-service-properties update \
  --account-name evermailstorage \
  --enable-versioning true

az storage container immutability-policy create \
  --account-name evermailstorage \
  --container-name mbox-archives-gdpr \
  --period 2555 \ # 7 years in days
  --policy-mode Locked
```

### Encryption in Transit

#### TLS Configuration
- **Minimum Version**: TLS 1.2 (prefer TLS 1.3)
- **Cipher Suites**: Only strong ciphers (AES-GCM)
- **HSTS**: Strict-Transport-Security header enforced

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
    });
});

app.UseHsts();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});
```

#### Internal Service Communication
- Within Azure Container Apps Environment: encrypted by default
- Use managed identities instead of connection strings where possible

### Secret Management

#### Azure Key Vault
All secrets stored in Key Vault:
- Database connection strings (`ConnectionStrings--blobs`, `ConnectionStrings--queues`)
- SQL Server password (`sql-password` - dev only)
- Stripe API keys (when implemented)
- Azure OpenAI keys (Phase 2)
- JWT signing keys (future)

**Key Vault Resources:**
- **Dev**: `evermail-dev-kv` (resource group: `evermail-dev`)
- **Prod**: `evermail-prod-kv` (resource group: `evermail-prod`)

**Access in Code:**
The application uses Aspire's Key Vault integration which automatically:
- Uses `DefaultAzureCredential` for authentication
- Works with managed identities in Azure
- Falls back to Azure CLI credentials in local development
- Loads secrets into `IConfiguration` automatically

```csharp
// In Program.cs (automatically configured via Aspire resource reference)
builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");

// Use in code (secrets available via IConfiguration)
var connectionString = builder.Configuration.GetConnectionString("blobs");
var sqlPassword = builder.Configuration["sql-password"];
```

**Access Control (RBAC):**
Key Vaults use RBAC authorization. Grant access to Container Apps via managed identity:

```bash
# Get Container App managed identity principal ID
APP_IDENTITY_ID=$(az containerapp show --name evermail-webapp --resource-group evermail-prod-rg --query identity.principalId -o tsv)

# Grant Key Vault Secrets User role (read-only access to secrets)
az role assignment create \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/<sub-id>/resourceGroups/evermail-prod/providers/Microsoft.KeyVault/vaults/evermail-prod-kv \
  --assignee $APP_IDENTITY_ID
```

**Local Development:**
- Tries Key Vault first (if logged into Azure CLI via `az login`)
- Falls back to user secrets if Key Vault is not accessible
- Test Key Vault access: `curl -k https://localhost:7136/api/v1/dev/test-keyvault`
- Verify in logs: Look for "✅ Azure Key Vault secrets loaded" or "⚠️ Key Vault not accessible"

**Secret Naming Convention:**
- Use double dashes (`--`) for nested configuration keys (e.g., `ConnectionStrings--blobs`)
- Simple keys use single name (e.g., `sql-password`)

## Input Validation & Sanitization

### API Input Validation

**Use Data Annotations**:
```csharp
public record CreateMailboxRequest
{
    [Required]
    [MaxFileSize(5 * 1024 * 1024 * 1024)] // 5 GB for Pro tier
    [AllowedExtensions(".mbox")]
    public IFormFile File { get; init; }
}

public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly long _maxFileSize;

    public MaxFileSizeAttribute(long maxFileSize)
    {
        _maxFileSize = maxFileSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file && file.Length > _maxFileSize)
        {
            return new ValidationResult($"File size exceeds {_maxFileSize / 1024 / 1024} MB limit");
        }
        return ValidationResult.Success;
    }
}
```

### SQL Injection Prevention
- **Always use parameterized queries** (EF Core handles this automatically)
- Never concatenate user input into SQL strings
- Use stored procedures for complex queries

**Bad** ❌:
```csharp
var emails = await _context.EmailMessages
    .FromSqlRaw($"SELECT * FROM EmailMessages WHERE Subject LIKE '%{searchTerm}%'")
    .ToListAsync();
```

**Good** ✅:
```csharp
var emails = await _context.EmailMessages
    .Where(e => e.Subject.Contains(searchTerm))
    .ToListAsync();

// Or with raw SQL (parameterized)
var emails = await _context.EmailMessages
    .FromSqlInterpolated($"SELECT * FROM EmailMessages WHERE Subject LIKE {$"%{searchTerm}%"}")
    .ToListAsync();
```

### XSS Prevention

#### Blazor WebAssembly
- **Automatic escaping**: Blazor escapes HTML by default
- Use `@((MarkupString)html)` only for trusted content

```razor
@* Safe: automatically escaped *@
<p>@emailSubject</p>

@* Unsafe: only use for sanitized HTML *@
<div>@((MarkupString)sanitizedHtmlBody)</div>
```

#### HTML Sanitization
Use **HtmlSanitizer** library to clean user-generated HTML (email bodies):

```csharp
public string SanitizeHtmlBody(string html)
{
    var sanitizer = new HtmlSanitizer();
    
    // Allow safe tags only
    sanitizer.AllowedTags.Clear();
    sanitizer.AllowedTags.Add("p");
    sanitizer.AllowedTags.Add("br");
    sanitizer.AllowedTags.Add("strong");
    sanitizer.AllowedTags.Add("em");
    sanitizer.AllowedTags.Add("a");
    
    // Remove scripts, iframes, etc.
    sanitizer.AllowedAttributes.Clear();
    sanitizer.AllowedAttributes.Add("href");
    
    return sanitizer.Sanitize(html);
}
```

> **Ingestion note**: `MailboxProcessingService` stores both the `TextBody` and raw `HtmlBody` that MimeKit extracts so the viewer can render the original context. Treat every stored HTML fragment as untrusted input—always run it through the sanitizer above (or an equivalent CSP-safe renderer) before using `MarkupString`, otherwise hostile emails could persistently inject scripts into the UI.

### File Upload Security

#### .mbox File Validation
```csharp
public async Task<bool> ValidateMboxFile(Stream fileStream)
{
    // 1. Check magic bytes (first line should start with "From ")
    using var reader = new StreamReader(fileStream, leaveOpen: true);
    var firstLine = await reader.ReadLineAsync();
    if (!firstLine?.StartsWith("From ") ?? true)
    {
        _logger.LogWarning("Invalid mbox file: missing 'From ' header");
        return false;
    }
    
    // 2. Scan for malicious content (e.g., embedded executables)
    // Use ClamAV or Azure Defender for Storage
    
    // 3. Limit file size based on subscription tier
    if (fileStream.Length > GetMaxFileSizeForTier(_tenantContext.SubscriptionTier))
    {
        _logger.LogWarning("File size exceeds tier limit");
        return false;
    }
    
    return true;
}
```

#### Antivirus Scanning
Integrate with **Azure Defender for Storage** or **ClamAV**:

```bash
# Enable Defender for Storage
az security pricing create \
  --name StorageAccounts \
  --tier Standard \
  --resource-group evermail-prod-rg
```

## Audit Logging

### What to Log
- User authentication (login, logout, failed attempts)
- Email views (for compliance tier)
- Mailbox uploads, deletions
- Data exports (GDPR requests)
- Role changes
- API access from admin accounts

### Audit Log Structure
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; } // EmailViewed, MailboxUploaded, etc.
    public string ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } // JSON
}
```

### Implementation (Middleware)
```csharp
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, EmailDbContext db, TenantContext tenantContext)
    {
        // Log sensitive operations
        if (context.Request.Method == "POST" || context.Request.Method == "DELETE")
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                UserId = tenantContext.UserId,
                Action = $"{context.Request.Method} {context.Request.Path}",
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow
            };
            
            db.AuditLogs.Add(auditLog);
            await db.SaveChangesAsync();
        }
        
        await _next(context);
    }
}
```

## GDPR Compliance

### Right to Access
Users can export all their data via `/api/users/me/export`:
- All emails (as `.eml` files)
- Metadata (JSON)
- Audit logs
- Packaged as ZIP, available for 7 days

### Right to be Forgotten
Users can delete their account via `/api/users/me` (DELETE):
1. Soft-delete user account (mark as `Deleted`)
2. Delete all mailboxes from database (cascade to emails, attachments)
3. Delete all blobs from storage
4. Anonymize audit logs (replace `UserId` with `[deleted]`)
5. Cancel Stripe subscription
6. Wait 30 days, then hard-delete user record

### Mailbox Deletion Workflow
- UI/API actions (`rename`, `delete upload`, `delete emails`, `purge`) write to `MailboxDeletionQueue` (DB) and enqueue a message on the `mailbox-deletion` Azure Storage Queue.
- Worker processes queue messages using tenant-scoped credentials, deletes blobs first, then database rows, finally writes structured `AuditLogs` entries (`MailboxUploadDeleted`, `MailboxEmailsDeleted`, `MailboxPurged`).
- Default retention window: 30 days (`ExecuteAfter = RequestedAt + 30d`). SuperAdmins may pass `purgeNow=true` to bypass the retention window (still audited).
- Mailboxes stay visible while emails remain. Once both upload + emails are deleted, worker marks the mailbox as `SoftDeletedAt` and schedules hard delete.
- Deduplication (`ContentHash`) prevents re-importing duplicates. Each re-import is logged via `MailboxReimported`.

### Data Retention Policies
- **Free Tier**: 30 days
- **Pro Tier**: 1 year
- **Team Tier**: 2 years
- **GDPR Archive Tier**: 1-10 years (configurable, immutable)

### PII Detection (GDPR Archive Tier)
Use **Azure Cognitive Services Text Analytics** to detect PII:
```csharp
var client = new TextAnalyticsClient(endpoint, credential);
var response = await client.RecognizePiiEntitiesAsync(emailBody);

foreach (var entity in response.Value)
{
    await _context.PIIDetections.AddAsync(new PIIDetection
    {
        EmailMessageId = emailId,
        TenantId = tenantId,
        PIIType = entity.Category.ToString(),
        Value = entity.Text,
        Confidence = entity.ConfidenceScore,
        FoundIn = "TextBody"
    });
}
```

## Rate Limiting & DDoS Protection

### API Rate Limiting
Use **AspNetCoreRateLimit** library:

```csharp
services.AddMemoryCache();
services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1h",
            Limit = GetLimitForTier("Free") // 100
        }
    };
});

services.AddInMemoryRateLimiting();
app.UseIpRateLimiting();
```

### Azure Front Door (Optional)
For production, use **Azure Front Door** with WAF (Web Application Firewall):
- DDoS protection (L3/L4)
- Rate limiting per IP
- Geo-filtering (block specific countries)
- OWASP top 10 protection

```bash
az network front-door create \
  --resource-group evermail-prod-rg \
  --name evermail-frontdoor \
  --backend-address evermail-webapp.azurecontainerapps.io

az network front-door waf-policy create \
  --resource-group evermail-prod-rg \
  --name evermailWafPolicy \
  --sku Premium_AzureFrontDoor
```

## Incident Response Plan

### Detection
- **Real-time Alerts**: Application Insights, Azure Security Center
- **Anomaly Detection**: Unusual login locations, high failed auth attempts
- **SIEM Integration**: Export logs to Azure Sentinel (optional)

### Response Workflow
1. **Detect**: Alert triggered (e.g., "50 failed logins from same IP")
2. **Assess**: Review logs, determine severity
3. **Contain**: Block IP, disable compromised account
4. **Eradicate**: Patch vulnerability, rotate secrets
5. **Recover**: Restore from backup if needed
6. **Post-Mortem**: Document incident, update security policies

### Communication
- **Internal**: Notify CTO, dev team via Slack/Teams
- **External**: Email affected users within 72 hours (GDPR requirement)
- **Public**: Status page update (https://status.evermail.com)

## Security Checklist (Pre-Production)

- [ ] All secrets stored in Azure Key Vault
- [ ] TLS 1.2+ enforced, HSTS enabled
- [ ] SQL injection prevention verified (use parameterized queries)
- [ ] XSS prevention verified (HTML sanitization)
- [ ] CSRF protection enabled (ASP.NET Core anti-forgery tokens)
- [ ] Rate limiting configured
- [ ] 2FA enforced for admin accounts
- [ ] Audit logging enabled for sensitive operations
- [ ] Database TDE enabled
- [ ] Blob storage encryption enabled
- [ ] Multi-tenant isolation tested (cannot access other tenant's data)
- [ ] File upload validation and scanning
- [ ] JWT tokens signed with strong algorithm (ES256)
- [ ] Password policy enforced (12+ chars, complexity)
- [ ] GDPR export/delete workflows tested
- [ ] Backup and restore procedures tested
- [ ] Incident response plan documented and team trained
- [ ] Penetration testing completed (hire external firm)

---

**Last Updated**: 2025-11-11  
**Security Contact**: security@evermail.com  
**Next Security Audit**: Quarterly

