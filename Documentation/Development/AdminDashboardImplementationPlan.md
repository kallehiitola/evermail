# Admin Dashboard Implementation Plan

> **Last Updated:** 2025-11-19  
> **Scope:** Evermail.AdminApp (Blazor Server) + `/api/v1/admin/*` surface  
> **References:** `Documentation/Architecture.md` ("Admin Dashboard Action Plan"), `Documentation/API.md`, `Documentation/Security.md`

---

## 1. Objectives

1. Mirror the look & feel of `Evermail.WebApp` (shared theme tokens, MudBlazor components) while exposing operational tooling for admins.
2. Leverage the existing ASP.NET Core Identity store so `Admin` / `SuperAdmin` roles unlock admin surfaces without duplicating accounts.
3. Implement admin APIs first (documented in `Documentation/API.md`), then bind Blazor Server pages for real-time insight and control.
4. Maintain strict multi-tenancy and auditing: every action logs to `AuditLogs`, and tenant-scoped data stays isolated even in admin views.

---

## 2. Prerequisites

- [ ] Roles `User`, `Admin`, `SuperAdmin` seeded (`DataSeeder`).
- [ ] Bootstrap SuperAdmin accounts in `EvermailOps` tenant with credentials stored in user secrets/Key Vault.
- [ ] Shared Razor Class Library (RCL) for typography, components, and CSS tokens (exported from WebApp or referenced directly).
- [ ] Aspire wiring ensures AdminApp shares identity DB + config.

---

## 3. Architecture Alignment

| Layer | Deliverable | Notes |
|-------|-------------|-------|
| API | `/api/v1/admin/*` endpoints | Minimal APIs under `Evermail.WebApp`; all require `Admin`/`SuperAdmin`. |
| Admin UI | `Evermail.AdminApp` | Blazor Server, MudBlazor, shared layout + nav. |
| Auth | Shared Identity | JWT issuer/audience identical; AdminApp uses cookie + JWT hybrid (Blazor Server default) with role checks. |
| Storage | Same Azure SQL + Blob | Read-only insights obey tenant filters; destructive actions double-check ownership. |

---

## 4. Implementation Phases

### Phase A – Foundations (Day 0-1)
1. **Theme Sharing**
   - Extract existing CSS tokens/components into `Evermail.Shared.UI` or reference `Evermail.WebApp/wwwroot/app.css` via static file package.
   - Add dark-mode + layout helpers to AdminApp.
2. **Project Setup**
   - Ensure `Evermail.AdminApp` targets `net10.0`, references `MudBlazor`, `Evermail.Common`, and uses the shared DI extensions (in line with the upgraded solution baseline).
   - Configure authentication/authorization (cookie auth + JWT fallback) with `[Authorize(Roles = "Admin,SuperAdmin")]` globally.
3. **Navigation Shell**
   - Implement `App.razor`, `MainLayout.razor`, and `NavMenu.razor` with sections: Dashboard, Tenants, Users, Mailboxes, Jobs, Analytics, Billing, Logs, Settings.

### Phase B – API + DTO Layer (Day 1-2)
1. Document all admin endpoints in `Documentation/API.md` (request/response models, roles, sample payloads).
2. Add DTOs under `Evermail.Common.DTOs.Admin` for:
   - `TenantSummaryDto`, `UserSummaryDto`, `MailboxJobDto`, `QueueDepthDto`, `BillingSnapshotDto`, `AuditEntryDto`.
3. Implement Minimal APIs in `Evermail.WebApp/Evermail.WebApp/Endpoints/AdminEndpoints.cs`:
   - `GET /admin/dashboard` – aggregate KPIs.
   - `GET /admin/tenants`, `GET /admin/tenants/{id}`, `PATCH /admin/tenants/{id}`.
   - `GET /admin/users`, `PATCH /admin/users/{id}/roles`.
   - `GET /admin/mailboxes`, `POST /admin/mailboxes/{id}/retry`.
   - `GET /admin/jobs/queue-depth`, `POST /admin/jobs/{id}/retry`.
   - `GET /admin/analytics/storage`, `/analytics/revenue`.
   - `GET /admin/logs/audit`, `GET /admin/logs/errors`.
4. Add integration tests enforcing role requirements + tenant isolation.

### Phase C – UI Vertical Slices (Day 3-6)

1. **Operations Dashboard**
   - Tiles: Queue depth, ingestion throughput (emails/min), failed jobs, total storage, active tenants/users.
   - Real-time updates via SignalR hub or 10s polling.
2. **Tenant & User Management**
   - Searchable tables with filters (tier, status, retention, last login).
   - Actions: suspend tenant, adjust plan limits, reset 2FA, assign roles (SuperAdmin only).
   - Show storage/mailbox usage charts per tenant.
3. **Mailbox / Job Control**
   - Table of in-flight uploads/deletions with status timeline, ability to retry/purge (SuperAdmin).
   - Detail drawer with mailbox metadata, attachments footprint, audit trail.
4. **Analytics & Billing**
   - Charts for MRR, ARPU, churn, storage cost vs revenue.
   - Stripe sync view listing customers, subscription states, failed invoices.
5. **Logs & Audit**
   - Searchable audit log list (action, actor, tenant, timestamp).
   - Exception viewer hooking into Application Insights API (filter by service).
6. **Compliance & GDPR Tooling**
   - Add compliance center page exposing retention policies per tenant, export/delete queues, and legal hold indicators.
   - Surface GDPR requests (export, delete) with approval workflows, SLA timers, and ability to trigger automated jobs (SuperAdmin only).
   - Provide immutable storage status, audit trail downloads, and compliance attestations per plan (read-only for Admin, write actions for SuperAdmin).

### Phase D – Cross-Cutting Concerns (Day 6-7)

- **Auditing:** wrap admin APIs with middleware that records `AuditLog` entries.
- **Error handling & alerts:** surface toast notifications, integrate Application Insights alerts.
- **Role management tooling:** internal page to add/remove roles for bootstrap accounts (dev only).
- **Docs & Tests:** update `Documentation/Architecture.md`, `Documentation/API.md`, add README section on AdminApp, expand integration tests.
- **Compliance hardening:** integrate GDPR tooling endpoints, ensure legal hold flows emit audit entries, validate retention overrides respect tier rules.

---

## 5. Security Checklist

- [ ] Global `[Authorize(Roles = "Admin,SuperAdmin")]` on AdminApp routes.
- [ ] API requires same roles + explicit claim checks for destructive operations.
- [ ] Tenant isolation: even when viewing all tenants, queries never mix data unless user is SuperAdmin.
- [ ] Audit all admin actions with actor, tenant, payload snapshot.
- [ ] CSRF protection for Blazor Server forms; anti-forgery tokens on POSTs.
- [ ] Secrets (bootstrap admin passwords, Stripe keys) stored in Key Vault.

---

## 6. Telemetry & Observability

- Custom metrics: `mailbox.queue.depth`, `mailbox.process.time`, `admin.actions.per.hour`.
- Dashboard pulls from Application Insights (REST API) for error feed.
- Log correlation IDs attached to admin-triggered operations for quick cross-service tracing.

---

## 7. Deliverables & Exit Criteria

1. `Documentation/API.md` updated with admin endpoints.
2. `Evermail.AdminApp` navigation + at least Dashboard + Tenants slices functional.
3. Seeded SuperAdmin accounts verified; login + role gating tested.
4. Admin-triggered actions logged in `AuditLogs` and visible in admin log view.
5. Integration tests covering:
   - Unauthorized access blocked.
   - Admin vs SuperAdmin permissions.
   - Tenant isolation enforcement.

Once these criteria are met, we can proceed to iterative enhancements (AI insights, bulk operations, automation hooks).

---

## 8. Timeline (Estimate)

| Day | Focus |
|-----|-------|
| 0 | Documentation + shared theme extraction |
| 1 | Admin API contracts + DTOs |
| 2 | Minimal API implementation + tests |
| 3 | Admin UI shell + Dashboard slice |
| 4 | Tenants & Users slice |
| 5 | Mailboxes/Jobs slice |
| 6 | Analytics + Billing slice |
| 7 | Logs, audit, hardening, docs |

*Adjust as needed based on feedback and testing results.*

---

