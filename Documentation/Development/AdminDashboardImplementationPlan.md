# Admin Dashboard Implementation Plan (Evermail internal SuperAdmin portal)

> **Last Updated:** 2025-12-15  
> **Scope:** Evermail.AdminApp (Blazor Server) — Evermail-internal SuperAdmin portal (not customer-facing)  
> **References:** `Documentation/Architecture.md` ("Admin Dashboard Action Plan"), `Documentation/API.md`, `Documentation/Security.md`

---

## 1. Objectives

1. Mirror the look & feel of `Evermail.WebApp` (reuse `wwwroot/app.css` tokens + layout patterns) while exposing Evermail ops tooling.
2. Enforce **OAuth-only access** (Google + Microsoft) with an allowlist (`kalle.hiitola@gmail.com` and `@evermail.ai`) so no customer can ever sign in.
3. Keep the AdminApp deployable to Azure and make it the single “SaaS management” surface (health, adoption/funnel, revenue/costs, tenant ops).
4. Avoid a “one-page-per-button” UI. Prefer a few thematic pages with multiple related sections.
5. Runtime controls must be explicit and safe: show current setup, require confirmations, and support restarts where required.

---

## 2. Prerequisites

- [ ] Roles `User`, `Admin`, `SuperAdmin` seeded (`DataSeeder`).
- [ ] OAuth credentials configured for both providers (same keys as WebApp).
- [ ] Allowlist configured: `kalle.hiitola@gmail.com` and `@evermail.ai` (no `evermail.com` domain).
- [ ] AdminApp can read from the platform database (Azure SQL) and storage/queue configuration (Key Vault).

---

## 3. Architecture Alignment

| Layer | Deliverable | Notes |
|-------|-------------|-------|
| Admin UI | `Evermail.AdminApp` | Blazor Server. Uses the same visual language as WebApp (shared CSS tokens + components). |
| Auth | OAuth allowlist | Google + Microsoft OAuth only. Only allowlisted identities can sign in; non-allowlisted users are rejected. |
| Data access | Direct + scoped | AdminApp reads the platform database without tenant query filters (SuperAdmin context) and can call Azure control plane APIs for service status. |
| Customer admin tools | Stay in WebApp | Tenant-admin pages (`/admin/*` inside WebApp) remain customer-facing and must not move into AdminApp. |
| API | Optional `/api/v1/admin/*` | If/when we add admin APIs, they are for internal integrations; AdminApp should not depend on undocumented or unimplemented endpoints. |

---

## 4. Implementation Phases

### Phase A – Foundations (Day 0-1)
1. **Theme sharing (WebApp parity)**
   - Reuse `Evermail.WebApp/wwwroot/app.css` tokens + shared assets (logo) in AdminApp.
   - Keep dark-mode support consistent with WebApp.
2. **Auth (OAuth-only + allowlist)**
   - Implement Google + Microsoft OAuth sign-in.
   - Enforce allowlist (`AllowedEmails`, `AllowedDomains`) on every OAuth callback.
   - Assign an internal role claim (`SuperAdmin`) for allowlisted users.
3. **Navigation shell**
   - Replace template layout with the same sidebar + card language as WebApp.
   - Start with 3 thematic pages:
     - **Ops Console** (runtime status, diagnostics, restart actions)
     - **Tenants & Users** (cross-tenant listing + role management)
     - **Business Dashboard** (adoption + revenue/cost placeholders → real metrics later)

### Phase B – Ops Console (Day 1-2)
1. **Runtime status (read-only)**
   - Show: SQL target, storage/queue target, Key Vault source, FTS status, queue depths, and basic “last activity” counters.
2. **Runtime controls**
   - Provide a **mode selector**: `Local`, `AzureDev`, `AzureProd`.
   - Switching requires explicit confirmation and clearly states what needs restart (WebApp/Worker/Migrations).
   - Restart buttons must be available (with audit logging) because switching storage/SQL requires restarts.

### Phase C – UI Vertical Slices (Day 3-6)

1. **Tenants & Users**
   - Migrate and supersede the current WebApp dev pages:
     - `/dev/tenant-manager` → tenant list + safe delete/reset tools (SuperAdmin).
     - `/dev/admin-roles` → internal role management (SuperAdmin, allowlisted only).
2. **Operational insights**
   - Mailbox/job views and queue depth summaries.
   - Audit log browser and export (internal evidence packs).
3. **Business dashboard**
   - Early: counts, growth deltas, storage usage, active tenants/users.
   - Later: funnel + costs/profit (Application Insights + Azure cost APIs).

### Phase D – Cross-Cutting Concerns (Day 6-7)

- **Auditing:** every admin action must emit an audit entry (internal “platform audit” stream).
- **Error handling & alerts:** surface clear banners and durable status badges.
- **Docs & Tests:** keep docs aligned, add role-gating tests, and remove/retire redundant WebApp dev pages once AdminApp covers them.

---

## 5. Security Checklist

- [ ] OAuth-only login (Google + Microsoft); no password auth.
- [ ] Allowlist enforced (`AllowedEmails`, `AllowedDomains`) and logged on denial.
- [ ] All pages require `SuperAdmin` (AdminApp is Evermail-only).
- [ ] Admin actions audited (who/what/when + correlation id).
- [ ] CSRF protection enabled for any state-changing actions.
- [ ] Production AdminApp operates **prod only** (no cross-environment switching from a prod deployment).

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

