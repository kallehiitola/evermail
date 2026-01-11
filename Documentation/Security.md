# Evermail - Security Documentation

## Security Overview

Evermail handles sensitive email data and must maintain the highest security standards. This document outlines our security architecture, threat model, and compliance measures.

### Security Modes (3 levels) & “How much do you trust Evermail?”

Evermail will support **three security levels** so customers can choose the trade-off between **service quality (search/AI/features)** and **cryptographic isolation (who can ever access plaintext)**.

> **Important accuracy note (current code vs target design)**  
> Some parts of the current UI run as `InteractiveServer`, which means **JS interop + form fields can flow to the server**. The specs below describe the **intended guarantees** for each level and the **implementation rules required** to make those guarantees true. Until those rules are enforced in code, we must not market stronger guarantees than we actually provide.

#### Level 1 — Full Service (Maximum capability)

- **Promise**: Best search and UX. Evermail provides ingestion, per-email viewing, attachments, full-text search, and AI features.
- **Key reality**: Evermail services **can decrypt** during processing (with strict access controls, audit logs, and least privilege).
- **Who it’s for**: Most users; “I want it to just work.”

#### Level 2 — Confidential Processing (Strong isolation + full search)

- **Promise**: Evermail can provide near-full capabilities, but **decryption only happens inside attested confidential compute** (TEE) and is gated by **Secure Key Release (SKR)** and tenant key policy. Every unwrap is audited.
- **Key reality**: This meaningfully reduces operator access in normal operation, but it is still not the same as “a malicious SaaS operator can never ship malicious code.”
- **Who it’s for**: Regulated customers who want full search + strong operational controls.
 - **Key sources supported**: Confidential Processing can support **both**:
   - **Evermail-managed keys** (keys are controlled by Evermail, but *released only to TEEs* under SKR policies and audited), and/or
   - **Customer-managed keys (BYOK)** via Azure Key Vault / AWS KMS (policy-gated and audited).

#### Level 3 — Zero-Access (Strictest: Evermail stores ciphertext only)

- **Promise**: Evermail infrastructure **never receives mailbox plaintext keys** for this mailbox, and therefore cannot decrypt the mailbox ciphertext using DB + blob access.
- **Capability trade-off**: Server-side ingestion/search/AI over message bodies is not possible without keys. Search is either:
  - **client-side** (recommended: local index in the browser), and/or
  - **token-based filtering** (deterministic tokens for selected fields).
- **Who it’s for**: Compliance-driven customers willing to trade capability for strict key custody.

#### Security level scope (tenant default vs per-mailbox)

Recommended model:
- Tenants choose a **default security level** during onboarding.
- Each upload can **override** the default (i.e., security level is effectively **per-mailbox**).
  - Rationale: teams often need “Full Service” for day-to-day and “Zero-Access” for a subset of archives.

#### Implementation requirements per level (server vs browser)

This is the non-negotiable rule set that enforces the guarantees:

##### Level 1 (Full Service)
- Allowed: server-side parsing, server-side indexing, attachment extraction, full text search (SQL FTS), AI features.
- Required: encryption at rest (TDE/SSE), TLS, least-privilege identities, audit logs for sensitive actions.

##### Level 2 (Confidential Processing)
- Required: any component that decrypts mailbox content must run in **confidential compute** and obtain keys via **SKR** (or equivalent strong release policy).
- Required: “normal” WebApp/UI must not hold plaintext DEKs in memory longer than necessary; keys are released to the worker only.
- Required: append-only evidence (ledger or immutable logs) for key release events.

##### Level 3 (Zero-Access)
- Required: mailbox keys/passphrases **must never cross the browser → server boundary**.
  - No `InteractiveServer` pages/components may generate keys, accept passphrases, or receive keys through JS interop.
  - No JS interop calls may return plaintext key material to .NET on the server.
  - No API endpoint may accept a mailbox plaintext key or a passphrase that can unwrap it.
- Required: server treats the uploaded blob as **opaque ciphertext**. No ingestion worker parses it.
- Allowed: server stores **opaque deterministic tokens** (HMAC outputs) to enable filtering without plaintext.

> This implies that Zero-Access flows must be implemented as **Interactive WebAssembly (client-only)** UX for key generation, encryption, and client-side search.

#### World-class BYOK onboarding (UX spec)

This section defines the **tenant-facing onboarding experience** for key management and privacy modes. It must satisfy:
- **Non-technical usability** (no scripts/CLI/JSON handoffs)
- **Clear, honest guarantees** per security level
- **Guarded recovery bundle flow** (acknowledgement gating)
- **Fast success path** (trial users can be “searching in minutes”)

##### Onboarding step: “Choose your security level”

Show 3 cards with a one-line promise, 3 bullets, and a “What you give up” disclosure link:

1. **Full Service (recommended for most)**  
   - *Promise*: “Best search and viewing—Evermail does the heavy lifting.”  
   - Bullets: “Full-text search”, “Attachment preview”, “AI features (plan dependent)”  
   - Disclosure: “Evermail services can decrypt during processing (audited, access controlled).”
   - CTA: “Use Full Service”

2. **Confidential Processing (strong isolation + full search)**  
   - *Promise*: “Decrypt only inside attested secure compute.”  
   - Bullets: “BYOK supported”, “SKR-gated key release”, “Full search and ingestion”  
   - Disclosure: “Requires key setup; may take ~10–20 minutes.”
   - CTA: “Set up Confidential”

3. **Zero-Access (ciphertext-only)**  
   - *Promise*: “Evermail never receives your mailbox key.”  
   - Bullets: “Client-side encryption”, “Local-only full search”, “Optional token filtering”  
   - Disclosure: “Lose your recovery bundle/passphrase and the data is unrecoverable.”
   - CTA: “Use Zero-Access”

Persist the choice as tenant default via the API (`PUT /api/v1/tenants/security-level`).

##### Onboarding step: “Keys & recovery”

This step adapts to the selected security level:

**Full Service**:
- Status message: “Keys are handled by Evermail. You can switch to BYOK later.”
- Optional toggle: “Require admin approval for exports/deletions” (policy-only guardrails).

**Confidential Processing**:
- Wizard-style checklist with **button-only** actions:
  1. “Connect your key provider” (Azure Key Vault / AWS KMS / Offline provider)
  2. “Test access” (`POST /api/v1/tenants/encryption/test`)
  3. “Enable Secure Key Release” (guided UI that generates the policy, shows required identity IDs, and validates policy presence via a “Check” button)
- If the tenant selects **Evermail-managed keys**, step (1) becomes “Confirm Evermail-managed keys” and the wizard focuses on the SKR/attestation readiness checks.
- UX principles:
  - Always show a single “Status: Ready / Not ready” pill.
  - Every failure should produce an actionable, copyable error (permission missing, wrong URI, etc.).

**Zero-Access**:
- Provide a “Generate recovery bundle” button that runs entirely client-side.
- Require explicit acknowledgement before revealing or downloading bundle material:
  - Checkbox: “I understand Evermail cannot recover this bundle or passphrase.”
  - Only after checked: enable “Download bundle” + “Copy key” actions.
- Provide a second checkbox before leaving the step:
  - “I saved the bundle offline.”
  - This is a UX guardrail (not cryptographic) but prevents accidental data loss.

##### Zero-Access upload UX (world-class “idiot-proof” flow)

On `/upload` when Zero-Access is selected:
- Default to **client-only upload experience** (WASM). Do not expose plaintext keys to any server-rendered component.
- Upload step layout:
  1. “Choose file”
  2. “Generate key” (auto-generated, but show the recovery bundle gate)
  3. “Upload encrypted” (progress + cancel)
  4. “Build local index” (optional; recommended)
- Post-upload:
  - Show “Open mailbox locally” CTA which opens the client-side viewer/search experience.
  - Clearly label that server-side email detail pages are not available for Zero-Access mailboxes.

#### Zero-Access Archive Mode (Client-Side Encryption) — target design

Zero-Access is intentionally optimized for non-technical users: the browser generates a random mailbox key and Evermail does not receive it.

1. **Key generation & custody**
   - The upload page generates a random 256-bit AES-GCM key in the browser (WebCrypto).
   - The UI shows the Base64 key + a SHA-256 fingerprint and offers a one-click “Download bundle” button.
   - **Guarantee for Level 3**: Evermail never receives the key. If the tenant loses it, the ciphertext is not decryptable.

2. **Client encryption pipeline**
   - The browser reads the selected file in **4 MiB chunks** and encrypts each chunk with AES‑GCM.
   - Each uploaded block payload is: `nonce (12 bytes) || ciphertext || tag` where:
     - Nonce = `noncePrefix (8 random bytes) || chunkIndex (uint32 big-endian)`
     - Tag length = 16 bytes (AES‑GCM default)
   - The client calls:
     1. `POST /api/v1/upload/encrypted/initiate` (alias: `POST /api/v1/mailboxes/encrypted-upload/initiate`) → returns SAS upload URL + `tokenSalt`.
     2. Uploads ciphertext blocks directly to Azure Blob Storage via the SAS URL (no plaintext hits server disk).
     3. `POST /api/v1/upload/encrypted/complete` (alias: `POST /api/v1/mailboxes/encrypted-upload/complete`) → stores metadata + hashed token sets and marks the mailbox as `Encrypted`.

3. **Server-side handling**
   - The WebApp treats the uploaded blob as **opaque ciphertext** and does not parse it.
   - The mailbox/upload rows are finalized as:
     - `SourceFormat = client-encrypted`
     - `Status = Encrypted`
     - `EncryptionMetadataJson` persisted (chunk sizes, nonce prefix, fingerprint, etc.)
   - The ingestion worker is **not queued** for these uploads.
   - Deterministic tokens are persisted to `ZeroAccessMailboxTokens` (`TenantId`, `MailboxId`, `TokenType`, `TokenValue`) to enable filtering without storing plaintext labels.

4. **Search & UX (Level 3)**
   - Default: users search in the browser using a **local index** (stored in IndexedDB) built from decrypted content.
   - Optional: deterministic token indexing allows server-side filtering (e.g., “from:alice”) without plaintext.
   - Email viewing (body + attachments) happens client-side after decryption.
   - UI surfaces clear warnings:
     - “Lose your passphrase = data unrecoverable.”
     - “Some features (AI summaries, cross-mailbox search) are unavailable in zero-access mode.”
     - “Switching a mailbox into zero-access requires re-upload because plaintext never leaves your device.”

5. **Transparency & trust (Option A)**
   - Publish build/version hashes for the client bundle to reduce “accidental trust drift.”
   - Long-term roadmap: integrate browser extensions or attestation (e.g., Subresource Integrity + CSP) to prove the delivered client matches the audited build.

##### Encrypted upload contract (`/api/v1/upload/encrypted/*` + alias under `/api/v1/mailboxes/encrypted-upload/*`)

| Endpoint | Purpose |
| --- | --- |
| `POST /initiate` | Validates plan limits, creates/updates the mailbox, allocates a SAS URL, and returns `tokenSalt` so the client can derive deterministic token keys without sharing the DEK. |
| `POST /complete` | Marks the upload as `Encrypted`, persists encryption metadata (`scheme`, nonce prefix, fingerprint), stores ciphertext stats, and ingests the hashed token sets supplied by the browser. |

Completion payload fields:
- `scheme` – semantic version of the client encryptor (`zero-access/aes-gcm-chunked/v1`).
- `metadataJson` – per-chunk sizes, nonce prefix, tag length, timestamps.
- `keyFingerprint` – SHA-256 hash of the raw DEK (base64) so tenants can map bundles to uploads.
- `tokenSets` – array of `{ "tokenType": "tag", "tokens": ["HMAC-SHA256(base64)", ...] }`. The server never sees plaintext tags; it simply stores the opaque HMACs.

##### Deterministic tokens – Phase 1 (mailbox tags) (implemented)

- Scope: user-entered tags (e.g., “Case-4821”, “AcmeBeta”) captured during upload.
- Derivation: the browser derives an HMAC key via HKDF using the **raw mailbox key** and server-issued `tokenSalt` (HKDF info string `evermail-zero-access-token/v1`), then HMACs each normalized tag with SHA-256. Only the HMAC outputs are sent to the server.
- Storage: `ZeroAccessMailboxTokens` ensures multi-tenant isolation and deduplicates repeated values.
- Querying: clients hash the search tag with the same token key and call `GET /api/v1/mailboxes?tagToken=<base64>` to limit responses to matching mailboxes. Once the client downloads/decrypts the archive it performs full-text search locally.
- Future phases will extend the same mechanism to per-email fields (from/to/subject tokens) as the WASM parser graduates to full message-level extraction.

##### Deterministic tokens – Phase 2 (header indexes) (implemented for .mbox/.mbx)

- **Scope**: the upload UI now derives deterministic tokens for the most common per-message headers—`from`, `to`, `cc`, and `subject`—in addition to the manual tag list. This lets tenants filter their encrypted mailboxes without downloading gigabytes of ciphertext just to find “all mail from alice@example.com”.
- **Client-side parsing**: `zero-access-upload.js` streams the `.mbox/.mbx` file in the browser, detects message boundaries (`From ` lines), parses/unfolds RFC 5322 headers, and extracts `from`/`to`/`cc` address tokens plus a normalized `subject`.
- **Normalization rules (current code)**:
  - Addresses → extracted via regex, lowercased, trimmed.
  - Subjects → lowercased, whitespace collapsed, trimmed, truncated to 200 chars.
- **Resource limits**: (a) inspect up to 2,000 messages, (b) cap each token type at 512 unique values, (c) deduplicate in-memory before hashing.
- **Derivation & storage**: once the plaintext sets are assembled the browser runs the same HKDF + HMAC flow as Phase 1, but now emits token sets such as `{ tokenType: "from", tokens: [...] }`. The WebApp persists them in `ZeroAccessMailboxTokens` alongside the legacy `tag` entries so the database schema does not change.
- **Query surface**: `/api/v1/mailboxes` accepts `fromToken`, `toToken`, and `subjectToken` query parameters (multi-value). Clients hash the user-entered value with the token key and supply it as `fromToken=base64` to pre-filter encrypted mailboxes.
- **UX**: the encrypted upload wizard now shows a “Header indexing” callout that reports how many unique senders/recipients/subjects were captured (or why it was skipped). Future client-side search views will reuse the same hashing helper to keep the UX consistent.

##### Multi-admin BYOK bundle registry

- New entity `TenantEncryptionBundle` stores *wrapped* DEKs (never plaintext) per admin with labels, checksums, and timestamps so multiple administrators can maintain their own recovery bundles.
- Admin endpoints:
  - `GET /api/v1/tenants/encryption/bundles` – list bundles for the tenant (label, createdBy, lastUsed).
  - `POST /api/v1/tenants/encryption/bundles` – upload/import an additional bundle (same JSON produced by the Offline BYOK lab).
  - `DELETE /api/v1/tenants/encryption/bundles/{id}` – revoke a stale bundle.
- UI updates:
  - Offline BYOK components now show the existing bundle inventory, including who generated it and when it was last downloaded.
  - Admins can regenerate bundles for themselves without overwriting other admins’ copies, and they can re-download a wrapped bundle directly from the UI (still client-side decrypted with their passphrase).

Zero-Access Archive Mode inherits every safeguard from Confidential Compute Mode (tenant TMKs, audit logging, retention policies) while guaranteeing Evermail’s infrastructure never observes plaintext. This tier is aimed at compliance-sensitive customers who accept reduced functionality in exchange for cryptographic separation.

### HTTP Security Middleware

All HTTP responses now flow through a dedicated `SecurityHeadersMiddleware` so we consistently emit modern browser safeguards (and avoid forgetting them when we add new endpoints):

- `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload` (production-only) forces HTTPS for one year so downgraded HTTP requests are rejected by the browser before they ever leave the device.
- `Content-Security-Policy: default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self' data:; connect-src 'self' https://localhost:* wss://localhost:*` (dev mode allows localhost sockets for hot-reload) drastically reduces XSS attack surface by disallowing third-party scripts/styles.
- `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy: geolocation=(), camera=(), microphone=(), clipboard-read=(), clipboard-write=()` and `X-XSS-Protection: 1; mode=block` cover clickjacking, MIME sniffing, referrer leakage, unnecessary device APIs, and legacy IE XSS protections.

Because Blazor’s hot reload still injects inline styles/scripts in development, the middleware automatically relaxes CSP only when `IHostEnvironment.IsDevelopment()` is true; production builds remain on the strict policy above.

### Audit Logging

Every tenant-facing API call that mutates data must leave a durable audit trail. The new `AuditLoggingMiddleware` and `IAuditLogger` service enforce this requirement automatically:

- Middleware scope: all authenticated `POST`, `PUT`, `PATCH`, and `DELETE` requests under `/api/v1` (except `/api/v1/dev/*`) are recorded once the downstream pipeline finishes.
- Captured fields: tenant ID, user ID (when available), HTTP method + route template, response status code, remote IP (v4/v6), and the reported `User-Agent`. The log fits inside the existing `AuditLogs` table (`Action`, `ResourceType`, `Details`, etc.) so no schema change is necessary.
- Multi-tenancy: if the current `TenantContext` is empty (e.g., during anonymous login calls) the logger silently skips writing to avoid corrupting data with `Guid.Empty`.
- Extensibility: endpoints can inject `IAuditLogger` directly when they need to add richer `Details` payloads (JSON snippets, counts, etc.), but the middleware already covers the baseline trail for sensitive operations such as mailbox deletes, user management, or encryption updates.

These logs power compliance exports (“show me who deleted mailbox X”), anomaly detection (sudden surge of DELETEs from a new IP), and the GDPR “record of processing activities” requirement logged earlier in this doc.

#### Implementation Status (November 2025)

- ✅ **Delivered**: Offline BYOK lab (browser-wrapped bundles + `/api/v1/tenants/encryption/offline`), archive format detection, plan-aware normalization, security middleware (HSTS/CSP/X-Frame/X-Content), baseline audit logging, rate limiting, GDPR self-service APIs, zero-access encrypted upload contract (`/api/v1/mailboxes/encrypted-upload/*`), deterministic tag tokens, and the multi-admin BYOK bundle registry/UI.
- 🚧 **In progress**: Deterministic token expansion to per-email metadata, deterministic download manifests for chunk-level streaming, and the Confidential Compute/Secure Key Release rollout (Phase 1). The audit log UX/export surface is also pending.
- 📝 **Tracked follow-ups**: Stripe integration, encrypted search UX polish, and audit log reporting remain in the `Next Steps` section of `Documentation/ProgressReports/ProgressReport.md`.

##### Compliance console (audit trail UX + exports)

To make the existing audit pipeline actionable we are adding a tenant-facing **Compliance Console** inside the admin area:

1. **Timeline grid** – A MudBlazor data grid lists `AuditLog` entries with virtualized scrolling. Filters on the left let admins scope by date range, action (`MailboxDeleted`, `GdprExportRequested`, etc.), actor (user), and resource type. Badges call out anomalies (e.g., >10 destructive actions inside 5 minutes) using simple heuristics computed client-side.
2. **Inline details** – Selecting a row shows a drawer with the structured metadata we store today (IP address, user agent, `Details` JSON rendered as a key/value list). Nothing editable—read-only transparency only.
3. **CSV export** – A “Download filtered CSV” button calls the new `/api/v1/audit/logs/export` endpoint with the currently selected filters. The export runs server-side, streams a short-lived CSV (max 10 k rows), and is automatically audited as `AuditLogCsvExported`. No manual CLI or SQL access required.
4. **GDPR job monitor** – The same page surfaces `UserDataExports` and `UserDeletionJobs` history so compliance teams can prove when a user-requested export/deletion completed. Cards show status chips, start/end timestamps, and the blob download links (still protected by SAS tokens).
5. **Zero-CLI UX** – All interactions are button-only. The UI never exposes the raw CSV URL; instead it triggers a browser file download directly from the authenticated API response. If a recovery bundle or hash needs to be shown we reuse the guarded pattern (acknowledgement checkbox + persistent warning).

Administrators can now satisfy “who touched what” audits, deliver CSV evidence packs, and monitor GDPR jobs without Evermail staff involvement, reinforcing the zero-trust posture described throughout this document.

##### Offline key custody prototype (Browser BYOK) — current behavior vs target

Phase 1 introduces a lightweight **Offline BYOK Lab** so admins can experiment with client-side key handling before enabling full zero-access ingestion:

1. Navigate to **Admin → Offline BYOK (Lab)**.
2. Enter a tenant label and a strong passphrase (minimum 12 chars).
3. The Blazor WebAssembly client calls `window.crypto.subtle` to:
   - Generate a 256-bit DEK (`crypto.getRandomValues`).
   - Derive a wrapping key from the passphrase via PBKDF2 (SHA-256, 310 k iterations, 128-bit salt).
   - Wrap the DEK with AES-GCM (unique nonce) and compute a SHA-256 checksum of the plaintext key.
4. The UI displays:
   - **Plaintext DEK** (copy once, never stored by Evermail).
   - **Wrapped bundle JSON** containing `wrappedDek`, `salt`, `nonce`, `checksum`, `tenantLabel`, and `createdAt`.
5. **Current**: The UI can POST the wrapped bundle **plus the passphrase** to `/api/v1/tenants/encryption/offline` so the server can unwrap it once and store `OfflineMasterKeyCiphertext` protected by `OfflineByok:MasterKey`.  
   **Implication**: This enables an “offline provider” for server-side processing, but it does **not** satisfy Level 3 Zero-Access, because passphrases/keys cross to the server.

5. **Target for Level 3**: Recovery bundle creation and storage are **client-only**:
   - Evermail may store an *opaque* bundle registry entry (wrapped payload) for inventory, but must never receive a passphrase or any unwrapping material.
   - Any “recovery” flow must happen in the browser with explicit user acknowledgement.
6. Existing bundles can be imported later from the same admin page—the new “Upload bundle” card lets an admin pick the `.evermail-key.json`, enter the passphrase, and call the same API without re-generating keys.

**Warnings surfaced in the UI and reiterated here:**
- Lose the downloaded bundle/passphrase → the UI cannot help you recreate the original bundle (treat it as a recovery artifact).
- Note: the service retains an encrypted copy of the unwrapped offline key under `OfflineByok:MasterKey`, so “recoverability” is a product/policy decision rather than a cryptographic impossibility in the Offline provider.
- Evermail only stores the DEK encrypted with the Offline BYOK master key provided by operators. Compromise of the SQL row does **not** reveal the tenant key unless the operator also compromises the host-level protector.
- The master key itself lives in Azure Key Vault as `OfflineByok--MasterKey` (base64-encoded 256-bit). During deployment we mirror it into the ingestion worker via `offline-master`/`OfflineByok__MasterKey=secretref:offline-master` so Confidential Container Apps can unwrap tenant bundles without keeping plaintext keys on disk.
- Do **not** paste the plaintext key anywhere else—treat it like a hardware token.
- Bundle format is versioned (`"version": "offline-byok/v1"`); future releases will maintain backward compatibility or provide a migration tool.

**Service-side controls**
- `OfflineByok:MasterKey` must be a base64-encoded 256-bit secret shared by the WebApp and the ingestion worker. Set it via user secrets or Key Vault before enabling BYOK; the protector refuses to start without it.
- `TenantEncryptionSettings.Provider = "Offline"` indicates the tenant is running on their own DEK. The ingestion worker unwraps mailbox DEKs by decrypting `OfflineMasterKeyCiphertext` with the protector, wrapping the per-mailbox keys with AES-GCM, and zeroing buffers after each operation.

Upcoming milestones:
- Attach the browser-wrapped DEK to mailbox uploads via metadata headers.
- Offer deterministic token derivation so server-side filtering remains possible.
- Allow tenants to register multiple offline bundles (per admin) with recovery workflows.

##### Onboarding security choices (Quick Start vs BYOK)

The onboarding wizard now walks every new tenant through a friendly, marketing-style comparison of the two supported encryption paths:

- **Evermail-managed (Fast Start)**  
  - Copy highlights speed: “Be searching in minutes.”  
  - Single click provisions the built-in `EvermailManaged` provider via `UpsertTenantEncryptionSettings`, completing the security step instantly for trials, demos, or debugging.  
  - We surface the trade-off transparently: Evermail operators could still decrypt during normal operations, so compliance teams should graduate to BYOK before production.  
  - Progress tracking marks this step complete because `Tenant.EncryptionSettings.Provider = EvermailManaged` yields `EncryptionConfigured = true`.

- **Customer-managed key (BYOK)**  
  - Copy focuses on control: “Your key, your rules.”  
  - Selecting this option sets `Tenant.SecurityPreference = "BYOK"` and exposes two paths:
    1. Inline **Offline BYOK Lab** component (reusing `offlineByok.js`) so admins can generate a wrapped DEK bundle entirely in the browser today.  
    2. Links to `/admin/encryption?onboarding=1` where they can plug in Azure Key Vault or AWS KMS credentials when ready.
  - We remind users that the step only completes once the BYOK settings validate successfully (`EncryptionConfigured = true`), even if they’ve downloaded a prototype bundle.

The hero banner shows which identity provider (Google or Microsoft) the current admin used to sign in so they understand which account controls the workspace keys. All copy stays upbeat (“Need maximum assurances? Flip on BYOK whenever security asks.”) while clearly listing the trade-offs in the wizard cards.

#### External KMS Providers (AWS First)

Some tenants already operate their own Hardware Security Modules in clouds other than Azure. Evermail supports this as an advanced option without requiring admins to understand the underlying crypto:

1. **Provider picker** – Admin UI exposes three options: Evermail-managed (default), Azure BYOK, or External KMS. Selecting External prompts for provider (AWS today), key ARN, and IAM role ARN.
2. **AWS KMS connector** – Evermail assumes a customer-provided role via AWS STS, then uses `GenerateDataKeyWithoutPlaintext` / `Decrypt` to wrap and unwrap DEKs. The connector enforces:
   - Least-privilege IAM template (Encrypt/Decrypt/GenerateDataKeyWithoutPlaintext only).
   - Rate limiting + exponential backoff to respect tenant KMS quotas.
   - Structured audit logs capturing AWS request IDs for every operation.
3. **Security guarantees** – Because AWS KMS lacks Azure’s Secure Key Release + TEE attestation, the “Confidential Compute Mode” promise becomes “operators cannot access plaintext during normal operations,” but a malicious Evermail release could theoretically abuse the IAM role. Tenants who need absolute zero-access should pair External KMS with Zero-Access Archive Mode so plaintext never leaves the browser.
4. **Future providers** – The same connector pattern (provider picker + automation script + signed requests) lets us add GCP KMS or on-prem HSMs later without reworking ingestion.

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
- `Admin`: Tenant admin (manage users, view billing, configure BYOK). The very first user created for a tenant is promoted to `Admin` automatically so every tenant starts with at least one administrator. Additional admins can be added/removed from `/settings/users`.
- `SuperAdmin`: Platform admin (view all tenants, system config, AdminApp access). Only SuperAdmins can reach the standalone AdminApp; tenant admins are limited to the in-tenant Blazor admin pages.

#### Evermail AdminApp (internal SuperAdmin portal)
- **Audience**: Evermail staff only (never exposed to customers).
- **Auth**: OAuth-only (Google + Microsoft), no password auth.
- **Access control**: allowlist enforced on OAuth callback:
  - Allowed email(s): `kalle.hiitola@gmail.com`
  - Allowed domain(s): `evermail.ai`
- **Environment guardrail**: when deployed to production the AdminApp operates **prod only** (no cross-environment switching UI).
- **Operational safety**: runtime switches (Local / AzureDev / AzureProd) require explicit confirmations and emit audit events for every change and restart.

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

### Confidential Content Protection (Zero-Trust Mode)

Default infrastructure encryption (TDE/SSE) keeps attackers out of the storage layer, but it does not stop privileged operators from querying the database. The **Confidential Content Protection** program introduces an envelope encryption model plus hardware-backed attestation so that even SuperAdmins cannot read tenant data.

#### Goals
- Tenant data is unreadable outside a Trusted Execution Environment (TEE).
- Tenants may bring their own master keys; Evermail never handles plaintext keys at rest.
- Search, analytics, and upcoming AI features continue to work with minimal latency impact.
- Every decrypt/unwrap action leaves an immutable audit trail.

#### Key Hierarchy
1. **Tenant Master Key (TMK)** – Asymmetric key stored in the tenant’s own Azure Key Vault or Managed HSM. Evermail keeps only the key identifier.
2. **Mailbox Data Encryption Key (DEK)** – Random 256-bit AES-GCM key generated when a mailbox (or upload) is created.
3. **Wrapped DEK** – The DEK is wrapped with the TMK (using `Encrypt`/`UnwrapKey` operations). Only the wrapped value is stored alongside mailbox metadata.

```mermaid
flowchart TD
    TenantKey[Customer Key Vault<br/>Tenant Master Key] -->|Unwrap allowed only for attested workload| WorkerTEE
    WorkerTEE[Confidential Worker] -->|uses| DEK[Mailbox DEK (AES-256-GCM)]
    DEK -->|encrypt/decrypt| Payload[(Emails, Attachments, Search Tokens)]
```

#### Confidential Workloads
- Move ingestion, search, and AI microservices into **Azure Confidential Container Apps** or **AMD SEV-SNP VMs**.
- Register each workload’s attestation policy with the tenant TMK (Key Vault “key release” policy). Key Vault refuses to unwrap unless the workload presents a valid attestation report for the signed container image.
- Control plane APIs (dashboard, billing, monitoring) keep running in standard containers but never handle plaintext.

> **Current deployment (2025-11-25)**  
> Azure has not yet exposed the confidential workload profile for Container Apps in West/North Europe. Until it does, the ingestion worker runs on a standard Dedicated Container Apps environment in West Europe (`evermail-conf-worker`). Production is locked down with PIM-only access, no shell/exec, exhaustive logging, and automatic secret rotation. Development environments may keep relaxed settings (ssh/exec allowed, debugging symbols, `EnsureHttpClientBaseAddress` disabled) to speed up troubleshooting, but they never process real tenant data.
>
> When Microsoft lights up the confidential profile we will:
> 1. Recreate the Container Apps environment with the confidential workload profile (same VNet/subnet), or switch to AKS confidential node pools if Container Apps still lags.
> 2. Update the Secure Key Release policy from the placeholder `allowEvermailOps` claims to the real Microsoft Azure Attestation attributes for the signed image.
> 3. Redeploy the worker, validate attestation hashes, rotate managed identity credentials, and flip Key Vault to “TEE-only”.
> 4. Ship a customer update + changelog entry stating that plaintext is now confined to attested TEEs.

#### Encryption & Search Pipeline
1. Queue message arrives with tenant + mailbox identifiers.
2. Worker in TEE requests `UnwrapKey(TMK, wrappedDek)`; attestation proof is automatically validated by Key Vault.
3. Worker decrypts MIME payloads, attachments, and derived search tokens inside enclave memory only.
4. Search indexes store **deterministically encrypted tokens** (AES-SIV with tenant-specific salt). SQL Server FTS never sees plaintext, but deterministic encryption allows equality lookup for token matches.
5. Snippets/results returned to the API are immediately re-encrypted with the DEK before leaving the enclave; the Blazor client decrypts on demand using short-lived per-session keys (see “Client-Assisted Privacy” below) or receives plaintext over TLS if the tenant opts out.

```csharp
// Pseudocode inside the enclave
var dek = await _keyUnwrapper.GetDekAsync(mailboxId, cancellation);
using var aesGcm = new AesGcm(dek);
aesGcm.Encrypt(nonce, plaintextBody, ciphertextBody, authTag);
var token = DeterministicEncrypt(dek, Normalize(searchTerm));
await _emailStore.SaveAsync(ciphertextBody, token, ...);
```

#### Client-Assisted Privacy (Paranoid Tier)
- Tenants can require an additional passphrase-derived key (Argon2id) that double-wraps each DEK. Workers receive the passphrase share via a temporary token that expires after the job completes.
- For manual decrypt/export flows, provide an open-source CLI that uses the tenant’s passphrase + wrapped DEKs to decrypt offline. Evermail never stores or recovers the passphrase share.

#### Audit & Monitoring
- Every `UnwrapKey` call is logged by Key Vault and mirrored into an **Azure Confidential Ledger** stream.
- Workers emit structured `AuditLogs` entries (`DekUnwrapped`, `AttachmentDecrypted`, `AiSummaryGenerated`) with attestation claims, workload version, and purpose.
- Alerts fire if a tenant’s TMK releases more than N keys per hour or if an enclave hash changes.

#### Phased delivery plan

| Phase | Deliverables | Key Microsoft references |
| --- | --- | --- |
| **Phase 1 – BYOK enrollment (MVP)** | Tenant onboarding wizard provisions or links an Azure Key Vault/Managed HSM key, marks it exportable, uploads the public portion, and grants Evermail’s managed identity `release` permission. Queue/worker schema now stores `WrappedDekId`, rotation metadata, and proof that Secure Key Release policy JSON has been staged. Plaintext still executes in standard Container Apps, so documentation and contracts explicitly note that superadmins retain break-glass visibility until Phase 2 finishes. | [Secret & key management for confidential computing](https://learn.microsoft.com/en-us/azure/confidential-computing/secret-key-management) |
| **Phase 2 – Attested TEEs** | Ingestion/search/AI workers are redeployed to Azure Confidential Container Apps or AKS confidential node pools ([deployment models](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-computing-deployment-models), [confidential containers overview](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-containers)). Secure Key Release policies require Microsoft Azure Attestation (MAA) claims that match each container image ([SKR + attestation workflow](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation)). Key Vault will now refuse to unwrap DEKs for any context outside the signed TEE, which gives us the “we can’t read your mail” guarantee. |

##### Phase 1 – Secure Key Release onboarding flow

**Goal:** Before we migrate workers into TEEs we still need per-tenant Secure Key Release (SKR) policies so that the move to attested workloads is a switch-flip instead of a re-onboarding exercise. Phase 1 therefore focuses on capturing, validating, and auditing SKR JSON even though the policy currently references a permissive attestation placeholder (`allowEvermailOps`). Once TEEs are deployed we only need to swap the placeholder claims with Microsoft Azure Attestation (MAA) evidence.

**Implementation summary**

1. **New tenant endpoints**
   - `GET /api/v1/tenants/encryption/secure-key-release/template` returns a signed JSON template with the required `anyOf/allOf` grammar, the tenant’s managed-identity object ID, and placeholder attestation claims. The admin UI calls this endpoint behind a single button so tenants never have to edit JSON themselves.
   - `POST /api/v1/tenants/encryption/secure-key-release` accepts the completed policy JSON plus an optional `attestationProvider` label. The backend validates the payload with `JsonDocument.Parse`, re-serializes it with canonical indentation, computes a SHA-256 hash, and stores both the JSON and the hash inside `TenantEncryptionSettings`.
   - `GET /api/v1/tenants/encryption/secure-key-release` returns the stored JSON (redacted to the current tenant) so admins can review/edit in the UI.
   - `DELETE /api/v1/tenants/encryption/secure-key-release` resets the policy if a tenant wants to rotate claims or switch providers.

2. **Data model hooks**
   - `TenantEncryptionSettings` already exposes `SecureKeyReleasePolicyJson`, `SecureKeyReleasePolicyHash`, `IsSecureKeyReleaseConfigured`, `SecureKeyReleaseConfiguredAt`, and `SecureKeyReleaseConfiguredByUserId`. Phase 1 now actively maintains those fields whenever an admin uploads or resets SKR JSON.
   - `MailboxEncryptionState` entries record the `ProviderKeyVersion`, `WrapRequestId`, and `LastUnwrapRequestId` for every DEK lifecycle so later compliance exports can prove that only policies with staged SKR JSON produced ciphertext.

3. **UI + onboarding guardrail**
   - `/admin/encryption` now contains a “Secure Key Release policy” card with an editor, template loader, and status chips that surface the current hash + timestamp.
   - `OnboardingStatusCalculator` requires SKR to be configured before it treats Azure Key Vault or AWS KMS providers as “done”. This keeps the wizard from marking encryption complete until both the key metadata and the placeholder SKR policy exist.
   - **No CLI/JSON handoffs** – Tenants never see raw JSON or command snippets. The admin portal provides “generate & apply” buttons that call the SKR endpoints directly and only surfaces read-only status (hash, timestamp) for transparency.

4. **Auditability**
   - Every `POST/DELETE` call runs through `AuditLoggingMiddleware` and captures the tenant, user, and endpoint path.
   - The SHA-256 hash is included in the `TenantEncryptionSettingsDto.SecureKeyRelease` payload so administrators can confirm which policy revision is active without exposing the whole JSON blob.

Once a tenant uploads their policy, Aspire propagates it via configuration to the ingestion worker and future confidential containers. When we implement Phase 2 we only need to update the attestation clauses that the template generates.

##### Phase 1 implementation tasks
1. **Tenant wizard** – Guides admins through creating/importing a RSA-HSM or EC-HSM key, setting `exportable=true`, and capturing the Key Vault URI.
2. **Policy scaffolding** – Generate SKR JSON with placeholder attestation claims (`allowEvermailOps`) so that Phase 2 can swap in real MAA measurements without revisiting every tenant.
3. **DEK rotation hooks** – Extend `MailboxProcessingService` to store `WrappedDekId`, `Version`, `CreatedBy`, and `TmKeyVersion` fields so any later unwraps can be attributed.
4. **Operational controls** – Enforce PIM on the managed identity that can call `ReleaseKey`, enable Key Vault logging, and mirror audit events into Azure Monitor.

##### Phase 2 implementation tasks
1. **Provision TEEs** – Create a dedicated resource group containing Azure Confidential Container Apps (or AKS confidential node pools) plus Microsoft Azure Attestation provider instances. Images produced by the CI pipeline are signed and their measurements recorded.
2. **Update SKR policies** – Replace placeholder claims with the real MAA attributes (for AMD SEV-SNP: `x-ms-isolation-tee.x-ms-attestation-type = sevsnpvm`, `x-ms-compliance-status = azure-compliant-cvm`). Reference: [Microsoft SKR policy grammar](https://learn.microsoft.com/en-us/azure/key-vault/keys/policy-grammar).
3. **Runtime wiring** – Workers call MAA, attach the attestation JWT to the Key Vault `Release` request, unwrap the DEK, and keep plaintext inside the enclave. Search/AI responses are re-encrypted with DEKs before returning to the API surface.
4. **Immutable logging** – Stand up Azure Confidential Ledger (≈ $3/day/ledger per [official announcement](https://techcommunity.microsoft.com/blog/azureconfidentialcomputingblog/price-reduction-and-upcoming-features-for-azure-confidential-ledger/4387491)) to capture “who unwrapped what and when” evidence.

#### Rollout Checklist
1. **Key onboarding** – Tenant uploads TMK reference inside Settings. Validate by running a dry-run attestation + unwrap test.
2. **Worker migration** – Publish ingestion/search/AI workloads as confidential containers, configure attestation policies, and rotate secrets.
3. **Data migration** – For existing mailboxes, generate DEKs, encrypt historical content in-place, and backfill wrapped DEKs.
4. **Fail-safe** – Keep a per-tenant “break glass” policy requiring multi-party approval + customer confirmation before temporarily disabling zero-trust mode (logged + time-boxed).

This model preserves usability while ensuring that legitimately paranoid customers can trust the platform: even Evermail operators cannot read their archives without the tenant-controlled keys and a verified confidential workload.

#### Operational guardrails (Phase 1)
- **Least privilege identity** – The Azure AD object that can call `ReleaseKey` is in its own PIM role (“Evermail Key Release”). Elevation requires MFA + approval; every activation is logged.
- **Mandatory logging** – Key Vault diagnostic settings stream `AuditEvent` → Log Analytics. Saved KQL alert (`KeyReleaseSpike`) fires if `count(ReleaseKey) > 5` in 10 minutes for the same tenant.
- **Repo-tracked onboarding script** – `scripts/tenant-keyvault-onboarding.ps1` provisions the Key Vault, creates the RSA-HSM TMK, and grants only the minimum permissions. Tenants can review/modify the script before running.
- **Admin UI** – `/admin/encryption` (Blazor) writes into `/api/v1/tenants/encryption`. Only Admin/SuperAdmin roles can access it. Every change writes to `TenantEncryptionSettings.LastVerifiedAt` + audit log.
- **Break-glass doc** – For Phase 1 we keep a manual decryption path, but invoking it requires: (1) customer ticket, (2) CTO approval, (3) after-action note in `Documentation/Security.md#Incident Response`. This policy is linked from the admin UI so that tenants understand the current safeguard.

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

`EmailEndpoints.SanitizeHtml` applies the same allowlist before returning `EmailDetailDto.HtmlBody`, and the Blazor client highlights keywords via `EvermailSearchHighlights`, which walks the DOM and injects `<mark class="search-hit">` spans only after the sanitized markup renders. The highlight helper never concatenates strings into the HTML payload, so existing CSP/XSS guarantees remain intact even when auto-scrolling through matches.

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

#### Multi-format archive hardening (Nov 2025 refresh)
- **Temp-file hygiene**: PST/ZIP/EML uploads are streamed into tenant-scoped random file names under `%TMP%/evermail-*`. Every extractor wraps its output in `IAsyncDisposable`, scrubbing files (secure delete) even when ingestion fails midway.
- **Format validation**: 
  - `.zip` payloads must contain at least one `.mbox`, `.pst`, `.ost`, or `.eml` entry. Anything else is rejected with a `400`.
  - `.pst` / `.ost` files run through `PstToMboxWriter` (powered by the embedded `XstReader` engine) so we validate the MS-PST structure, hydrate recipients/attachments, and emit canonical MIME that reuses the existing dedupe + attachment pipeline.
  - `.eml`/Maildir archives run through MimeKit parsing up-front, so malformed MIME can’t reach the database.
- **Automatic format detection**: `ArchiveFormatDetector` inspects the uploaded blob (headers + ZIP entries) before we ever queue ingestion. If the payload doesn’t match a supported archive type we mark the upload as failed with a friendly message and never touch the worker.
- **Plan-aware inflation guardrails**: We track the *inflated* byte count (unzipped `.mbox`, converted `.pst`) against the tenant’s plan to block “40 GB PST zipped to 2 GB” uploads instantly.
- **Client-side zero-access compatibility**: When a tenant enables the WASM encryption path, the browser runs the same detection logic, decrypts/normalizes PST or ZIP payloads locally, and only uploads ciphertext chunks—no server-side PST parsing needed. The backend still enforces size/format checks on the encrypted blobs.
- **Future offloading**: Because `ArchivePreparationService` already abstracts the normalization step, we can flip a single feature flag to bypass server conversion whenever Zero-Access mode insists that archives never leave the client in plaintext.

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
Evermail now uses the built-in **ASP.NET Core rate limiting middleware** (`System.Threading.RateLimiting`) so throttling happens before any API endpoint executes. We partition requests by tenant (when authenticated) or by client IP (anonymous flows such as `/api/v1/auth/login`) and apply tier-aware policies:

| Tier | Limit | Window | Notes |
|------|-------|--------|-------|
| Free | 100 requests | 1 hour | evaluated per tenant |
| Pro | 1,000 requests | 1 hour | evaluated per tenant |
| Team | 10,000 requests | 1 hour | evaluated per tenant |
| Enterprise | Unlimited | — | no throttling |
| Anonymous | 60 requests | 1 minute | shared per IP |

Implementation details:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, RateLimitPartitionKey>(ResolvePartition);
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = ((int)TimeSpan.FromMinutes(1).TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail("Too many requests"), cancellationToken: token);
    };
});

app.UseRateLimiter();
```

- Rate limit metadata (plan name, tenant id, IP) is embedded in every JWT (`subscription_tier` claim) so the middleware can execute synchronously without hitting the database.
- Rejections include `Retry-After`, `X-RateLimit-Limit`, and `X-RateLimit-Policy` headers, keeping client SDKs standards-compliant.
- Development-only endpoints (`/api/v1/dev/*`) remain exempt so load tests can run without touching production budgets.

### Azure Front Door (Optional)
For production, use **Azure Front Door** with WAF (Web Application Firewall):
- DDoS protection (L3/L4)
- Rate limiting per IP

### GDPR Self-Service APIs

- **Data export**  
  - Endpoint: `POST /api/v1/users/me/export` (requires auth)  
  - Generates a zipped GDPR bundle that contains:
    - `profile.json`: tenant + user metadata and plan limits
    - `mailboxes.json`, `uploads.json`: archive metadata including zero-access flags
    - `emails.ndjson`: full message bodies + headers (streamed directly from SQL Server)
    - `audit-logs.ndjson`: tenant audit entries (action, IP, user agent)
  - Archives are written to the dedicated `gdpr-exports` blob container and kept for 7 days. Clients poll `GET /api/v1/users/me/exports/{id}` and request a signed download URL (`GET .../download`) when ready.
  - Every request is logged via `AuditLoggingMiddleware` (`UserDataExportRequested` / `UserDataExportCompleted`).

- **Right to be forgotten**  
  - Endpoint: `DELETE /api/v1/users/me` (requires auth)  
  - Workflow:
    1. Soft-anonymise the Identity record (scrub email, names, phone; flip `IsActive = false`) and revoke all refresh tokens.
    2. Enqueue `MailboxDeletionQueue` jobs for every mailbox + upload so background workers purge blobs + SQL rows.
    3. Anonymise existing `AuditLogs` rows by clearing `UserId` and appending `[anonymized]` to the details column.
    4. Persist a `UserDeletionJob` audit row (`status = Completed`) so admins can show deletion receipts.
  - Response: `202 Accepted` with the deletion job id and timestamps.

- **Auditability**  
  - New tables: `UserDataExports`, `UserDeletionJobs` (both multi-tenant scoped with indexes on `TenantId` and `Status`).
  - Audit logger emits structured events (`UserDataExportRequested`, `UserDataExportCompleted`, `UserDeletionRequested`) so compliance exports can be generated later.
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

## AdminApp (Evermail SuperAdmin portal) Security Model

`Evermail.AdminApp` is an internal-only ops portal for Evermail staff. It is **not** a customer-facing tenant admin surface and is treated as a privileged control plane.

### Authentication
- **OAuth-only**: Google + Microsoft sign-in (cookie-based auth).
- **No password sign-in** for AdminApp.
- **SameSite + Safari**: OAuth flows require strict cookie handling. AdminApp uses the ASP.NET Core SameSite mitigation (`CookiePolicyOptions`) and sets OAuth correlation cookies to `SameSite=None` to avoid Safari breaking OAuth callbacks.

### Authorization
- **SuperAdmin-only policy**: all pages require the `SuperAdmin` role.
- **Allowlist enforced during OAuth sign-in**:
  - Explicit allowlist: `kalle.hiitola@gmail.com`
  - Domain allowlist: `@evermail.ai`

### Runtime switching guardrails
- Runtime switching (Local / Azure Dev / Azure Prod) is **development-only** and stored in Key Vault under `EvermailRuntime--Mode`.
- In production deployments, AdminApp is locked to **production resources only** (no cross-environment switching).

### Dev-only bypass (for UI iteration)
- A dev-only bypass endpoint exists for fast UI iteration.
- It is only enabled when:
  - `ASPNETCORE_ENVIRONMENT=Development`, and
  - `AdminAuth:DevBypassEnabled=true` (set by Aspire AppHost for local dev only).

**Last Updated**: 2025-12-16  
**Security Contact**: security@evermail.com  
**Next Security Audit**: Quarterly

