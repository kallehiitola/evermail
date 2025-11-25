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
- **Navigation Layout** (Nov 2025 refresh): The left rail is a three-zone column. The top zone shows the signed-in user's identity card (initial badge + display name + truncated email) and exposes a popover with `Profile & settings`, `Manage subscription`, and `Sign out`. Truncate long emails with `text-overflow: ellipsis` so addresses never blow up the layout. The middle zone contains the primary navigation stack with `Home`, `Mailboxes`, `Upload`, `Emails`, and an accordion-labeled `Dev` bucket for diagnostics links (Auth Status, Admin Roles, Key Vault probe, etc.). The bottom zone pins the `Settings` link directly above the Evermail wordmark, and the logo/wordmark sit centered horizontally inside the sidebar footer. When the visitor is not authenticated the sidebar stays hidden entirely (no gradient gutter) so the home/marketing experiences handle login/register CTAs without duplication; the top-row renders a lightweight Home/Login/Register nav pill group to keep wayfinding obvious.

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

##### Guided Onboarding Wizard (Dec 2025 rollout)
- **Entry point**: After account creation/OAuth we redirect to `/onboarding` instead of `/admin/encryption`. The wizard is also linked from the dashboard banner (`/onboarding?step=security`) so admins can jump straight to unfinished work.
- **Steps & data flow**:
  1. **Choose plan** – `GET /api/v1/tenants/plans` returns every active `SubscriptionPlan` (pricing, limits, features). Selecting one calls `PUT /api/v1/tenants/subscription`, which updates `Tenant.SubscriptionTier`, max limits, and stamps `Tenant.OnboardingPlanConfirmedAt`. Staying on the Free plan simply re-posts the same tier so we still record confirmation.
  2. **Secure tenant** – Surfaces the encryption status from `GET /api/v1/tenants/onboarding/status` plus shortcuts to `/admin/encryption?onboarding=1` and the offline BYOK lab. A “Refresh status” button re-queries the endpoint once the admin finishes SKR/AWS wiring.
  3. **Upload mailbox** – Links to `/upload`, reminds the user of their plan limits, and offers sample data instructions so they can exercise ingestion immediately.
- **Progress tracking**: `TenantOnboardingStatusDto` now includes `PlanConfirmed` and `SubscriptionTier`. The wizard (and dashboard banner) calculates completion purely from the backend (`PlanConfirmed`, `EncryptionConfigured`, `HasMailbox`) so refreshing the page always reflects reality.
- **Design language**: Uses the same gradient hero, pill groups, and `.modern-card` stack as Home/Upload. The left rail is a progress list (step number + icon) while the right pane renders rich content for the selected step (plan cards, security alert, upload CTA). Everything is dark-mode aware thanks to existing tokens.

###### Onboarding UX Narrative (Jan 2026 refresh)

1. **OAuth welcome**  
   - Immediately after Google or Microsoft sign-in we land users on `/onboarding` with a hero block: “Welcome back, `<email>`” plus a provider badge (Google/Microsoft).  
   - Copy clarifies that their identity is already secured and the next steps are about configuring the workspace.  
   - CTA: “Start setup” selects the first item in the progress rail.

2. **Plan selection**  
   - Modern cards list each plan (Free/Pro/Team/etc.) with upbeat copy (“Launch fast”, “Scale with AI search”).  
   - Each card shows price, limits, and bullets. Selecting a card calls `PUT /api/v1/tenants/subscription`.  
   - Confirmation toast: “Great choice! You can switch plans anytime in Settings → Billing.”  
   - Rail marks step complete once `TenantOnboardingStatusDto.PlanConfirmed = true`.

3. **Security setup**  
   - Two cards with marketing-friendly pros/cons:
     - **Evermail-managed (Fast Start)**  
       - Headline: “Be searching in minutes.”  
       - Bullets: “Evermail holds the keys”, “Best for trials & debugging”, “Switch to BYOK anytime.”  
       - CTA button “Use quick-start keys” triggers `UpsertTenantEncryptionSettings` with `Provider=EvermailManaged`. The backend now auto-provisions the managed protector (sets `EncryptionPhase = EvermailManaged`, stamps `LastVerifiedAt`, and invalidates onboarding caches) so the wizard immediately shows “Keys provisioned automatically.”
     - **Customer-managed key (BYOK)**  
       - Headline: “Your key, your rules.”  
       - Bullets: “Evermail can’t see plaintext without your approval”, “Pairs with Azure Key Vault, AWS KMS, or offline bundles”, “Perfect for compliance teams.”  
       - When selected, an inline panel embeds the existing Offline BYOK Lab component so admins can generate a test key bundle using browser WebCrypto. Copy reassures them they can swap in Azure/AWS credentials later.
   - Side copy references their OAuth identity (“`kalle@contoso.com` will manage these keys”) to keep context.  
  - Backend stores a new `TenantSecurityPreference` string (`QuickStart` or `BYOK`) inside onboarding status so the wizard can resume correctly.

4. **Billing acknowledgement**  
   - Placeholder step while Stripe integration is pending.  
   - Panel reminds the user of the plan they chose, estimated monthly/annual cost, and offers two buttons: “Connect Stripe (coming soon)” (disabled) and “Acknowledge billing later” (records `PaymentAcknowledgedAt`).  
   - Marketing copy: “We’ll prompt you to enter billing once you’re ready to import real archives. No charges until you connect Stripe.”  
   - Stored in backend as `Tenant.PaymentAcknowledged`/timestamp so we know they’ve seen the paywall expectations.

5. **Upload kickoff**  
   - Final step shows a celebratory card (“Security locked in. Billing queued up. Let’s import your first mailbox.”) plus plan limit reminders.  
   - Primary CTA goes to `/upload`; secondary link surfaces documentation/video.  
   - Checklist recaps prior steps with checkmarks so admins see the full journey they just completed.

###### Messaging & Content Guidelines
- **Tone**: friendly SaaS marketing. Emphasize benefits before trade-offs (“Switch to BYOK whenever compliance demands it” instead of “Managed keys are less secure”).  
- **Google/Microsoft identity**: always show the signed-in provider icon in the hero + sidebar identity card so users know which account is active.  
- **Security choice rationale**:
  - Evermail-managed: highlight speed, zero configuration, ideal for demos/debugging. Disclose that Evermail infrastructure can decrypt during operations.  
  - BYOK: highlight tenant-controlled keys, zero-operator access, compliance readiness. Mention that setup takes longer and requires vault access.  
- **Offline BYOK lab integration**: reuse the existing component but frame it as “Need a key right now? Generate a test bundle without leaving your browser.” Provide reminders (“Store this bundle safely—Evermail can’t recover it”).  
- **Placeholder billing step**: be transparent (“Stripe hookup coming soon”) yet actionable (“Mark this step as acknowledged so we can alert you when checkout is ready”).  
- **Data contract updates** (for future implementation):
  - `TenantOnboardingStatusDto` gains `SecurityPreference`, `PaymentAcknowledged`, `PaymentAcknowledgedAt`, and `IdentityProvider`.
  - Wizard uses these flags to unlock the Upload step only when plan + security + billing acknowledgements are all true.

This spec keeps the onboarding flow grounded in the current implementation while documenting the UX copy, OAuth considerations, and marketing positioning we expect to ship alongside the managed/BYOK security choices.

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

##### Admin Dashboard Action Plan
1. **Adopt shared look & feel**
   - Reuse the existing Blazor WebApp theme tokens (`wwwroot/app.css`, `.modern-card`, `.stat-grid`, `.glass-panel`, etc.) via a shared Razor Class Library so admin surfaces inherit typography, spacing, gradients, and dark-mode rules without divergence.
   - Mirror navigation, card layouts, and hero/header treatments from user pages to keep brand continuity; extend tokens only in `app.css` and propagate to both apps.
2. **Finalize admin API contracts**
   - Document `/api/v1/admin/*` endpoints in `Documentation/API.md` covering tenants, users, mailboxes, jobs, analytics, billing, and audit logs.
   - Add DTOs in `Evermail.Common` for dashboard tiles (queue depth, processing SLA, storage totals) before UI work begins so UI and backend evolve in lockstep.
3. **Scaffold Evermail.AdminApp**
   - Ensure the project references the shared theme library, registers MudBlazor, and enforces `[Authorize(Roles = "Admin,SuperAdmin")]` plus tenant-aware filters even for admin views.
   - Stand up layout + navigation (Dashboard, Tenants, Users, Mailboxes, Jobs, Analytics, Billing, Logs) with placeholder components styled via the shared tokens to quickly validate look and feel.
4. **Iterate by vertical slices**
   - **Operations dashboard**: live queue depth, ingestion throughput, failure counts (SignalR/polling) plus quick links into Aspire logs.
   - **Tenant/user management**: searchable grids, role toggles, plan overrides, retention settings, enforcement of multi-tenant guardrails prior to invoking actions.
   - **Mailbox/job lifecycle**: status timelines, retry/purge controls, deletion queue visibility, attachment footprint per mailbox.
   - **Analytics/billing**: MRR tiles, churn/ARPU charts, Stripe sync state, storage per tier, cost vs revenue deltas.
   - **Audit & error intelligence**: surfaced Application Insights exceptions, audit trail search, downloadable CSV exports.
5. **Quality gates**
   - Automated tests for admin endpoints (role enforcement, tenant scoping, pagination).
   - Storybook-style visual regression (optional) or screenshot diffs to ensure shared look & feel parity with the main WebApp.
   - Observability hooks (structured logging, Application Insights custom metrics) to power the dashboard itself.
6. **Preseed privileged identities**
   - Keep a single ASP.NET Core Identity store for WebApp, AdminApp, and API clients; privilege levels come from roles (`User`, `Admin`, `SuperAdmin`) on the same user objects, which preserves tenant filtering and JWT claim generation.
   - Extend `DataSeeder` to accept a `UserManager<ApplicationUser>` and create default SuperAdmin accounts (e.g., `founder@evermail.com`, `ops@evermail.com`) with randomly generated passwords stored in user secrets/Key Vault for production.
   - Ensure the seed users belong to a dedicated `EvermailOps` tenant so their activity never collides with customer data, and document rotation procedures (disable default SuperAdmins after onboarding real admins).
   - Configure the admin/API JWT policies to require the same Audience/Issuer as the main app so tokens remain interchangeable; scope API clients via roles/claims rather than a separate credential silo.

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

- **Supported formats**: raw `.mbox`, Google Takeout `.zip` (multiple `.mbox` files), standalone `.pst`, Microsoft export `.zip` (contains `.pst`), Outlook offline cache files (`.ost` / `.ost.zip`), `.eml` bundles, and `.eml`/Maildir-style `.zip` archives.
- **Detection**: The UI no longer asks users to pick a format. `ArchiveFormatDetector` runs immediately after each upload completes, opens the blob directly from Azure Storage, and inspects headers/ZIP entries to determine whether it is a PST/OST (raw or zipped), Google Takeout `.mbox`, Maildir `.eml` bundle, or single `.eml`. The resolved `SourceFormat` is persisted on both `Mailbox` and `MailboxUpload` before we queue ingestion.
- **Normalized sizing**: When `ArchivePreparationService` finishes converting the archive into a canonical `.mbox`, the worker records `NormalizedSizeBytes` on both mailboxes and uploads. UI progress bars and storage dashboards always prefer this value so “Processed 1.3 GB / 1.3 GB” matches the uncompressed workload even if the original upload was a much smaller ZIP.
- **Normalization**: `ArchivePreparationService` streams every upload through a temp workspace before `MailboxProcessingService` sees it. Google Takeout and Apple Mail ZIP exports are expanded into a single `.mbox`, loose `.eml` files (or `.eml` ZIP bundles) are wrapped into a synthetic `.mbox`, and Outlook `.pst` / `.pst.zip` / `.ost` payloads are converted to `.mbox` via `PstToMboxWriter`.
- **PST/OST conversion**: `PstToMboxWriter` embeds the open-source `XstReader` engine (Ms-PL) to walk every folder/message in the PST/OST, hydrate recipients + attachments, and emit canonical `MimeMessage` instances into an `MboxrdWriter`. Attachments stay inline so the existing hashing/dedupe/attachment pipeline works untouched. The implementation follows Microsoft’s [MS-PST specification](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-pst/) and the official [Outlook export workflow](https://support.microsoft.com/en-us/office/export-emails-contacts-and-calendar-items-to-outlook-using-a-pst-file-14252b52-3075-4e9b-be4e-ff9ef1068f91).
- **Plan-aware inflation guardrails**: After normalization we compare the inflated byte count against the tenant’s `SubscriptionPlan.MaxFileSizeGB`. If a 700 MB ZIP expands to 4 GB but the tenant’s limit is 1 GB the worker fails fast with a descriptive error, blocking compressed payload attacks and aligning with the safeguards outlined in `Documentation/Security.md`.
- **Client-side parity**: When Zero-Access Archive Mode is enabled the browser consults the same `SourceFormat` metadata to decide which parser to run (mbox, pst/ost, eml) before encrypting chunks client-side. The new normalization service makes it easy to short-circuit the server-side conversion path once the WASM extractor ships.
- **Encrypted upload contract**: `POST /api/v1/mailboxes/encrypted-upload/initiate` prepares the mailbox, returns a SAS URL, and hands back a `tokenSalt`. After the browser encrypts and uploads the chunks it calls `POST /api/v1/mailboxes/encrypted-upload/complete` with the scheme metadata, key fingerprint, ciphertext statistics, and the hashed deterministic tag tokens. Server-side ingestion simply records metadata; the worker never touches ciphertext.
- **Deterministic tokens (phase 1)**: Tag hashes live in `ZeroAccessMailboxTokens` (TenantId + MailboxId + TokenType + TokenValue). Search surfaces add a `tagToken` filter so the client can hash the user’s query with the same token key and have the server narrow the result set *before* the mailbox is downloaded/decrypted locally. Future phases will extend the same storage to per-email tokens emitted by the WASM parser.
- **Multi-admin BYOK bundles**: `TenantEncryptionBundle` stores wrapped DEK bundles per admin (label, checksum, createdBy) so several privileged users can maintain their own recovery material without overwriting each other. The Offline BYOK lab and admin encryption page now list these bundles, allow re-download, and expose a `DELETE` affordance for stale entries.
- **Normalization flow**:
  1. Raw `.mbox` → streamed directly into MimeKit’s `MimeParser`.
  2. `.zip` containing `.mbox` → entries are concatenated into a temporary `.mbox` file then streamed (still honoring the 5 GB per-file plan limit).
  3. `.pst` / `.pst` inside `.zip` / `.ost` / `.ost.zip` → the worker vendors Microsoft’s MS-PST spec via XstReader. Messages are converted into `MimeMessage` instances (headers, threading info, attachments) and emitted into a temporary `.mbox` via `MboxrdWriter`.
  4. `.eml` / Maildir archives → every message is parsed with MimeKit, re-wrapped in `.mbox`, and fed through the same ingestion path so deduplication, hashing, encryption, and attachment policies stay consistent.
- **Zero-copy discipline**: all temporary files live under `Path.GetTempPath()` with GUID file names, inherit the tenant’s ACLs, and are shredded immediately after parsing (success or failure). We never leave PST/plaintext artifacts behind.
- **Progress tracking**: Extractors report uncompressed byte totals back to `MailboxUpload.ProcessedBytes`, allowing the UI to show “Unzipping 1.2 GB / 3.4 GB” or “Converting PST (412/3 891 messages)” before the SQL inserts start.

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
  5. Azure Queue Storage caps message visibility timeouts at 7 days, so the worker re-hides each deletion message in 1-minute/7-day bounds until the job is due, and the enqueue operation sets `timeToLive = -1` so the message survives longer retention windows.
  6. Deletion jobs aggressively mark every outstanding upload (including failed imports) as deleted, clear orphaned blob pointers, and reset `IsPendingDeletion`, so even "borked" mailboxes can be purged with a single run. When SuperAdmins tick **Purge immediately**, the worker treats the job as a forced purge—suppresses “not found” errors, logs warnings, and always soft-deletes the mailbox row (regardless of lingering blobs/records) so the UI list is cleaned up even if the data was already missing.
  7. Every state change writes to `AuditLogs`, enabling GDPR traceability.

#### Threading, Recipient Indexing & Search Surfaces
- **Conversation graph**: Every email rolls up to `EmailThreads` via normalized `ConversationKey` (first reference → In-Reply-To → MessageId). Threads store participant summary, message counts, first/last timestamps, and power UI grouping.
- **Recipient index**: `EmailRecipients` table (plus a flattened `RecipientsSearch` column) enables instant filtering by To/Cc/Bcc/Reply-To/Sender without scanning JSON payloads.
- **Full-text coverage**: SQL Server CONTAINSTABLE now searches `Subject`, `TextBody`, `HtmlBody`, `RecipientsSearch`, `FromName`, and `FromAddress`, matching Microsoft Learn guidance for multi-column FTS catalogues. The endpoint falls back to `LIKE` queries across the same set when FTS is unavailable.

#### Search Experience Enhancements (Nov 2025 refresh)
- **Result quality cues**: The `Emails.razor` list no longer exposes the raw SQL `RANK`. Each row renders a three-dot `match-strength` indicator derived from the normalized rank plus readable chips sourced from the API’s `matchFields`/`matchSources` arrays (“Matched in subject + body”, “Attachment hit”, etc.). The numeric value is still returned in the payload for telemetry and sort overrides, but only the human-readable cues hit the UI.
- **Card + table hybrid layout**: Search results now use a custom card layout (`email-card` components rendered via `MudVirtualize`) with subject + sender on the first line, thread/date metadata on the second, and a quick-action rail (preview, open, pin) revealed on hover/focus. The right rail also shows attachment pills and folder/label chips so users can triage without leaving the list. Result groups (“Pinned”, “All matches”) show sticky dividers when favorites are present.
- **Preferences & date formatting**: `UserDisplaySettings` persists date formats (`MMM dd, yyyy` default, `dd.MM.yyyy` alternative), density, keyboard shortcuts, match navigator, and auto-scroll flags. `UserPreferencesService` hydrates those settings at startup, `IDateFormatService` centralizes formatting, and the `/settings` page surfaces MudSelect/MudSwitch controls so the entire app (list, detail, exports) respects the same format.
- **Account & security settings**: `/settings` now consumes `GET /api/v1/users/me/profile` for identity, tenant limits, and role awareness, plus integrates the 2FA endpoints (`/api/v1/auth/enable-2fa`, `/api/v1/auth/verify-2fa`). Admins see workspace usage + plan badges pulled from onboarding status, while every user can enable 2FA inline via QR-code enrollment, view backup codes, and manage search/display preferences without leaving the page. All cards reuse `.settings-card` + `.settings-grid` tokens for consistency with Mailboxes/Emails surfaces.
- **Contextual snippets & metadata**: `SearchSnippetBuilder` locates the first keyword hit (post tokenization), extracts ~160 characters around it, HTML-encodes the window, and injects `<mark class="search-hit">` spans before returning both `snippetOffset`/`snippetLength` and a sanitized `highlightedSnippet`. Attachment matches list filenames inside `matchSources` so the UI can render “Matched in attachment: invoice.pdf”.
- **Detail view highlights**: `EmailDetail.razor` wraps the rendered HTML/text body inside a searchable container and calls `EvermailSearchHighlights` once the DOM hydrates. When the “auto-scroll to keyword” preference is enabled the component jumps to the first `<mark>` automatically, and a floating “Jump to next match” pill cycles through matches via the JS navigator. The toggle lives in Settings (“Show match navigator button”) and persists per user.
- **Saved filters & chips**: `/emails/saved-filters` powers reusable chips (e.g., “From Alice last 30 days”) above the list. Chips can be reordered, favorited, edited, or deleted inline without leaving the page, and tapping a chip instantly rehydrates the query builder.
- **Keyboard + quick actions**: `/` focuses the search box, `j`/`k` move the active selection, `Enter` opens the highlighted row, and `p` pins/unpins. The shortcut handler respects the user’s “Keyboard shortcuts enabled” preference. Hover/focus states expose preview/open/pin buttons so mouse users get the same speed.
- **Pinned & favorited results**: Pins persist in `PinnedEmailThreads`. When a pinned email or conversation matches the current query it floats to a dedicated block at the top of the virtualized list (still honoring tenant/user filters) and carries a “Pinned” badge alongside timestamp + thread depth context.
- **Attachment & metadata pills**: Inline attachment pills show filename + size with a download icon powered by the attachment SAS endpoint. Folder/mailbox chips, match summaries, and saved filter badges reuse the shared `status-pill`/`action-pill` tokens for brand consistency.
- **Density, skeletons & virtualization**: Users can toggle Cozy vs. Compact density (stored in `ResultDensity`). Large result sets stream through Blazor’s `<Virtualize>` (100-row buffer) to keep the DOM small, and while the API response is inflight the UI renders shimmer skeleton cards so the layout stays steady and perceived latency stays under 500 ms. This pattern applies equally in light/dark themes via shared CSS tokens.
- **Parser sync warning**: Highlight accuracy depends on the shared `SearchQueryParser`. Any syntax change (operators, grouping, quoted phrases) must update both the parser and the snippet/highlight builders so API + UI remain in lockstep.

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
- `ZeroAccessMailboxTokens` - Opaque deterministic tag hashes for zero-access mailboxes (powers equality filtering without plaintext)
- `TenantEncryptionBundles` - Registry of offline BYOK bundles per admin (wrapped ciphertext + metadata)
- `UserDataExports` / `UserDeletionJobs` - GDPR job tracking
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

### Confidential Content Protection Layer

To satisfy “no admin can read my email” requirements, Evermail introduces a dedicated security layer that sits between the data plane and every workload that touches decrypted content.

- **Envelope encryption**: Each mailbox/upload owns an AES-256-GCM Data Encryption Key (DEK). The DEK is wrapped with the tenant’s Master Key (TMK) that lives in the tenant’s Azure Key Vault or Managed HSM. The platform only stores wrapped DEKs; plaintext keys exist solely inside confidential workloads.
- **Tenant-managed keys (BYOK)**: Tenants provide a Key Vault key identifier during onboarding. We configure Key Vault key-release policies so that unwrap operations succeed only when the request comes from an attested Evermail workload. Operators with SuperAdmin access cannot call `UnwrapKey` manually because the policy denies non-attested contexts.
- **Confidential workloads**: Ingestion, search, and AI services run inside Azure Confidential Container Apps (AMD SEV-SNP). Each container image is signed; its attestation quote is validated automatically before Key Vault releases any DEK. Control-plane APIs (billing, admin UI) remain outside the enclave and never see plaintext.
- **Deterministic encrypted indexes**: After decrypting inside the TEE, workers normalize and encrypt search tokens using AES-SIV with a tenant-specific salt. SQL Server stores only encrypted tokens yet can still satisfy equality lookups, keeping FTS performance while protecting content from database admins.
- **Audit + monitoring**: Every decrypt/unwrap event is logged in both Key Vault and the Evermail `AuditLogs` table, including attestation hash, workload version, and purpose. Alerts trigger on unusual key-release patterns.
- **Optional client share**: For the Paranoid tier, tenants can require a passphrase-derived key that double-wraps each DEK. Background jobs receive short-lived passphrase tokens; exports can be decrypted offline via an open-source CLI so customers remain in control even if Evermail infrastructure is compromised.

#### Phased delivery roadmap

| Phase | Scope | Azure references |
| --- | --- | --- |
| **Phase 1 – BYOK foundation (MVP)** | Tenants onboard a customer-managed key (CMK) stored in their Azure Key Vault/Managed HSM; Evermail rotates per-mailbox DEKs, wraps them with the TMK, and enforces strict RBAC/PIM to limit unwrap operations. Plaintext still flows through standard Container Apps, so we clearly document that superadmins retain emergency access. Secure Key Release policies are authored up-front but initially reference “allowEvermailOps” attestation placeholders so we can validate the flow. | [Secret & key management](https://learn.microsoft.com/en-us/azure/confidential-computing/secret-key-management) |
| **Phase 2 – Zero-trust enforcement** | Move ingestion, search, and AI workers into Azure Confidential Container Apps/AKS pools ([deployment models](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-computing-deployment-models), [confidential containers](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-containers)). Attach production-grade Secure Key Release policies so Key Vault only unwraps DEKs for Microsoft Azure Attestation (MAA) claims emitted by the signed confidential images ([SKR + attestation](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation)). At this point Evermail operators cannot read tenant mail even with elevated permissions. |

Implementation highlights:

1. **Attested workloads** – Confidential Container Apps or AKS node pools are provisioned in separate Azure resources (managed outside Aspire’s AppHost) and publish OCI images compiled from `Evermail.IngestionWorker` / AI services. Each deploy step records the expected measurement hash so the SKR policy can match `x-ms-isolation-tee.*` claims.
2. **Key lifecycle** – Tenant onboarding flow walks the admin through generating/importing a TMK, assigning Evermail’s managed identity minimal `release` permissions, and storing the Key Vault URI against the tenant row. DEKs are rotated per mailbox upload and re-wrapped whenever the tenant rotates their TMK.
3. **Cross-plane integration** – Aspire still orchestrates the rest of the estate (queues, SQL, Blob, WebApp). Queue payloads contain the wrapped DEK identifier; confidential workers fetch the message, perform MAA attestation, call SKR, and process data entirely inside the TEE. Responses returned to the public API remain encrypted unless the caller presents a tenant-scoped token.
4. **Operational tooling** – Azure Confidential Ledger (priced at ~$3/day per ledger instance per the [official announcement](https://techcommunity.microsoft.com/blog/azureconfidentialcomputingblog/price-reduction-and-upcoming-features-for-azure-confidential-ledger/4387491)) stores append-only proofs that a given DEK was unwrapped, by which container revision, and for what purpose.

##### Phase 1 concrete architecture (current sprint)

- **SecureKeyReleaseService** (lives inside `TenantEncryptionService`) exposes three new flows:
  1. `SecureKeyReleaseTemplateGenerator` emits SKR JSON with a permissive `allowEvermailOps` attestation rule plus the tenant’s managed-identity object ID. The generator is deterministic, so identical tenants always receive the same template (handy for diffing).
  2. `ConfigureSecureKeyReleaseAsync` canonicalizes the uploaded JSON (parses → `JsonDocument` → writes with `JsonSerializerOptions { WriteIndented = true }`), computes a SHA-256 hash, updates the `TenantEncryptionSettings` record, and stamps `SecureKeyReleaseConfiguredAt/By`.
  3. `ResetSecureKeyReleaseAsync` clears the JSON/hash flags so tenants can rotate or switch providers.

- **DTO and API contract** – `TenantEncryptionSettingsDto.SecureKeyRelease` now includes the policy hash and configured timestamp. Dedicated endpoints expose the full JSON only to the owning tenant. The UI never stores drafts server-side; admins edit locally then submit the final JSON so we maintain a clean audit trail of approved policies.

- **Onboarding dependencies** – `OnboardingStatusCalculator` marks Azure/AWS BYOK as “configured” only when both the provider metadata and `IsSecureKeyReleaseConfigured` are true. The wizard highlights SKR as the final checkbox once keys are saved.
- **UI-only workflow** – The admin encryption page drives every SKR action through buttons and status chips; we never ask tenants to download scripts or edit JSON manually. “Generate & apply” posts the template automatically, while “Clear policy” calls the delete endpoint in one click.

- **Aspire integration** – The Aspire AppHost keeps orchestrating WebApp + Worker, but the SKR services run inside the same solution so developers can iterate with `dotnet run Evermail.AppHost`. No new Azure resources are required in Phase 1; the Key Vault policies still point at the managed identity associated with the AppHost deployment.

This layer allows us to advertise zero-trust guarantees: decrypt operations run only in measured hardware, tenant keys never leave customer control, and staff-level access provides observability/operations without exposure to message content.

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

## Potential Enhancements (Backlog Inspiration)

Documented ideas to guide future roadmap conversations:

- **Processing Transparency Dashboards**: Visualize each mailbox’s lifecycle (upload → parsing → indexing → retention countdown) with retry counts and purge timers so users instantly know status.
- **Insights & Analytics**: Convert ingestion metadata into shareable dashboards—top senders, conversation spikes, attachment heatmaps, quota vs. plan limits, and GDPR audit exports.
- **Workspace Collaboration**: Expose the planned `Workspaces` table earlier so tenants can invite teammates, assign per-mailbox permissions, tag important threads, and leave annotations.
- **Advanced Search Helpers**: Layer guided filters (date histograms, sender chips, boolean builders) plus saved searches/recent queries on top of the SQL FTS stack to improve discoverability.
- **Attachment-Centric Flows**: Offer an attachment browser (filter by type/size/sender), inline previews for common formats, dedupe warnings, and bulk export powered by Blob metadata.
- **AI Assist Features**: Use the confidential compute pipeline to generate per-thread summaries, sentiment labels, suggested follow-ups, and contact cards while keeping tenant keys in control.
- **Direct Account Imports**: Ship Gmail/Microsoft Graph connectors with a “Connect mailbox” wizard so users can pull live accounts without creating .mbox files (flagged beta until quotas verified).
- **Compliance Self-Service**: Bundle GDPR requests into the UI—self-serve export progress, retention policy badges per plan, configurable purge windows, and audit log browsing/searching.
- **Notifications & Mobile Prep**: Before the MAUI app, add push/email/web notifications for completed ingestions or saved-search hits plus offline-friendly responsive views of summaries.
- **Predictable Billing Insights**: Surface in-app cost estimators showing projected storage, AI credit usage, and upcoming invoices so users can upgrade before hitting limits.

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

### Compliance console (admin UI)

The admin area gains a dedicated compliance workspace that folds the raw `AuditLogs` table and GDPR job entities into a friendly MudBlazor experience:

1. **Data flow**
   - `AuditLogQueryService` (new application service) projects filtered logs via EF Core, applying tenant-scoped filters and pagination.
   - `GET /api/v1/audit/logs` returns a `PagedResult<AuditLogDto>` that the MudBlazor table consumes via server-side data mode.
   - `GET /api/v1/audit/logs/export` re-runs the same filter but streams a CSV (`text/csv`) with up to 10 k rows and a SHA-256 hash header for tamper evidence.
   - `GET /api/v1/compliance/gdpr-jobs` aggregates `UserDataExports` + `UserDeletionJobs` so the UI can render status chips and download buttons.

2. **UI components**
   - `Admin/AuditLogs.razor` hosts the grid, filter panel, anomaly summaries, and export button. It subscribes to the global rate-limit notifier/toast service so 429s remain understandable even while reviewing logs.
   - `AuditLogDetailsDrawer.razor` renders the selected row’s metadata, prettifying JSON in the `Details` column and flagging mismatched IP → geo lookups (future enhancement).
   - `GdprJobTimeline.razor` reuses the existing user-export DTOs but scopes them to the tenant, listing requester, timestamps, status, and SAS download links (read-only).

3. **Aspire considerations**
   - Everything stays inside the current Aspire solution—no extra services to orchestrate. The APIs sit in the WebApp project; the UI is another Blazor page.
   - Exports stream directly from the WebApp container, so no Az Functions or storage staging is required. Long-running exports reuse the existing background `UserDataExport` infrastructure if we later need asynchronous dumps.

4. **Security**
   - Endpoints are `[Authorize(Roles = "Admin")]` and reuse `TenantContext` so tenants never leak data across workspaces.
   - CSV download links never expose signed URLs; instead we send the file inline with `Content-Disposition: attachment`.
   - Every export action writes a second audit entry (`AuditLogCsvExported`) to prove evidence packs were generated.

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

**Last Updated**: 2025-11-24  
**Document Owner**: CTO  
**Review Frequency**: Monthly or after major architectural changes

