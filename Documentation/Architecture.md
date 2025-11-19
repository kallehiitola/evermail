# Evermail - System Architecture

## Executive Summary

Evermail is a cloud-based SaaS platform that enables users to upload, view, search, and analyze email archives from .mbox files. Built on Microsoft Azure using .NET 8 and Azure Aspire, the system is designed for scalability, security, and cost-effectiveness.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Users / Clients                          │
└───────────────┬─────────────────────────────────┬───────────────┘
                │                                 │
        ┌───────▼────────┐              ┌────────▼────────┐
        │   Web App UI   │              │  Admin Dashboard│
        │ (Blazor WASM)  │              │ (Blazor Server) │
        └───────┬────────┘              └────────┬────────┘
                │                                 │
        ┌───────▼─────────────────────────────────▼───────┐
        │           WebApp API (ASP.NET Core)             │
        │  - Authentication (ASP.NET Identity)            │
        │  - Search APIs                                  │
        │  - Mailbox Management                           │
        │  - Billing Portal                               │
        └───┬─────────────┬───────────────┬───────────────┘
            │             │               │
    ┌───────▼─────┐  ┌────▼────────┐  ┌─▼────────────────┐
    │ Azure Queue │  │  Azure SQL  │  │  Blob Storage    │
    │   (Jobs)    │  │ Serverless  │  │  - mbox files    │
    └───────┬─────┘  │  Database   │  │  - attachments   │
            │        └─────────────┘  └──────────────────┘
    ┌───────▼──────────────┐
    │  Ingestion Worker    │
    │  - Parse .mbox       │
    │  - Extract metadata  │
    │  - Store in DB       │
    └──────────────────────┘
```

## Component Architecture

### 1. Frontend Layer

#### User Web App (Blazor Web App)
- **Purpose**: User-facing web application for mailbox management and search
- **Technology**: Blazor Web App (hybrid SSR + Interactive WebAssembly) with MudBlazor UI components
- **Rendering Strategy**:
  - **Static SSR**: Landing pages, marketing (fast load, SEO-friendly)
  - **Interactive Server**: Search, real-time features
  - **Interactive WASM**: Email viewer, rich interactions
- **Future**: Share Razor components with .NET MAUI mobile app (Phase 2)
- **Key Features**:
  - User registration and authentication
  - .mbox file upload (up to 5GB)
  - Email search interface with full-text and AI-powered search
  - Email viewer with HTML rendering and attachment download
  - Account settings and billing portal (Stripe)
- **Deployment**: Azure Static Web Apps or served from WebApp API

##### Product UI & UX Principles (November 2025 refresh)
Apply these rules whenever you touch the Blazor surface so future prompts inherit the same mental model.

**Brand Foundation**
- Use the updated infinity logo (`wwwroot/evermail_logo.png`) next to the lowercase text mark (`.evermail-logo__word`). Keep the wordmark color `#49d9c9`, font-weight 600, and letter-spacing 0.04 em. Never recolor the loop outside of the documented gradient.
- All color decisions flow through `wwwroot/app.css` tokens. Key tokens:  
  `--color-brand-primary #2563EB`, `--color-brand-accent #06B6D4`, `--color-brand-gradient linear-gradient(120deg,#2563EB 0%,#06B6D4 70%)`, base surface `--color-app-bg #F8FAFC` (light) / `#020617` (dark). When adding elements, reference these variables instead of hard-coded hex values.
- Typography: Inter → Segoe UI → system stack, `font-size: 1rem` base with 1.5 line-height. Section labels (“hero eyebrows”) are uppercase, tracking 0.2 em, `font-size: .85rem`.

**Theme + Accessibility**
- Theme choice lives in `ThemeService` (`wwwroot/js/theme.js`). Always bind new Razor components to `data-theme` rather than inventing new state.  
- Ensure every new component inherits both light and dark variations: add selectors under `:root[data-theme='dark']` when necessary.  
- Maintain 4.5:1 contrast for primary text and 3:1 for icons. Default text color tokens already comply; keep them.

**Layout System**
- Wrappers: Authenticated pages use `.home-wrapper` (max-width 1200 px, `gap: 3rem`). Public/auth flows share `.auth-wrapper`.
- Hero blocks (`.page-hero`, `.detail-hero`, `.auth-hero`): short eyebrow → H1 (24–40 px) → one supporting sentence. Right-hand column holds CTAs or stat chips (never more than 3 metrics).
- Cards: prefer `.modern-card`, `.table-card`, `.settings-card`, `.auth-card`. They provide 26–32 px radius, subtle border, and drop shadows defined via `--shadow-sm` / `--shadow-lg`. Avoid Bootstrap default cards.
- Spacing: Top-level sections get `margin-bottom: 3rem`. Keep consistent gutters using CSS variables; never reintroduce `.container mt-4`.

**Component Patterns**
- **Mailboxes & Emails**: Use `.stat-grid` for summary counters, `.data-table` for tabular content, `.action-pill` for row actions, `.status-pill` for lifecycle cues. Buttons inside tables should be `<button type="button">` or `<a>`; no `<div>` click handlers.
- **Modals**: Use the “detail modal” stack from `Mailboxes.razor`:  
  `<div class="modal fade show detail-modal d-block" tabindex="-1">` → `.glass-panel` content → `.modal-overlay` backdrop. This ensures the glowing border and allows button clicks.  
- **Upload**: Drag-and-drop area uses `<InputFile>` + `.upload-dropzone`. Keep `@ondragenter/leave` states toggling `.upload-dropzone--drag` class. The progress bar uses `.usage-progress.jumbo`.  
- **Auth**: Login/Register keep SSO buttons first (`.social-btn--google` then `.social-btn--microsoft`), followed by divider, then email/password form. CTA text should be short (“Continue with Google”). Buttons get hover shadows; add `type="button"` to SSO controls.
- **Buttons**: Primary CTAs use gradient backgrounds + `box-shadow: 0 18px 35px rgba(37,99,235,.35)` (already defined). Secondary actions use `.btn-outline-secondary` with 1px border referencing `--color-border`. Danger flows (delete/purge) use `.btn-outline-danger` + `status-pill--danger`.

**Interaction & Copy**
- “Trust the whitespace”: prefer 2–3 short sentences per section, no dense paragraphs.
- Provide immediate feedback for async operations: `spinner-border-sm` inside buttons, progress bars for uploads/ingestion, alert banners for errors.
- Keep table/summary copy in sentence case, e.g., “Pending deletion” instead of all caps.
- Always describe destructive actions with a `Danger Zone` eyebrow and reinforce retention rules (see current delete modal copy).

**Implementation Checklist**
1. Wrap new views in the correct layout container (`home-wrapper`, `auth-wrapper`, etc.).
2. Use CSS tokens for colors and backgrounds; extend `app.css` when necessary.
3. Ensure dark-mode variant by inspecting `:root[data-theme='dark']`.
4. Use the shared component classes before inventing new ones. If a new pattern is unavoidable, document it here and in `app.css`.
5. Validate keyboard accessibility (Tab order, focus states). All clickable items should be button/anchor elements, not bare `<div>`s.

Adhering to these rules keeps every surface (dashboard, mailboxes, upload, auth, settings) visually cohesive and future AI prompts can extend the system without guesswork.

#### Admin Dashboard (Blazor Server)
- **Purpose**: Internal operations and monitoring
- **Technology**: Blazor Server for real-time updates
- **Key Features**:
  - User and tenant management
  - Mailbox processing status monitoring
  - Storage usage analytics
  - Payment and subscription management
  - Error logs and job queue monitoring
- **Access Control**: Admin-only, protected by role-based claims

#### Mobile App (.NET MAUI Blazor Hybrid - Phase 2)
- **Purpose**: Native mobile experience for iOS and Android
- **Technology**: .NET MAUI Blazor Hybrid with shared Razor components
- **Architecture**:
  - Shared Razor Component Library (RCL) between web and mobile
  - 80-90% code reuse from web app
  - Native platform features (camera, offline, push notifications)
- **Key Features**:
  - All web app features
  - Offline email viewing
  - Push notifications for new emails
  - Native file picker for mbox upload
  - Biometric authentication
- **Distribution**: iOS App Store, Google Play Store
- **Timeline**: Phase 2 (after MVP, month 6-12)

### 2. Application Layer

#### WebApp API (ASP.NET Core)
- **Purpose**: Main API gateway for all client operations
- **Architecture**: Clean Architecture with CQRS pattern for complex operations
- **Key Responsibilities**:
  - **Authentication & Authorization**
    - ASP.NET Core Identity with JWT tokens
    - Multi-tenant context resolution from claims
    - Role-based access control (User, Admin, SuperAdmin)
  - **Mailbox Management**
    - Handle file uploads to Blob Storage
    - Enqueue processing jobs
    - Track mailbox processing status
  - **Search APIs**
    - Full-text search using SQL Server FTS (Subject, Text, HTML, Recipient blobs)
    - Advanced filtering (date range, sender, recipient, conversation/thread)
    - AI-powered semantic search (Phase 2)
  - **Billing Integration**
    - Stripe Checkout session creation
    - Customer Portal redirect
    - Webhook handling for subscription events
  - **User Management**
    - Profile management
    - Data export (GDPR)
    - Account deletion with cascade cleanup

#### Ingestion Worker (Background Service)
- **Purpose**: Asynchronous .mbox file processing
- **Technology**: .NET BackgroundService with Azure Queue trigger
- **Processing Pipeline**:
  1. **Receive Job Message**
     - Message format: `{ TenantId, UserId, MailboxId, BlobPath }`
  2. **Download .mbox from Blob Storage**
     - Stream file (never load fully into memory)
  3. **Parse with MimeKit**
     - Use `MimeParser` with `MimeFormat.Mbox`
     - Process in batches of 500 messages
     - Handle corrupt messages gracefully (log and skip)
  4. **Extract Email Data**
     - Metadata: MessageId, In-Reply-To, References, From/Sender/Reply-To, Return-Path, List-Id, Thread-Topic, Importance/Priority, Categories
     - Recipients: Normalize To/Cc/Bcc/Reply-To/Sender into `EmailRecipients` + JSON arrays for backwards compatibility
     - Threading: Derive a deterministic `ConversationKey`, link to (or create) an `EmailThread`, set `ThreadDepth`, update participant roster and message counts
     - Content: Snippet (first 200 chars), Text body, HTML body (stored + indexed)
     - Attachments: Save to Blob Storage, store reference in DB
  5. **Store in Database**
     - Bulk insert in batches for performance
     - Update mailbox processing status + `MailboxUpload` progress
  6. **Error Handling**
     - Retry failed jobs (exponential backoff)
     - Mark mailbox as "Failed" after 3 attempts
     - Notify user via email

#### Migration Service (Evermail.MigrationService)
- **Purpose**: Apply Entity Framework Core migrations before any other Aspire project starts.
- **Lifecycle**:
  1. Aspire’s AppHost launches the `migrations` project first.
  2. `Evermail.MigrationService` connects to Azure SQL using the same connection string as WebApp/Worker.
  3. `dotnet ef database update` equivalent runs automatically and exits on success.
  4. AppHost waits for the process to report `Finished waiting for resource 'migrations'` before starting WebApp, Worker, Admin.
- **Why it matters**: all schema additions (`MailboxUploads`, `MailboxDeletionQueue`, `ContentHash` indexes, etc.) ship in migrations and are guaranteed to exist in every environment without manual SQL.
- **Manual run** (for troubleshooting): `dotnet run --project Evermail.MigrationService` from repository root.

#### Mailbox Lifecycle & Deletion Services
- **Components**:
  - `Evermail.Infrastructure.Services.QueueService` publishes to two queues: `mailbox-ingestion` (parse uploads) and `mailbox-deletion` (recycle-bin cleanup).
  - `MailboxProcessingService` now receives `{ MailboxId, UploadId }`, computes a SHA-256 `ContentHash`, and skips duplicates per mailbox/upload pair.
  - `MailboxDeletionService` executes jobs from `MailboxDeletionQueue`, scrubs blobs, optionally keeps indexed emails, and records audit events.
- **Workflow**:
  1. WebApp creates a `MailboxUpload` row for every upload or re-import.
  2. `mailbox-ingestion` queue triggers the worker, which streams the blob, batches inserts (500), uploads attachments, and updates both `Mailbox` and `MailboxUpload` statistics.
  3. Users schedule cleanup via `POST /mailboxes/{id}/delete`. A `MailboxDeletionQueue` row is created and a message is placed on `mailbox-deletion`.
  4. Worker polls `mailbox-deletion`; once `ExecuteAfter` arrives it deletes blobs first, then emails, then optionally the mailbox record (when both upload+emails are gone). Purge windows default to 30 days unless SuperAdmins set `purgeNow`.
  5. Every state change writes to `AuditLogs`, enabling GDPR traceability.

#### Threading, Recipient Indexing & Search Surfaces
- **Conversation graph**: Every email rolls up to `EmailThreads` via normalized `ConversationKey` (first reference → In-Reply-To → MessageId). Threads store participant summary, message counts, first/last timestamps, and power UI grouping.
- **Recipient index**: `EmailRecipients` table (plus a flattened `RecipientsSearch` column) enables instant filtering by To/Cc/Bcc/Reply-To/Sender without scanning JSON payloads.
- **Full-text coverage**: SQL Server CONTAINSTABLE now searches `Subject`, `TextBody`, `HtmlBody`, `RecipientsSearch`, `FromName`, and `FromAddress`, matching Microsoft Learn guidance for multi-column FTS catalogues. The endpoint falls back to `LIKE` queries across the same set when FTS is unavailable.

#### Search Indexer (Optional, Phase 2)
- **Purpose**: Synchronize database to Azure AI Search
- **Technology**: .NET BackgroundService with change feed
- **Responsibilities**:
  - Generate embeddings for semantic search
  - Push documents to Azure AI Search index
  - Handle index refresh and updates

### 3. Data Layer

#### Azure SQL Serverless Database
- **Choice Rationale**: Cost-effective, auto-pause when idle, excellent full-text search
- **Key Tables**:
  - `Tenants` - SaaS tenant isolation
  - `Users` - User accounts and authentication
  - `Mailboxes` - Uploaded mbox metadata and processing status
  - `EmailMessages` - Core email data
  - `Attachments` - Attachment metadata (files in Blob)
  - `Workspaces` - Shared archive spaces (Phase 2)
  - `AuditLogs` - GDPR compliance and security auditing
  - `SubscriptionPlans` - Stripe subscription tracking

**Schema Highlights**:
```sql
-- Multi-tenancy enforcement
CREATE TABLE EmailMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId NVARCHAR(64) NOT NULL,
    UserId NVARCHAR(64) NOT NULL,
    MailboxId UNIQUEIDENTIFIER NOT NULL,
    MessageId NVARCHAR(512),
    Subject NVARCHAR(1024),
    FromAddress NVARCHAR(512) NOT NULL,
    FromName NVARCHAR(512),
    ToAddresses NVARCHAR(MAX),
    CcAddresses NVARCHAR(MAX),
    Date DATETIME2 NOT NULL,
    Snippet NVARCHAR(512),
    TextBody NVARCHAR(MAX),
    HtmlBody NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_EmailMessages_Tenant_User (TenantId, UserId),
    INDEX IX_EmailMessages_Date (Date),
    CONSTRAINT FK_EmailMessages_Mailbox FOREIGN KEY (MailboxId) 
        REFERENCES Mailboxes(Id) ON DELETE CASCADE
);

-- Full-text search catalog
CREATE FULLTEXT CATALOG EmailSearchCatalog;
CREATE FULLTEXT INDEX ON EmailMessages(Subject, TextBody, FromName)
    KEY INDEX PK_EmailMessages
    ON EmailSearchCatalog;
```

#### Azure Blob Storage
- **Container Structure**:
  - `mbox-archives/{tenantId}/{mailboxId}/original.mbox` - Original uploaded file
  - `attachments/{tenantId}/{mailboxId}/{messageId}/{filename}` - Email attachments
  - `exports/{tenantId}/{exportId}/export.zip` - GDPR data exports (temp, 7-day expiry)
- **Lifecycle Policies**:
  - Hot tier → Cool tier after 90 days (configurable per plan)
  - Delete exports after 7 days
- **Security**:
  - Private containers (no public access)
  - SAS tokens with 15-minute expiry for user downloads
  - Encryption at rest (Azure managed keys)
  - Immutable storage for GDPR Archive tier (WORM)

#### Azure Storage Queues
- **Queues**:
  - `mailbox-ingestion` – drives mbox parsing/indexing
  - `mailbox-deletion` – drives recycle-bin cleanup and hard deletion
- **Ingestion Message Format**:
```json
{
  "tenantId": "tenant-123",
  "userId": "user-456",
  "mailboxId": "guid",
  "uploadId": "guid",
  "blobPath": "mbox-archives/tenant-123/mailbox-guid/original.mbox",
  "fileSizeBytes": 10485760,
  "enqueuedAt": "2025-11-11T10:30:00Z"
}
```
- **Deletion Message Format**:
```json
{
  "tenantId": "tenant-123",
  "mailboxId": "guid",
  "uploadId": "guid-or-null",
  "deleteUpload": true,
  "deleteEmails": false,
  "requestedBy": "user-456",
  "jobId": "guid"
}
```
- **Processing**: Worker service polls both queues with 5-minute visibility timeout, max 3 retries (poison queue after that). Azure Container Apps horizontal scaling (KEDA) can fan out to hundreds/thousands of worker replicas because the queues decouple work from HTTP traffic.

### 4. External Integrations

#### Stripe Payment Processing
- **Integration Points**:
  1. **Checkout Flow**
     - Create Stripe Customer on user registration
     - Create Checkout Session for plan selection
     - Redirect to Stripe-hosted payment page
     - Handle success/cancel redirects
  2. **Webhooks** (`/api/webhooks/stripe`)
     - `checkout.session.completed` → Activate subscription
     - `invoice.payment_succeeded` → Extend subscription
     - `invoice.payment_failed` → Send notification, downgrade after grace period
     - `customer.subscription.deleted` → Downgrade to free tier
  3. **Customer Portal**
     - Generate portal session for users to manage subscriptions
     - Update payment methods, view invoices, cancel subscriptions
- **Security**: Verify webhook signatures using Stripe signing secret

#### Azure AI Services (Phase 2)
- **Azure OpenAI**:
  - Models: GPT-4o for summaries, GPT-4o-mini for quick responses
  - Use cases: Email summarization, smart search, entity extraction
- **Azure AI Search**:
  - Semantic search with embeddings
  - Advanced filtering and faceting
  - Query cost: ~€30-60/month for Basic tier
- **Azure Form Recognizer**:
  - Extract invoice data from attachments
  - OCR for scanned documents

#### OAuth Integrations (Phase 2+)
- **Gmail API**: Direct import from Google account (OAuth 2.0)
- **Microsoft Graph API**: Import from Outlook.com and Office 365
- **Flow**:
  1. User initiates OAuth consent
  2. Receive access/refresh tokens
  3. Fetch emails via API (paginated)
  4. Convert to MIME format and process via standard pipeline

## Design Patterns

### Multi-Tenancy Pattern
**Strategy**: Shared database with tenant isolation (MVP), Elastic pools for scale (Phase 2)

#### Phase 1: Shared Database (MVP - 0-100 users)

**Recommended by Microsoft Learn for SaaS startups**

```csharp
// Global query filter in EF Core
modelBuilder.Entity<EmailMessage>()
    .HasQueryFilter(e => e.TenantId == _currentTenant.Id);

// Tenant context resolver
public class TenantContext
{
    public string TenantId { get; set; }
    public string UserId { get; set; }
}

// Resolve from JWT claims
services.AddScoped<TenantContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var tenantId = httpContext.User.FindFirst("TenantId")?.Value;
    var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return new TenantContext { TenantId = tenantId, UserId = userId };
});
```

**Cost**: €15-30/month  
**Capacity**: Up to 1000 tenants  
**Benefits**: Lowest cost, simplest management

#### Phase 2: Hybrid Model with Elastic Pools (100-1000 users)

**Microsoft Learn recommended for growing SaaS**

```
Azure SQL Elastic Pool (€100-200/month)
├── Shared Database
│   ├── Free tier tenants (60%)
│   └── Pro tier tenants (30%)
│
└── Dedicated Databases (in elastic pool)
    ├── Team tenant databases (8%)
    └── Enterprise tenant databases (2%)
```

**Benefits**:
- ✅ **Cost optimization** - Share compute across databases
- ✅ **Tenant isolation** - Premium tiers get dedicated databases
- ✅ **Easy cost tracking** - Per-database metrics in Azure
- ✅ **Noisy neighbor protection** - Elastic pool handles resource balancing

**Implementation**:
```csharp
public class TenantDatabaseResolver
{
    public async Task<string> GetConnectionStringAsync(string tenantId)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        
        return tenant.SubscriptionTier switch
        {
            "Free" => _config["ConnectionStrings:SharedDatabase"],
            "Pro" => _config["ConnectionStrings:SharedDatabase"],
            "Team" => $"Server=...;Database=tenant_{tenantId};...",
            "Enterprise" => $"Server=...;Database=tenant_{tenantId};...",
            _ => _config["ConnectionStrings:SharedDatabase"]
        };
    }
}
```

#### Phase 3: Sharding (1000+ users)

**For massive scale**

```
Multiple Shards
├── Shard 1 (West Europe) - Tenants 1-500
├── Shard 2 (North Europe) - Tenants 501-1000
└── Shard Map Database (tenant routing)
```

### CQRS (Command Query Responsibility Segregation)
Separate read and write operations for complex scenarios:

```csharp
// Commands (writes)
public record CreateMailboxCommand(string TenantId, string UserId, Stream FileStream);
public record DeleteMailboxCommand(Guid MailboxId);

// Queries (reads)
public record SearchEmailsQuery(string TenantId, string UserId, string SearchTerm, int Page);
public record GetEmailDetailQuery(Guid EmailId);

// Handlers
public class CreateMailboxHandler : IRequestHandler<CreateMailboxCommand, Guid>
{
    public async Task<Guid> Handle(CreateMailboxCommand request, CancellationToken ct)
    {
        // Upload to blob, create DB record, enqueue job
    }
}
```

### Repository Pattern
Abstract data access for testability:

```csharp
public interface IEmailRepository
{
    Task<EmailMessage?> GetByIdAsync(Guid id, string tenantId);
    Task<PagedResult<EmailMessage>> SearchAsync(SearchCriteria criteria);
    Task AddRangeAsync(IEnumerable<EmailMessage> emails);
}

public class EmailRepository : IEmailRepository
{
    private readonly EmailDbContext _context;
    
    public async Task<EmailMessage?> GetByIdAsync(Guid id, string tenantId)
    {
        return await _context.EmailMessages
            .Where(e => e.Id == id && e.TenantId == tenantId)
            .FirstOrDefaultAsync();
    }
    // ...
}
```

## Security Architecture

### Authentication Flow
1. User submits email + password
2. API validates credentials via ASP.NET Identity
3. Generate JWT token with claims: `UserId`, `TenantId`, `Roles`
4. Client stores JWT in localStorage (WASM) or cookie (Server)
5. Client includes JWT in Authorization header for API requests
6. API validates JWT and resolves tenant context

### Authorization Layers
1. **API Level**: `[Authorize]` attribute on controllers
2. **Tenant Level**: Automatic filtering via EF Core query filters
3. **Resource Level**: Check ownership before operations
4. **Role Level**: Admin-only endpoints use `[Authorize(Roles = "Admin")]`

#### Blazor Authorization Flow (UI)
- ❌ Do **not** use `@attribute [Authorize]` on Blazor components. It prevents the router from rendering redirects and 404 pages.
- ✅ `Components/Routes.razor` wraps the app in `<AuthorizeRouteView>` and renders `<RedirectToLogin />` whenever authentication fails.
- ✅ Each protected page/component must:
  - Set an appropriate `@rendermode` (usually `InteractiveServer`).
  - Wrap its content in `<AuthorizeView>` (optionally with `Roles="..."`).
  - Render `<CheckAuthAndRedirect />` inside `<NotAuthorized>` so anonymous users are redirected to `/login?returnUrl=...`.
- ✅ `<CheckAuthAndRedirect />` handles client-side token validation after hydration and shows `<RequiresAuth />` as a fallback.

This pattern keeps HTTP responses at 200 for interactive routes, lets the Blazor app issue consistent redirects, and ensures the router can still display proper 404 pages.

### Data Protection
- **In Transit**: TLS 1.3 for all connections
- **At Rest**: 
  - Azure SQL TDE enabled by default
  - Blob Storage encryption with Microsoft-managed keys
  - Consider customer-managed keys (CMK) for Enterprise tier
- **Secrets**: Azure Key Vault for connection strings, API keys
- **PII Detection**: Azure Cognitive Services for GDPR compliance tier

### Audit Logging
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string TenantId { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; } // EmailViewed, MailboxDeleted, DataExported
    public string ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
}
```

## Scalability & Performance

### Horizontal Scaling
- **Web API**: Auto-scale Azure Container Apps based on CPU/memory
- **Ingestion Worker**: Scale based on queue depth (e.g., 1 instance per 100 messages)
- **Database**: SQL Serverless auto-scales compute; consider read replicas at 1000+ users

### Performance Optimizations
1. **Caching**:
   - User profile and permissions (Redis, optional)
   - Search results (5-minute TTL)
   - Mailbox metadata
2. **Database**:
   - Compiled queries for frequently-used searches
   - Pagination (max 100 results per page)
   - `AsNoTracking()` for read-only queries
3. **Blob Storage**:
   - CDN for static assets (future)
   - Parallel upload for large files (chunked upload)
4. **Search**:
   - SQL FTS for MVP (fast, cheap)
   - Azure AI Search for >500GB indexed data

### Monitoring & Observability
- **Azure Application Insights**:
  - Track request latency (p50, p95, p99)
  - Monitor exception rates
  - Custom metrics: mailbox processing time, search queries/sec
- **Aspire Dashboard** (dev): Real-time telemetry for all services
- **Alerts**:
  - High error rate (>5% in 5 minutes)
  - Queue depth >1000 for >10 minutes
  - Database DTU >80%
  - Blob storage >90% of quota

## Disaster Recovery & Business Continuity

### Backup Strategy
- **Database**: Automated daily backups (Azure SQL retention: 7 days)
- **Blob Storage**: Geo-redundant storage (GRS) for mbox-archives container
- **Configuration**: Infrastructure-as-Code via Bicep/Terraform
- **Secrets**: Backed up in Azure Key Vault with soft-delete enabled

### Recovery Objectives
- **RTO (Recovery Time Objective)**: 4 hours
- **RPO (Recovery Point Objective)**: 24 hours (daily backups)
- **Availability Target**: 99.5% (4 hours downtime/month acceptable for side-hustle)

### Failure Scenarios
| Scenario | Impact | Mitigation |
|----------|--------|------------|
| API container crash | User can't access | Auto-restart by Container Apps, scale to 2+ instances |
| Worker container crash | Delayed processing | Jobs remain in queue, worker restarts and resumes |
| SQL database outage | Full outage | Azure SQL HA (99.99%), manual failover to geo-replica |
| Blob storage outage | Can't upload/download | GRS replication, failover to secondary region |
| Stripe webhook failure | Payment not reflected | Webhook retry logic, manual reconciliation dashboard |

## Cost Model

### Infrastructure Costs (Monthly, Estimated)
| Resource | Configuration | Cost (EUR) |
|----------|---------------|------------|
| Azure SQL Serverless | 2 vCores, 10GB storage | €15-30 |
| Blob Storage (Hot) | 100GB data, 1000 ops/day | €5 |
| Storage Queue | 10K messages/day | €1 |
| Container Apps | 3 containers, 0.5 vCPU, 1GB RAM each | €40-60 |
| Application Insights | 5GB ingestion/month | €10 |
| Key Vault | Secrets only | €1 |
| **Total (MVP)** | | **€72-107** |

### Variable Costs (Per User)
- Storage: €0.008/GB/month (blob) + €0.05/GB/month (DB)
- Compute: ~€0.50/month per active user (amortized)
- Stripe fees: 2.9% + €0.30 per transaction

**Break-even**: ~20 paying users at €9/month = €180 revenue vs €100 infra cost

## Technology Stack Summary

| Layer | Technology | Version | Purpose |
|-------|------------|---------|---------|
| **Runtime** | .NET | **9.0** | Core platform (latest) |
| **Orchestration** | Azure Aspire | **9.4** | Service orchestration |
| **Frontend (Web)** | **Blazor Web App** | .NET 9 | **Hybrid SSR + WASM rendering** |
| **Frontend (Mobile)** | **.NET MAUI Blazor Hybrid** | .NET 9 | **Phase 2 - Native mobile apps** |
| **Admin** | Blazor Server | .NET 9 | Real-time dashboard |
| **UI Framework** | MudBlazor | 7.0+ | Component library |
| **Database** | Azure SQL Serverless | Latest | Relational data + FTS |
| **ORM** | Entity Framework Core | 8.0+ | Data access |
| **Storage** | Azure Blob Storage | V12 | File storage |
| **Queue** | Azure Storage Queue | V12 | Background jobs |
| **Email Parser** | MimeKit | 4.0+ | .mbox parsing |
| **Authentication** | ASP.NET Core Identity | 8.0 | User auth |
| **Payment** | Stripe.net | Latest | Subscription billing |
| **AI (Phase 2)** | Azure OpenAI | Latest | GPT-4, embeddings |
| **Search (Phase 2)** | Azure AI Search | Latest | Semantic search |
| **Testing** | xUnit + Playwright | Latest | Unit & E2E tests |

## Decision Log

| Date | Decision | Rationale | Source |
|------|----------|-----------|--------|
| 2025-11-11 | **Use .NET 9** (not .NET 8) | Latest version, Aspire 9.4 support, performance improvements | Microsoft Learn MCP |
| 2025-11-11 | **Use Blazor Web App** (hybrid SSR+WASM) | Better performance, SEO, flexibility vs pure WASM | Microsoft Learn MCP |
| 2025-11-11 | **Plan .NET MAUI Hybrid for mobile** (Phase 2) | 80-90% code reuse, native features, single codebase | Microsoft Learn MCP |
| 2025-11-11 | **Use Azure SQL Serverless** over PostgreSQL | Auto-pause, elastic pools support, better multi-tenant features | Microsoft Learn MCP |
| 2025-11-11 | **Shared database with TenantId** for MVP | Lowest cost, best for SaaS, validated by Microsoft docs | Microsoft Learn MCP |
| 2025-11-11 | **Add Elastic Pools in Phase 2** | Cost optimization when 50+ databases, resource sharing | Microsoft Learn MCP |
| 2025-11-11 | **Hybrid blob storage** (shared + dedicated) | Shared for Free/Pro, dedicated for Enterprise cost tracking | Microsoft Learn MCP |
| 2025-11-11 | Use Storage Queue over Service Bus | Simpler, cheaper for MVP; migrate to Service Bus if need advanced routing | Architecture review |
| 2025-11-11 | Use MimeKit over custom parser | Battle-tested, handles corrupt mbox gracefully, actively maintained | Architecture review |
| 2025-11-11 | Use Stripe over Paddle | Better .NET SDK, more flexible for metered billing, standard for SaaS | Architecture review |
| 2025-11-11 | Phase AI features to v2 | Get to paying customers faster, validate core value prop first | Business strategy |

## Next Steps

1. **MVP Development** (Weeks 1-4)
   - Set up Aspire solution structure
   - Implement authentication and tenant isolation
   - Build mbox ingestion pipeline
   - Create basic search UI
   
2. **Beta Launch** (Week 5)
   - Deploy to Azure
   - Onboard 10 beta users
   - Collect feedback
   
3. **Monetization** (Week 6-8)
   - Integrate Stripe
   - Implement usage metering
   - Add admin dashboard
   - Launch paid plans

4. **Phase 2 Features** (Weeks 9+)
   - Gmail/Outlook import
   - AI-powered search and summaries
   - Shared workspaces
   - GDPR compliance tier

---

**Last Updated**: 2025-11-11  
**Document Owner**: CTO  
**Review Frequency**: Monthly or after major architectural changes

