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

#### User Web App (Blazor WebAssembly)
- **Purpose**: User-facing SPA for mailbox management and search
- **Technology**: Blazor WebAssembly with MudBlazor UI components
- **Key Features**:
  - User registration and authentication
  - .mbox file upload (up to 5GB)
  - Email search interface with full-text and AI-powered search
  - Email viewer with HTML rendering and attachment download
  - Account settings and billing portal (Stripe)
- **Deployment**: Azure Static Web Apps or served from WebApp API

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
    - Full-text search using SQL Server FTS
    - Advanced filtering (date range, sender, recipient)
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
     - Metadata: MessageId, From, To, Cc, Bcc, Date, Subject
     - Content: Text body, HTML body, snippet (200 chars)
     - Attachments: Save to Blob Storage, store reference in DB
  5. **Store in Database**
     - Bulk insert in batches for performance
     - Update mailbox processing status
  6. **Error Handling**
     - Retry failed jobs (exponential backoff)
     - Mark mailbox as "Failed" after 3 attempts
     - Notify user via email

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
- **Queue**: `mailbox-ingestion`
- **Message Format**:
```json
{
  "tenantId": "tenant-123",
  "userId": "user-456",
  "mailboxId": "guid",
  "blobPath": "mbox-archives/tenant-123/mailbox-guid/original.mbox",
  "fileSizeBytes": 10485760,
  "enqueuedAt": "2025-11-11T10:30:00Z"
}
```
- **Processing**: Ingestion Worker with visibility timeout of 5 minutes
- **Retry Policy**: Max 3 attempts with exponential backoff

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
**Strategy**: Shared database with tenant isolation

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
| **Runtime** | .NET | 8.0+ | Core platform |
| **Orchestration** | Azure Aspire | 9.0+ | Service orchestration |
| **Frontend** | Blazor WebAssembly | .NET 8 | User SPA |
| **Admin** | Blazor Server | .NET 8 | Real-time dashboard |
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

| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-11-11 | Use Azure SQL Serverless over PostgreSQL | Lower cost for side-hustle, auto-pause, excellent FTS built-in |
| 2025-11-11 | Use Blazor WASM over React | C# full-stack, faster dev for solo/small team, less context switching |
| 2025-11-11 | Use Storage Queue over Service Bus | Simpler, cheaper for MVP; migrate to Service Bus if need advanced routing |
| 2025-11-11 | Use MimeKit over custom parser | Battle-tested, handles corrupt mbox gracefully, actively maintained |
| 2025-11-11 | Use Stripe over Paddle | Better .NET SDK, more flexible for metered billing, standard for SaaS |
| 2025-11-11 | Phase AI features to v2 | Get to paying customers faster, validate core value prop first |

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

