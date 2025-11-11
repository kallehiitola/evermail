# Evermail MVP - Complete Todo List

> **MVP Scope**: Full-Featured (6-8 weeks)  
> **Target**: Beta launch with 10 users  
> **Includes**: Full auth with 2FA, all tiers, admin dashboard, multiple mailbox support

---

## 📅 Timeline Overview

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| **Phase 0**: Foundation | Week 1 (5 days) | Aspire solution, database, authentication |
| **Phase 1**: Core Backend | Week 2 (5 days) | Email parsing, ingestion worker, storage |
| **Phase 2**: Search & API | Week 3 (5 days) | Full-text search, REST APIs, endpoints |
| **Phase 3**: User Frontend | Week 4 (5 days) | Blazor Web App, email viewer, search UI |
| **Phase 4**: Payments | Week 5 (5 days) | Stripe integration, all tiers, webhooks |
| **Phase 5**: Admin Dashboard | Week 6 (5 days) | Admin UI, monitoring, user management |
| **Phase 6**: Polish & Deploy | Week 7-8 (10 days) | Testing, bug fixes, production deployment |

**Total**: 6-8 weeks to beta launch

---

## Phase 0: Foundation & Setup (Week 1)

### Day 1: Aspire Solution Structure

- [ ] **Create .NET 9 Aspire solution**
  ```bash
  dotnet new aspire -n Evermail --framework net9.0
  ```
  
- [ ] **Add all projects to solution**:
  - [ ] Evermail.AppHost (Aspire orchestrator)
  - [ ] Evermail.WebApp (Blazor Web App)
  - [ ] Evermail.AdminApp (Blazor Server admin)
  - [ ] Evermail.IngestionWorker (Background service)
  - [ ] Evermail.Domain (Domain entities)
  - [ ] Evermail.Infrastructure (EF Core, Blob, Queue)
  - [ ] Evermail.Common (DTOs, utilities)

- [ ] **Configure Aspire AppHost**:
  - [ ] Add SQL Server component
  - [ ] Add Azure Storage component (blobs + queues)
  - [ ] Add service references
  - [ ] Configure local development (Azurite, SQL container)

- [ ] **Update Documentation/Architecture.md** with actual project structure

### Day 2-3: Database & Entity Framework

- [ ] **Install EF Core packages**:
  - [ ] Microsoft.EntityFrameworkCore.SqlServer (9.0)
  - [ ] Microsoft.EntityFrameworkCore.Tools (9.0)
  - [ ] Microsoft.EntityFrameworkCore.Design (9.0)

- [ ] **Create domain entities** (following DatabaseSchema.md):
  - [ ] Tenant entity (TenantId, Name, SubscriptionTier, etc.)
  - [ ] User entity (Id, TenantId, Email, PasswordHash, 2FA fields)
  - [ ] Mailbox entity (Id, TenantId, UserId, FileName, Status, BlobPath)
  - [ ] EmailMessage entity (Id, TenantId, UserId, MailboxId, Subject, From, To, Date, Bodies)
  - [ ] Attachment entity (Id, TenantId, EmailMessageId, FileName, BlobPath)
  - [ ] SubscriptionPlan entity (Id, Name, PriceMonthly, MaxStorageGB, MaxUsers)
  - [ ] Subscription entity (Id, TenantId, PlanId, StripeSubscriptionId, Status)
  - [ ] AuditLog entity (Id, TenantId, UserId, Action, ResourceType, Timestamp)

- [ ] **Create EmailDbContext**:
  - [ ] Add DbSets for all entities
  - [ ] Configure relationships in OnModelCreating
  - [ ] Add global query filters for multi-tenancy
  - [ ] Add indexes (TenantId, UserId, Date, FromAddress, etc.)

- [ ] **Create initial migration**:
  ```bash
  dotnet ef migrations add InitialCreate -p Evermail.Infrastructure -s Evermail.WebApp
  ```

- [ ] **Seed subscription plans**:
  - [ ] Free tier (€0, 1GB, 1 user, 30 days retention)
  - [ ] Pro tier (€9, 5GB, 1 user, unlimited mailboxes, 1 year retention)
  - [ ] Team tier (€29, 50GB, 5 users, shared workspaces, 2 years retention)
  - [ ] Enterprise tier (€99, 500GB, 50 users, GDPR archive)

### Day 4-5: Authentication & Authorization

- [ ] **Configure ASP.NET Core Identity**:
  - [ ] Install Microsoft.AspNetCore.Identity.EntityFrameworkCore (9.0)
  - [ ] Create ApplicationUser extending IdentityUser
  - [ ] Add TenantId to ApplicationUser
  - [ ] Configure password requirements (12+ chars, complexity)
  - [ ] Configure lockout policy (5 attempts, 15-minute lockout)

- [ ] **Implement JWT authentication**:
  - [ ] Install Microsoft.AspNetCore.Authentication.JwtBearer (9.0)
  - [ ] Configure JWT with ES256 algorithm (ECDSA)
  - [ ] Add claims: sub, tenant_id, email, role
  - [ ] Set 15-minute access token expiry
  - [ ] Implement refresh tokens (30-day expiry)

- [ ] **Implement 2FA (Two-Factor Authentication)**:
  - [ ] TOTP support (Google Authenticator, Authy)
  - [ ] Generate QR codes for setup
  - [ ] Verify 2FA codes
  - [ ] Generate backup codes (10 single-use)
  - [ ] Store encrypted 2FA secrets

- [ ] **Add OAuth providers**:
  - [ ] **Google OAuth** integration
    - [ ] Install Microsoft.AspNetCore.Authentication.Google
    - [ ] Configure Google OAuth client (get credentials from Google Cloud Console)
    - [ ] Handle Google callback
    - [ ] Map Google profile to ApplicationUser
  - [ ] **Microsoft OAuth** integration
    - [ ] Install Microsoft.Identity.Web (9.0)
    - [ ] Configure Microsoft/Entra ID OAuth
    - [ ] Handle Microsoft callback
    - [ ] Map Microsoft profile to ApplicationUser

- [ ] **Implement tenant context resolver**:
  - [ ] Create TenantContext class (TenantId, UserId)
  - [ ] Register as scoped service
  - [ ] Resolve from JWT claims

- [ ] **Create auth endpoints**:
  - [ ] POST /api/v1/auth/register
  - [ ] POST /api/v1/auth/login
  - [ ] POST /api/v1/auth/refresh
  - [ ] POST /api/v1/auth/enable-2fa
  - [ ] POST /api/v1/auth/verify-2fa
  - [ ] GET /api/v1/auth/google (OAuth redirect)
  - [ ] GET /api/v1/auth/microsoft (OAuth redirect)

---

## Phase 1: Core Backend (Week 2)

### Day 6-7: Email Parsing with MimeKit

- [ ] **Install MimeKit** (4.0+)

- [ ] **Create email parser service**:
  - [ ] IEmailParserService interface
  - [ ] EmailParserService implementation
  - [ ] Method: ParseMboxFileAsync(Stream, MailboxId, TenantId, UserId)
  - [ ] Batch processing (500 messages per batch)
  - [ ] Error handling (skip corrupt messages, log, continue)

- [ ] **Implement message mapping**:
  - [ ] Extract: MessageId, Subject, From, To, Cc, Bcc, Date
  - [ ] Extract: TextBody, HtmlBody
  - [ ] Generate snippet (first 200 chars)
  - [ ] Handle missing/corrupt headers gracefully
  - [ ] Extract References, InReplyTo (for threading in Phase 2)

- [ ] **Implement attachment processing**:
  - [ ] Extract attachments from MimeMessage
  - [ ] Save to Blob Storage: `attachments/{tenantId}/{mailboxId}/{messageId}/{filename}`
  - [ ] Create Attachment entity records
  - [ ] Handle inline vs regular attachments
  - [ ] Skip oversized attachments (>100MB) with warning

### Day 8-9: Blob Storage & Queue Integration

- [ ] **Configure Azure Blob Storage**:
  - [ ] Install Azure.Storage.Blobs (12.x)
  - [ ] Create BlobStorageService
  - [ ] Method: UploadMboxAsync(Stream, TenantId, MailboxId)
  - [ ] Path structure: `mbox-archives/{tenantId}/{mailboxId}/original.mbox`
  - [ ] Generate SAS tokens (15-minute expiry for downloads)
  - [ ] Implement tenant ownership verification before SAS generation

- [ ] **Configure Azure Storage Queues**:
  - [ ] Install Azure.Storage.Queues (12.x)
  - [ ] Create QueueService
  - [ ] Queue name: `mailbox-ingestion`
  - [ ] Method: EnqueueMailboxJobAsync(MailboxIngestionJob)
  - [ ] Message format: JSON with TenantId, UserId, MailboxId, BlobPath

- [ ] **Create multiple mailbox support** ⭐ NEW FEATURE:
  - [ ] Update Mailbox entity with DisplayName, Description fields
  - [ ] UI: Show list of all user's mailboxes
  - [ ] UI: Allow uploading multiple mailboxes
  - [ ] UI: Rename/organize mailboxes
  - [ ] UI: Search across all mailboxes or filter by specific mailbox
  - [ ] Enforce tier limits (Free: 1 mailbox, Pro: unlimited)

### Day 10: Ingestion Worker

- [ ] **Create IngestionWorker project**:
  - [ ] BackgroundService that polls queue
  - [ ] Receive messages (10 at a time, 5-minute visibility timeout)
  - [ ] Process each message: download blob → parse → save to DB
  - [ ] Delete message after successful processing
  - [ ] Handle failures (retry 3 times, then mark as failed)

- [ ] **Implement mailbox status tracking**:
  - [ ] Update Mailbox.Status: Pending → Processing → Completed/Failed
  - [ ] Track ProcessingStartedAt, ProcessingCompletedAt
  - [ ] Track TotalEmails, ProcessedEmails, FailedEmails
  - [ ] Store error messages on failure

- [ ] **Implement progress reporting** (for UI):
  - [ ] Periodically update Mailbox.ProcessedEmails count
  - [ ] Calculate percentage complete
  - [ ] Estimate time remaining (optional)

---

## Phase 2: Search & API (Week 3)

### Day 11-12: Full-Text Search

- [ ] **Configure SQL Server Full-Text Search**:
  - [ ] Create migration to add full-text catalog
  - [ ] Add full-text index on EmailMessages (Subject, TextBody, FromName, FromAddress)
  - [ ] Test with CONTAINS and FREETEXT queries

- [ ] **Create search service**:
  - [ ] IEmailSearchService interface
  - [ ] EmailSearchService implementation
  - [ ] Method: SearchAsync(query, filters, page, pageSize)
  - [ ] Support filters: date range, sender, has attachments, mailbox
  - [ ] Implement pagination (max 100 results per page)
  - [ ] Use AsNoTracking() for performance

- [ ] **Create compiled queries** (performance optimization):
  - [ ] GetEmailByIdQuery
  - [ ] SearchEmailsQuery
  - [ ] GetMailboxesQuery

### Day 13-14: REST API Endpoints

- [ ] **Create mailbox endpoints** (`/api/v1/mailboxes`):
  - [ ] GET / - List user's mailboxes (with filters: status)
  - [ ] POST / - Upload new mbox file (multipart/form-data)
  - [ ] GET /{id} - Get mailbox details
  - [ ] DELETE /{id} - Delete mailbox (and all emails/attachments)
  - [ ] PATCH /{id} - Update mailbox (rename, description) ⭐ NEW

- [ ] **Create email endpoints** (`/api/v1/emails`):
  - [ ] GET /search - Search emails (full-text query + filters)
  - [ ] GET /{id} - Get email details
  - [ ] PATCH /{id}/read - Mark as read/unread

- [ ] **Create attachment endpoints** (`/api/v1/attachments`):
  - [ ] GET /{id}/download - Download attachment (verify tenant, generate SAS token)

- [ ] **Create user endpoints** (`/api/v1/users`):
  - [ ] GET /me - Get current user profile
  - [ ] PATCH /me - Update profile (name)
  - [ ] POST /me/enable-2fa - Enable 2FA
  - [ ] POST /me/verify-2fa - Verify 2FA setup
  - [ ] POST /me/export - Request GDPR data export
  - [ ] DELETE /me - Delete account (GDPR)

- [ ] **Add global error handling middleware**
- [ ] **Add rate limiting** (100/hour Free, 1000/hour Pro)
- [ ] **Configure CORS** for frontend
- [ ] **Add OpenAPI/Swagger** documentation
- [ ] **Add health check endpoints** (/health, /health/ready)

### Day 15: API Testing

- [ ] **Test all endpoints with Postman/REST Client**:
  - [ ] Auth flow (register, login, refresh token)
  - [ ] 2FA flow
  - [ ] OAuth flows (Google, Microsoft)
  - [ ] Mailbox upload and processing
  - [ ] Email search with various filters
  - [ ] Multiple mailbox uploads per user ⭐
  - [ ] Attachment download
  - [ ] Rate limiting

---

## Phase 3: User Frontend (Week 4)

### Day 16-17: Blazor Web App Setup

- [ ] **Install MudBlazor** (7.0+)

- [ ] **Create Blazor Web App structure**:
  - [ ] Configure render modes (SSR, Interactive Server, Interactive WASM)
  - [ ] Set up routing
  - [ ] Create layout components (MainLayout, NavMenu)
  - [ ] Configure MudBlazor theme

- [ ] **Create authentication pages**:
  - [ ] Login page (email/password + 2FA input)
  - [ ] Register page (email, password, name, accept terms)
  - [ ] 2FA setup page (QR code, verify code)
  - [ ] OAuth callback handlers (Google, Microsoft)
  - [ ] Forgot password page (future)

- [ ] **Create HTTP client configuration**:
  - [ ] Configure HttpClient with base URL
  - [ ] AuthenticationHandler (add JWT to requests)
  - [ ] Token refresh logic
  - [ ] Error handling

### Day 18-19: Email UI Components

- [ ] **Create mailbox management pages**:
  - [ ] Mailboxes list page (show all user's mailboxes) ⭐
    - [ ] Display: name, size, status, email count, upload date
    - [ ] Actions: upload new, rename, delete
    - [ ] Filter by status (Pending, Processing, Completed, Failed)
  - [ ] Upload mailbox dialog (drag-and-drop + file picker)
    - [ ] Support for multiple file uploads ⭐
    - [ ] Show tier limits (Free: 1, Pro: unlimited)
    - [ ] Add mailbox name and description fields ⭐
    - [ ] Progress indicator
  - [ ] Mailbox detail page (processing status, statistics)
  - [ ] Rename mailbox dialog ⭐ NEW

- [ ] **Create email search page**:
  - [ ] Search box (with autocomplete/suggestions)
  - [ ] Advanced filters panel:
    - [ ] Date range picker
    - [ ] Sender filter
    - [ ] Has attachments checkbox
    - [ ] **Select specific mailbox dropdown** ⭐ NEW (search within one or all)
  - [ ] Sort options (date, sender, subject)
  - [ ] Results list with pagination
  - [ ] Loading states

- [ ] **Create email list components**:
  - [ ] EmailListItem component (subject, from, date, snippet, attachment icon)
  - [ ] Virtualization for large lists (MudVirtualize)
  - [ ] Select/multi-select (for future bulk actions)

- [ ] **Create email viewer page**:
  - [ ] Email header (subject, from, to, cc, date)
  - [ ] HTML body rendering (with sanitization)
  - [ ] Plain text fallback
  - [ ] Attachment list with download links
  - [ ] **Show which mailbox this email is from** ⭐ NEW
  - [ ] Navigation (previous/next email)

### Day 20: User Dashboard

- [ ] **Create user dashboard/home page**:
  - [ ] Welcome message
  - [ ] **List of all mailboxes with quick stats** ⭐
  - [ ] Storage usage indicator (with tier limit)
  - [ ] Recent activity
  - [ ] Quick search box
  - [ ] Upload mailbox button (prominent)

- [ ] **Create account settings page**:
  - [ ] Profile info (name, email)
  - [ ] 2FA management (enable/disable, view backup codes)
  - [ ] Connected accounts (Google, Microsoft - show/disconnect)
  - [ ] Subscription tier info
  - [ ] Storage usage breakdown by mailbox ⭐ NEW
  - [ ] Data export request (GDPR)
  - [ ] Delete account (with confirmation)

---

## Phase 4: Stripe Payment Integration (Week 5)

### Day 21-22: Stripe Setup

- [ ] **Install Stripe.net** (latest)

- [ ] **Configure Stripe in WebApp**:
  - [ ] Add Stripe secret key to user secrets / Key Vault
  - [ ] Add Stripe webhook secret
  - [ ] Register StripeConfiguration

- [ ] **Create Stripe products in Stripe Dashboard** (test mode):
  - [ ] Product: "Evermail Free" (€0)
  - [ ] Product: "Evermail Pro" (€9/month, €90/year)
  - [ ] Product: "Evermail Team" (€29/month, €290/year)
  - [ ] Product: "Evermail Enterprise" (€99/month, €990/year)
  - [ ] Save price IDs to configuration

- [ ] **Implement Stripe service**:
  - [ ] IStripeService interface
  - [ ] StripeService implementation
  - [ ] Method: CreateCustomerAsync(User, Tenant)
  - [ ] Method: CreateCheckoutSessionAsync(PlanId, TenantId, SuccessUrl, CancelUrl)
  - [ ] Method: CreateCustomerPortalSessionAsync(StripeCustomerId)
  - [ ] Method: CancelSubscriptionAsync(SubscriptionId)

### Day 23: Subscription Flow

- [ ] **Create billing endpoints** (`/api/v1/billing`):
  - [ ] GET /plans - List available subscription plans
  - [ ] POST /checkout - Create Stripe checkout session
  - [ ] POST /portal - Get customer portal URL
  - [ ] GET /subscription - Get current subscription details

- [ ] **Create billing pages**:
  - [ ] Pricing page (show all tiers with features)
    - [ ] Highlight **unlimited mailboxes** for Pro tier ⭐
    - [ ] Show pricing for multiple users (Team/Enterprise)
  - [ ] Checkout redirect (to Stripe hosted page)
  - [ ] Success page (after payment)
  - [ ] Cancel page (if user cancels)

- [ ] **Implement subscription enforcement**:
  - [ ] Check storage limits before upload
  - [ ] Check mailbox count limits (Free: 1, Pro: unlimited) ⭐
  - [ ] Check user limits (Free/Pro: 1, Team: 5, Enterprise: 50)
  - [ ] Show upgrade prompts when limits reached

### Day 24-25: Stripe Webhooks

- [ ] **Create webhook endpoint** (`/api/webhooks/stripe`):
  - [ ] Verify webhook signature (critical for security!)
  - [ ] Handle events:
    - [ ] `checkout.session.completed` - Activate subscription
    - [ ] `invoice.payment_succeeded` - Extend subscription
    - [ ] `invoice.payment_failed` - Send notification, grace period
    - [ ] `customer.subscription.created` - Update tenant tier
    - [ ] `customer.subscription.updated` - Update tenant tier
    - [ ] `customer.subscription.deleted` - Downgrade to free tier

- [ ] **Test webhook handling**:
  - [ ] Use Stripe CLI to forward webhooks locally
  - [ ] Test each event type
  - [ ] Verify subscription status updates correctly
  - [ ] Test failure scenarios

- [ ] **Implement subscription upgrade/downgrade logic**:
  - [ ] Upgrade: Immediate access to new features
  - [ ] Downgrade: Graceful handling (keep data, reduce limits)
  - [ ] Cancel: Mark as cancelled, keep data until period end

---

## Phase 5: Admin Dashboard (Week 6)

### Day 26-27: Admin Application Setup

- [ ] **Create Evermail.AdminApp** (Blazor Server):
  - [ ] Configure for real-time updates (SignalR)
  - [ ] Install MudBlazor
  - [ ] Configure authentication (same JWT as WebApp)
  - [ ] Restrict to Admin/SuperAdmin roles only

- [ ] **Create admin navigation**:
  - [ ] Dashboard (overview)
  - [ ] Tenants (list, details)
  - [ ] Users (list, search, manage)
  - [ ] Mailboxes (list, processing status)
  - [ ] Jobs (queue monitor)
  - [ ] Analytics (usage, costs)
  - [ ] Billing (Stripe sync)

### Day 28: Admin - Tenant & User Management

- [ ] **Create admin API endpoints** (`/api/v1/admin`):
  - [ ] GET /tenants - List all tenants (SuperAdmin only)
  - [ ] GET /tenants/{id} - Get tenant details
  - [ ] PATCH /tenants/{id} - Update tenant (suspend, limits)
  - [ ] GET /users - List all users (with filters)
  - [ ] GET /users/{id} - Get user details
  - [ ] PATCH /users/{id} - Update user (roles, status)

- [ ] **Create admin pages**:
  - [ ] Tenants list page (table with filters)
  - [ ] Tenant detail page (users, storage, mailboxes, billing)
  - [ ] Users list page (searchable, filterable)
  - [ ] User detail page (mailboxes, activity, audit logs)

### Day 29: Admin - Job Monitoring

- [ ] **Create job monitoring endpoints**:
  - [ ] GET /admin/jobs - List processing jobs
  - [ ] GET /admin/jobs/queue-depth - Current queue depth
  - [ ] POST /admin/jobs/{id}/retry - Retry failed job

- [ ] **Create job monitoring page**:
  - [ ] Real-time queue depth indicator
  - [ ] List of processing jobs (with progress %)
  - [ ] Failed jobs list (with errors)
  - [ ] Retry button for failed jobs
  - [ ] Performance metrics (avg processing time per GB)

### Day 30: Admin - Analytics & Billing

- [ ] **Create analytics endpoints**:
  - [ ] GET /admin/analytics/overview - Key metrics
  - [ ] GET /admin/analytics/storage - Storage usage by tenant
  - [ ] GET /admin/analytics/revenue - Revenue metrics
  - [ ] GET /admin/analytics/users - User growth

- [ ] **Create analytics dashboards**:
  - [ ] Overview dashboard (KPIs, charts)
    - [ ] Total users, paying users, MRR
    - [ ] Storage used, mailboxes processed
    - [ ] Average mailboxes per user ⭐
  - [ ] Storage analytics (by tenant, by tier)
  - [ ] Revenue dashboard (Stripe sync)
  - [ ] User growth chart

- [ ] **Billing management page**:
  - [ ] Sync with Stripe (list customers, subscriptions)
  - [ ] Failed payments list
  - [ ] Revenue reports
  - [ ] Churn tracking

---

## Phase 6: Testing & Polish (Week 7)

### Day 31-32: Core Testing

- [ ] **Set up testing projects**:
  - [ ] Evermail.UnitTests (xUnit)
  - [ ] Evermail.IntegrationTests (WebApplicationFactory)

- [ ] **Write key unit tests**:
  - [ ] EmailParserService tests (various mbox formats)
  - [ ] TenantContext resolver tests
  - [ ] Subscription enforcement tests
  - [ ] Multi-mailbox support tests ⭐

- [ ] **Write integration tests**:
  - [ ] Auth flow tests (register, login, 2FA)
  - [ ] Mailbox upload and processing test
  - [ ] Email search test
  - [ ] Stripe webhook tests (mock webhooks)

- [ ] **Test OAuth providers**:
  - [ ] Google OAuth flow
  - [ ] Microsoft OAuth flow
  - [ ] Profile mapping

### Day 33-34: Bug Fixes & Polish

- [ ] **Test complete user flows**:
  - [ ] New user signup → upload mailbox → wait for processing → search emails
  - [ ] Upload multiple mailboxes → search across all ⭐
  - [ ] Free user hits limit → upgrade to Pro → unlimited mailboxes ⭐
  - [ ] Upgrade flow (Free → Pro)
  - [ ] Billing portal access
  - [ ] 2FA setup and login

- [ ] **Fix bugs found during testing**
- [ ] **Performance optimization**:
  - [ ] Add database indexes if queries are slow
  - [ ] Optimize large mailbox processing
  - [ ] Test with real 1GB+ mbox files

- [ ] **UI polish**:
  - [ ] Loading states everywhere
  - [ ] Error messages user-friendly
  - [ ] Mobile responsive (test on phone)
  - [ ] Empty states (no mailboxes, no search results)

### Day 35: Documentation Updates

- [ ] **Update Documentation/API.md** with all implemented endpoints
- [ ] **Update Documentation/DatabaseSchema.md** if schema changed
- [ ] **Update README.md** with actual setup instructions
- [ ] **Create user guide** (how to export mbox from Gmail, Outlook, Apple Mail)
- [ ] **Update Pricing.md** with multiple mailbox emphasis ⭐

---

## Phase 7: Production Deployment (Week 8)

### Day 36-37: Azure Infrastructure

- [ ] **Provision Azure resources with azd**:
  ```bash
  cd Evermail.AppHost
  azd init
  azd up
  ```
  - [ ] Environment: `evermail-prod`
  - [ ] Location: `westeurope`
  - [ ] Verify all resources created:
    - [ ] Resource group
    - [ ] SQL Server + Database (Serverless tier)
    - [ ] Storage account (with containers and queue)
    - [ ] Container Apps Environment
    - [ ] Container Apps (webapp, worker, admin)
    - [ ] Application Insights
    - [ ] Key Vault

- [ ] **Configure production secrets in Key Vault**:
  - [ ] Database connection string
  - [ ] Stripe live secret key (switch from test)
  - [ ] Stripe live webhook secret
  - [ ] JWT signing key
  - [ ] Google OAuth credentials (production)
  - [ ] Microsoft OAuth credentials (production)

- [ ] **Run database migrations in production**:
  ```bash
  dotnet ef database update --connection "<prod-connection-string>"
  ```

### Day 38: DNS & SSL

- [ ] **Configure custom domain** (if ready):
  - [ ] Register domain: evermail.com
  - [ ] Configure DNS:
    - [ ] app.evermail.com → WebApp
    - [ ] admin.evermail.com → AdminApp
    - [ ] api.evermail.com → WebApp API
  - [ ] Configure SSL certificates (automatic with Azure)

- [ ] **Update OAuth redirect URLs**:
  - [ ] Google OAuth: Add production URLs
  - [ ] Microsoft OAuth: Add production URLs

### Day 39-40: Monitoring & Launch Prep

- [ ] **Configure Application Insights**:
  - [ ] Custom events for key actions
  - [ ] Performance metrics
  - [ ] Exception tracking
  - [ ] Alerts:
    - [ ] High error rate (>5% in 5 minutes)
    - [ ] Queue depth >1000
    - [ ] Processing failures >10/hour

- [ ] **Set up Azure Monitor alerts**:
  - [ ] Database DTU >80%
  - [ ] Container app crashes
  - [ ] Blob storage operations throttled

- [ ] **Create runbook for common issues**:
  - [ ] Worker not processing
  - [ ] Database connection failures
  - [ ] Stripe webhook failures

- [ ] **Beta launch checklist**:
  - [ ] All tests passing ✅
  - [ ] Security scan completed ✅
  - [ ] Stripe in live mode ✅
  - [ ] Monitoring configured ✅
  - [ ] Backup strategy in place ✅
  - [ ] Documentation updated ✅

- [ ] **Invite 10 beta users**

---

## 🎯 Key Features Summary

### Multiple Mailbox Support ⭐ (Throughout MVP)

This feature is integrated across the entire system:

**Backend**:
- [x] Mailbox entity supports multiple per user
- [ ] Mailbox has DisplayName, Description fields
- [ ] Search can filter by specific mailbox
- [ ] Storage limits enforced (Free: 1 mailbox, Pro: unlimited)

**Frontend**:
- [ ] Mailboxes list page (shows all user's mailboxes)
- [ ] Upload supports multiple files
- [ ] Can rename/organize mailboxes
- [ ] Search across all or filter by mailbox
- [ ] Dashboard shows all mailboxes

**Pricing**:
- [ ] Free tier: 1 mailbox only
- [ ] Pro tier: Unlimited mailboxes (prominent in marketing)
- [ ] Team tier: Unlimited mailboxes + multiple users
- [ ] Enterprise: Unlimited mailboxes + bulk import tools

**Use Cases Enabled**:
1. ✅ Individual with multiple job history archives (Gmail from 3 companies)
2. ✅ HR importing multiple employee mailboxes
3. ✅ Small business with multiple department mailboxes (support@, sales@, info@)
4. ✅ Legal discovery with multiple custodian mailboxes

---

## 📊 Milestone Checklist

### MVP Complete When:

**Backend**:
- [ ] ✅ All entities created and migrated
- [ ] ✅ Authentication working (basic + OAuth + 2FA)
- [ ] ✅ Mbox parsing working (MimeKit, streaming, batches)
- [ ] ✅ Ingestion worker processing successfully
- [ ] ✅ Full-text search working
- [ ] ✅ All API endpoints implemented and tested
- [ ] ✅ Multiple mailbox support working ⭐

**Frontend**:
- [ ] ✅ Can register/login (email + OAuth)
- [ ] ✅ Can upload multiple mbox files ⭐
- [ ] ✅ Can view all mailboxes ⭐
- [ ] ✅ Can search across all mailboxes ⭐
- [ ] ✅ Can view emails and attachments
- [ ] ✅ Can manage subscription (upgrade, billing portal)

**Payments**:
- [ ] ✅ Stripe Free + Pro + Team + Enterprise tiers configured
- [ ] ✅ Checkout flow working
- [ ] ✅ Webhooks handling subscription events
- [ ] ✅ Tier limits enforced (mailbox count, storage, users) ⭐

**Admin**:
- [ ] ✅ Can view all tenants and users
- [ ] ✅ Can monitor mailbox processing
- [ ] ✅ Can view analytics (users, revenue, storage)
- [ ] ✅ Can see mailbox statistics per tenant ⭐

**Deployment**:
- [ ] ✅ Deployed to Azure (Evermail subscription)
- [ ] ✅ Monitoring configured
- [ ] ✅ SSL certificates working
- [ ] ✅ 10 beta users invited

---

## 🚀 Post-MVP Priorities (Phase 2)

### Quick Wins (Month 3-4)

- [ ] **Gmail/Outlook OAuth Import**:
  - [ ] Gmail API integration (read emails via API, convert to mbox format)
  - [ ] Microsoft Graph API integration
  - [ ] Incremental sync (don't re-import)

- [ ] **Bulk import UI** (for Team/Enterprise):
  - [ ] Upload multiple mbox files at once (zip support)
  - [ ] Import from Google Takeout (extract mbox from zip)
  - [ ] Batch processing UI

- [ ] **Email threading**:
  - [ ] Group emails by conversation (using InReplyTo, References)
  - [ ] Conversation view

### AI Features (Month 4-6)

- [ ] **Azure OpenAI integration**:
  - [ ] GPT-4o for summaries
  - [ ] Embeddings for semantic search

- [ ] **AI-powered features**:
  - [ ] Mailbox summary ("Summarize all emails in this mailbox") ⭐
  - [ ] Email summaries
  - [ ] Semantic search (natural language queries)
  - [ ] Entity extraction (companies, amounts, dates)

### Mobile App (Month 6-12)

- [ ] **Create .NET MAUI Blazor Hybrid app**:
  - [ ] Shared Razor Component Library
  - [ ] iOS app
  - [ ] Android app
  - [ ] Reuse 80-90% of web UI code

---

## 📋 Daily Standup Questions

Track progress with these questions:

1. **What did I complete yesterday?**
2. **What am I working on today?**
3. **Any blockers?**
4. **Is documentation updated?**
5. **Are tests passing?**

---

## ⚠️ Critical Reminders

### Always Do:

- ✅ **Check Documentation/** folder before implementing
- ✅ **Update docs** when design changes
- ✅ **Test multi-tenancy** (can't access other tenant's data)
- ✅ **Add TenantId** to every new entity
- ✅ **Enforce mailbox limits** per tier ⭐
- ✅ **Use Microsoft Learn MCP** for official patterns
- ✅ **Commit frequently** with semantic messages

### Never Do:

- ❌ Skip multi-tenancy (TenantId) on entities
- ❌ Create duplicate documentation
- ❌ Bypass tenant isolation
- ❌ Commit secrets to git
- ❌ Deploy without testing subscription limits ⭐

---

## 🎯 Success Metrics

### Week 4 (After Phase 3)
- ✅ Can upload and search emails
- ✅ Basic auth working
- ⚙️ No payments yet (acceptable for internal testing)

### Week 6 (After Phase 5)
- ✅ Full MVP feature complete
- ✅ Payments working
- ✅ Admin dashboard functional
- ✅ Multiple mailbox support tested ⭐

### Week 8 (After Phase 7)
- ✅ Deployed to production (Evermail subscription)
- ✅ 10 beta users onboarded
- ✅ 2-3 paying users (break-even path)
- ✅ All critical flows tested

---

## 📞 Need Help?

When working on any task, ask Cursor AI:

```
"Implement [feature] following the Documentation/[relevant-doc].md and 
multi-tenancy rules. search Microsoft Learn for [technology] best practices"
```

Examples:
```
"Create the Tenant entity following DatabaseSchema.md and multi-tenancy rules"

"Implement mbox parsing with MimeKit following email-processing.mdc rules. 
use library /jstedfast/mimekit"

"Create the email search API endpoint following API.md. 
search Microsoft Learn for SQL Server full-text search"

"Build the mailbox upload UI with MudBlazor file upload. 
use context7 for MudBlazor examples"
```

---

**Last Updated**: 2025-11-11  
**Total Tasks**: ~150 actionable items  
**Timeline**: 6-8 weeks to fully-featured MVP  
**Status**: Ready to begin Phase 0

