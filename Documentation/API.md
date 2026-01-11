# Evermail - API Documentation

## Overview

The Evermail API is built with **ASP.NET Core Minimal APIs** and follows REST principles. All endpoints require authentication (except public routes) and automatically enforce tenant isolation.

**Base URL**: `https://api.evermail.com/api/v1`  
**Authentication**: Bearer JWT token in `Authorization` header

## Authentication

### POST /auth/register
Register a new user and create a tenant.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "SecureP@ss123",
  "firstName": "John",
  "lastName": "Doe",
  "tenantName": "John's Archive"
}
```

**Response (201 Created)**:
```json
{
  "success": true,
  "data": {
    "userId": "guid",
    "tenantId": "guid",
    "email": "user@example.com"
  }
}
```

**Behavior notes**
- The first user created for a tenant is automatically added to the `Admin` role (alongside the default `User` role) so there is always at least one tenant administrator right after registration.
- Additional admins can be invited later via `/settings/users`. Removing the last admin is blocked; SuperAdmins can always step in through the AdminApp.

### POST /auth/login
Authenticate and receive JWT token.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "SecureP@ss123",
  "twoFactorCode": "123456" // Optional, if 2FA enabled
}
```

**Optional `fileType` hints** (case-insensitive):

| Value | Description |
| --- | --- |
| `mbox` | Single mailbox export files (`.mbox`, `.mbx`) from Gmail, Apple Mail, Thunderbird, etc. |
| `google-takeout-zip` | Google Takeout ZIP bundles that contain one or more `.mbox` files. |
| `microsoft-export-zip` | Microsoft Outlook/Office 365 exports delivered as ZIPs with a `.pst` inside. |
| `outlook-pst` | Raw `.pst` uploads (classic Outlook export). |
| `outlook-pst-zip` | Generic ZIPs that only contain a `.pst` (fallback when the UI cannot tell whether it came from Outlook’s export wizard). |
| `outlook-ost` | Raw `.ost` uploads (cached Outlook/Exchange mailbox files). |
| `outlook-ost-zip` | ZIP bundles that contain a single `.ost` file. |
| `eml` | Single `.eml` message. |
| `eml-zip` | ZIP bundle full of loose `.eml` files (Maildir-style exports). |

You can omit `fileType` (or send `auto-detect`) and Evermail will inspect the uploaded blob directly. The `ArchiveFormatDetector` service opens the blob from Azure Storage, checks ZIP entries, and validates PST/OST headers before persisting the resolved `SourceFormat` on `Mailbox` and `MailboxUpload`. Manual hints simply help the UI show smarter copy but are no longer required for accuracy.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGci...",
    "expiresAt": "2025-11-12T10:00:00Z",
    "user": {
      "id": "guid",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe"
    }
  }
}
```

### POST /auth/refresh
Refresh JWT token.

**Request Body**:
```json
{
  "refreshToken": "..."
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGci...",
    "refreshToken": "...",
    "expiresAt": "2025-11-12T10:00:00Z"
  }
}
```

---

## Tenant Security & BYOK

All endpoints under `/tenants/encryption` require `Admin` or `SuperAdmin` role membership. They let a tenant choose between Evermail-managed keys, Azure Key Vault BYOK, or the new AWS KMS connector.

### Security levels (Full Service / Confidential / Zero-Access)

Evermail exposes **three security levels** that control how much the platform can do server-side vs how strongly the tenant isolates keys:

| Level | Server capabilities | Key custody goal | Typical use |
| --- | --- | --- | --- |
| **Full Service** | Full ingestion, per-email viewing, attachments, SQL full-text search, AI features | Tenant trusts Evermail’s service to decrypt during processing (audited) | Most users |
| **Confidential Processing** | Same as Full Service, but decryption is restricted to attested confidential compute with SKR | Tenant trusts that keys release only to approved TEEs | Regulated/enterprise |
| **Zero-Access** | Server stores ciphertext only; no server-side ingestion of body/attachments | Tenant holds mailbox keys; server never receives them | Strict privacy/compliance |

**Scope**: recommended model is **tenant default** + per-upload override. The tenant default is chosen during onboarding and used as the default selection on `/upload`.

> Note: “BYOK provider” and “Security level” are distinct:
> - Full/Confidential may use Evermail-managed keys or external BYOK (Azure/AWS).
> - Zero-Access uses per-mailbox client-generated keys and does not require a server-side key provider.

#### GET /tenants/onboarding/status (extended)

The onboarding status response should include the tenant’s default security level and whether the chosen level is “ready”:
- `securityLevel`: `FullService` | `Confidential` | `ZeroAccess`
- `securityLevelReady`: boolean
- `encryptionConfigured`: boolean (still used for BYOK providers that the server must access)

#### PUT /tenants/security-level (new)

Sets the tenant’s default security level (used to preselect upload behavior). Requires `Admin`/`SuperAdmin`.

**Request Body**:
```json
{
  "securityLevel": "ZeroAccess" // FullService | Confidential | ZeroAccess
}
```

**Response (200 OK)** returns the updated onboarding status.

### GET /tenants/encryption
Fetch the current encryption settings for the authenticated tenant. The payload includes provider-agnostic status plus provider-specific metadata (Azure or AWS). Fields that don’t apply to the selected provider are omitted/null.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "provider": "AwsKms",
    "encryptionPhase": "WrapOnly",
    "isConfigured": true,
    "lastVerifiedAt": "2025-11-20T13:45:00Z",
    "lastVerificationMessage": "IAM role assumed and KMS key reachable.",
    "secureKeyRelease": {
      "isConfigured": false,
      "attestationProvider": null
    },
    "azure": null,
    "aws": {
      "kmsKeyArn": "arn:aws:kms:us-east-1:123456789012:key/abcd-1234",
      "iamRoleArn": "arn:aws:iam::123456789012:role/EvermailKeyRelease",
      "region": "us-east-1",
      "accountId": "123456789012",
      "externalId": "evermail-tenant-2fda3f8b09d1445f" // Read-only identifier the tenant configures in AWS AssumeRole policy
    }
  }
}
```

### PUT /tenants/encryption
Upsert encryption settings. The request specifies a `provider` plus the required provider-specific payload.

**Request Body**:
```json
{
  "provider": "AwsKms",                     // EvermailManaged | AzureKeyVault | AwsKms
  "azure": {
    "keyVaultUri": "https://tenant-kv.vault.azure.net/",
    "keyName": "evermail-tmk",
    "keyVersion": "0a1b2c3d4e",             // optional
    "tenantId": "11111111-2222-3333-4444-555555555555",
    "managedIdentityObjectId": "99999999-aaaa-bbbb-cccc-111111111111" // optional, used when tenant supplies their own MI
  },
  "aws": {
    "kmsKeyArn": "arn:aws:kms:us-east-1:123456789012:key/abcd-1234",
    "iamRoleArn": "arn:aws:iam::123456789012:role/EvermailKeyRelease",
    "region": "us-east-1",
    "accountId": "123456789012"
  }
}
```

- When `provider` is `EvermailManaged`, omit the `azure`/`aws` objects and the backend provisions an Azure Key Vault + TMK automatically inside Evermail’s subscription.
- When `provider` is `AzureKeyVault`, the `azure` object is required and `aws` must be omitted.
- When `provider` is `AwsKms`, the `aws` object is required and `azure` must be omitted. The server returns a generated `externalId` on the next GET response so the tenant can plug it into their AssumeRole policy.

**Response (200 OK)** mirrors `GET /tenants/encryption`.

### POST /tenants/encryption/test
Validates connectivity to the configured provider:
- `EvermailManaged` / `AzureKeyVault`: Calls `KeyClient.GetKeyAsync` (and later `WrapKey`) using `DefaultAzureCredential`.
- `AwsKms`: Uses AWS STS to assume the tenant-provided IAM role with the stored external ID, then calls `GenerateDataKeyWithoutPlaintext` on the supplied key ARN (result is discarded). The response includes request IDs for audit.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "AWS KMS key reachable. RequestId: 4d61a4f9-7bf9-4d3a-a0ed-3acfd389c101",
    "timestamp": "2025-11-20T14:03:12Z"
  }
}
```

On failure, `success` is `false` and `error` contains the human-friendly reason (missing permissions, wrong ARN, etc.).

### POST /tenants/encryption/offline
Uploads an offline BYOK bundle that was generated entirely in the browser. The payload includes the wrapped DEK plus the passphrase so the API can unwrap it once, encrypt it with the server-side protector key, and mark the tenant as `Provider = Offline`.

> **Important**: this endpoint is compatible with **Full Service** and **Confidential Processing** (where the server must unwrap keys to process mail). It is **not compatible with Zero-Access** if we want the “server never receives unwrapping material” guarantee.

**Request Body**:
```json
{
  "version": "offline-byok/v1",
  "tenantLabel": "Finance Vault",
  "createdAt": "2025-11-23T08:18:05.791Z",
  "wrappedDek": "n+6bZYpDdfrkqjCKq77i4GHaa/tYDG0eWhM+U/rxQRXgqn9s1SVm/1R1fJFDLbNX",
  "salt": "nEmZ1At8t2cL4NTn7aQCyg==",
  "nonce": "vvru5fEnMHh03kM2",
  "checksum": "9L/5X59nXs11Wutcgl2UrDBttv8ZNTetJfiVKEyWOhk=",
  "passphrase": "correct horse battery staple"
}
```

- The passphrase is only used inside the request lifetime. After the API derives the wrapping key and decrypts the DEK, both the plaintext DEK and passphrase are zeroed.
- The service encrypts the DEK with the protector key configured via `OfflineByok:MasterKey` and stores the ciphertext in `TenantEncryptionSettings.OfflineMasterKeyCiphertext`.
- Idempotent: calling this endpoint again overwrites the previous offline key (useful for rotation).

**Response (200 OK)** mirrors `GET /tenants/encryption`. If the checksum or passphrase is wrong the API returns `400 Bad Request` with `error` set to the reason.

### GET /tenants/encryption/bundles
List every wrapped offline BYOK bundle registered for the tenant. Each entry represents a separate admin’s recovery copy.

```json
{
  "success": true,
  "data": [
    {
      "id": "f1a75bea-8b54-4f09-8357-9e9c2265b2ed",
      "label": "Legal team – Marta",
      "version": "offline-byok/v1",
      "createdByUserId": "5bfe3c6e-557d-4b6a-b244-6bb4dbe5ceb0",
      "createdAt": "2025-11-23T19:42:00Z",
      "lastUsedAt": "2025-11-23T19:42:30Z"
    }
  ]
}
```

### POST /tenants/encryption/bundles
Upload or register an additional wrapped DEK bundle (identical schema to the Offline BYOK lab output). The payload never contains plaintext keys.

```json
{
  "version": "offline-byok/v1",
  "label": "Legal team – Marta",
  "wrappedDek": "n+6bZYpDdfrkqjCKq77i4GHaa/tYDG0eWhM+U/rxQRXgqn9s1SVm/1R1fJFDLbNX",
  "salt": "nEmZ1At8t2cL4NTn7aQCyg==",
  "nonce": "vvru5fEnMHh03kM2",
  "checksum": "9L/5X59nXs11Wutcgl2UrDBttv8ZNTetJfiVKEyWOhk="
}
```

Response mirrors `GET /tenants/encryption/bundles`. Validation ensures salts/nonces/checksums are base64-decoded and lengths match the spec.

### DELETE /tenants/encryption/bundles/{id}
Remove a stale bundle (for example when an admin leaves). Does **not** rotate the active offline key, it simply deletes the encrypted copy.

```
DELETE /api/v1/tenants/encryption/bundles/f1a75bea-8b54-4f09-8357-9e9c2265b2ed
204 No Content
```

### GET /tenants/encryption/secure-key-release/template
Returns the Secure Key Release (SKR) JSON template for the current tenant. The template already contains the tenant’s managed-identity object ID and a placeholder attestation clause (`allowEvermailOps`) so administrators only need to paste it into the Azure CLI/portal before Phase 2 lands.

```json
{
  "success": true,
  "data": {
    "policy": "{\n  \"version\": \"1.0.0\",\n  \"anyOf\": [\n    {\n      \"authority\": \"allowEvermailOps\",\n      \"allOf\": [\n        { \"claim\": \"x-ms-microsoft-identity-principal-id\", \"equals\": \"5a5c2c0f-...\" }\n      ]\n    }\n  ]\n}"
  }
}
```

### GET /tenants/encryption/secure-key-release
Returns the currently stored policy JSON (only visible to the owning tenant) plus metadata describing when it was staged.

```json
{
  "success": true,
  "data": {
    "policy": "{\n  \"version\": \"1.0.0\",\n  \"anyOf\": [...]\n}",
    "hash": "08C47E6F4DF9A3E0B30205E0E7C97868AB500D27C0A951C2967A8D5F5949D6F6",
    "configuredAt": "2025-11-24T10:15:00Z",
    "attestationProvider": "allowEvermailOps"
  }
}
```

### POST /tenants/encryption/secure-key-release
Stores (or replaces) the tenant’s SKR JSON. The backend validates that the payload is valid JSON, canonicalizes spacing, hashes it with SHA-256, and updates `TenantEncryptionSettings`. A successful save flips `secureKeyRelease.isConfigured` to `true` in subsequent `GET /tenants/encryption` responses.

```json
{
  "policy": "{ \"version\": \"1.0.0\", \"anyOf\": [{ \"authority\": \"allowEvermailOps\", \"allOf\": [{ \"claim\": \"x-ms-microsoft-identity-principal-id\", \"equals\": \"5a5c2c0f-...\" }] }] }",
  "attestationProvider": "allowEvermailOps"
}
```

**Response (200 OK)**:

```json
{
  "success": true,
  "data": {
    "policy": "{\n  \"version\": \"1.0.0\",\n  \"anyOf\": [...]\n}",
    "hash": "08C47E6F4DF9A3E0B30205E0E7C97868AB500D27C0A951C2967A8D5F5949D6F6",
    "configuredAt": "2025-11-24T10:15:00Z",
    "attestationProvider": "allowEvermailOps"
  }
}
```

### DELETE /tenants/encryption/secure-key-release
Clears the stored SKR JSON/hash so the tenant can upload a revised policy. After deletion `secureKeyRelease.isConfigured` becomes `false` until another policy is posted.

```
DELETE /api/v1/tenants/encryption/secure-key-release
204 No Content
```

> **UI expectation**: These SKR endpoints are only invoked by the admin portal’s buttons. Tenants never download scripts nor edit JSON manually—the UI performs the POST/DELETE calls and simply shows the resulting hash + timestamp.

### GET /audit/logs
Returns paginated audit entries for the current tenant. Filters are optional; omitting them returns the latest 50 rows.

| Query | Type | Description |
|-------|------|-------------|
| `page` | int (default 1) | 1-based page number |
| `pageSize` | int (default 50, max 200) | Page size |
| `startUtc` | ISO 8601 | Filter `Timestamp >= startUtc` |
| `endUtc` | ISO 8601 | Filter `Timestamp <= endUtc` |
| `action` | string | Exact match on `Action` |
| `userId` | guid | Filter by actor |
| `resourceType` | string | Exact match on `ResourceType` |

**Response**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "f1a7...",
        "timestamp": "2025-11-24T10:11:00Z",
        "action": "MailboxDeleted",
        "userId": "7aa3...",
        "userEmail": "ops@acme.com",
        "resourceType": "Mailbox",
        "resourceId": "2c4f...",
        "ipAddress": "203.0.113.42",
        "userAgent": "Mozilla/5.0 ...",
        "details": "{\"mailboxId\":\"2c4f...\",\"reason\":\"GDPR\"}"
      }
    ],
    "total": 5421,
    "page": 1,
    "pageSize": 50
  }
}
```

### GET /audit/logs/export
Streams a CSV (UTF-8, RFC 4180) containing up to 10 000 audit rows that match the supplied filters (same query parameters as the list endpoint). The response sets `Content-Disposition: attachment; filename="evermail-audit-2025-11-24.csv"` and includes `X-Export-Hash` (SHA-256 of the CSV body) for integrity.

```
GET /api/v1/audit/logs/export?startUtc=2025-11-01T00:00:00Z&action=MailboxDeleted
```

**Response**

```
HTTP/1.1 200 OK
Content-Type: text/csv
X-Export-Hash: 08C47E6F4DF9A3E0B30205E0E7C97868AB500D27C0A951C2967A8D5F5949D6F6

timestamp,action,userEmail,resourceType,resourceId,ipAddress,details
2025-11-24T10:11:00Z,MailboxDeleted,ops@acme.com,Mailbox,2c4f...,203.0.113.42,"{""mailboxId"":""2c4f..."",""reason"":""GDPR""}"
```

### GET /compliance/gdpr-jobs
Lists the tenant’s recent GDPR exports and deletion jobs so admins can verify status.

```json
{
  "success": true,
  "data": {
    "exports": [
      {
        "id": "af25...",
        "requestedBy": "dpo@acme.com",
        "requestedAt": "2025-11-22T09:00:00Z",
        "completedAt": "2025-11-22T09:04:30Z",
        "status": "Completed",
        "downloadUrl": "https://localhost:7136/api/v1/users/me/exports/af25.../download",
        "sha256": "6F6A..."
      }
    ],
    "deletions": [
      {
        "id": "ad91...",
        "targetUserEmail": "former.user@acme.com",
        "requestedBy": "dpo@acme.com",
        "requestedAt": "2025-11-18T11:12:13Z",
        "completedAt": "2025-11-18T11:30:50Z",
        "status": "Completed"
      }
    ]
  }
}
```

> **UI expectation**: These compliance endpoints are invoked exclusively by the admin portal’s buttons/chips. Results are read-only; CSV downloads and GDPR job links are surfaced as standard browser downloads without exposing SAS tokens or requiring CLI steps.

### GET /tenants/plans
Returns every active subscription plan so the onboarding wizard and admin screens can render pricing cards.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "name": "Free",
      "displayName": "Free Tier",
      "description": "Try Evermail with 1 mailbox and 30-day retention",
      "priceMonthly": 0,
      "priceYearly": 0,
      "currency": "EUR",
      "maxStorageGb": 1,
      "maxFileSizeGb": 1,
      "maxUsers": 1,
      "maxMailboxes": 1,
      "isRecommended": false,
      "features": [
        "1 GB storage",
        "1 mailbox",
        "30-day retention"
      ]
    }
  ]
}
```

### PUT /tenants/subscription
Sets the tenant’s subscription tier (and confirms the onboarding plan step). The backend updates `Tenant.SubscriptionTier`, `MaxStorageGB`, `MaxUsers`, and stamps `OnboardingPlanConfirmedAt`.

**Request Body**:
```json
{
  "planName": "Pro"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "message": "Subscription updated"
  }
}
```

### GET /tenants/onboarding/status
Returns a lightweight status object used by the guided registration / dashboard checklist so new admins can see what’s left to configure.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "hasAdmin": true,
    "planConfirmed": false,
    "subscriptionTier": "Free",
    "encryptionConfigured": false,
    "hasMailbox": false,
    "securityPreference": "QuickStart",
    "paymentAcknowledged": false,
    "paymentAcknowledgedAt": null,
    "identityProvider": "Google"
  }
}
```

Interpretation:
- `hasAdmin` – `true` as soon as at least one user in the tenant has the `Admin` role. Because the first registrant is auto-promoted, this typically starts as `true`.
- `planConfirmed` – `true` after the tenant explicitly selects (or reconfirms) a plan inside the onboarding wizard; backed by `Tenant.OnboardingPlanConfirmedAt`.
- `subscriptionTier` – the current plan name (`Free`, `Pro`, `Team`, `Enterprise`), even if not yet confirmed.
- `encryptionConfigured` – `true` once the tenant has selected Evermail-managed, Azure Key Vault, AWS KMS, or Offline BYOK and supplied the required fields.
- `hasMailbox` – `true` once at least one mailbox is uploaded; completes the onboarding checklist.
- `securityPreference` – `"QuickStart"` (Evermail-managed) or `"BYOK"` depending on what the admin picked in the wizard. This lets the UI display the right card even before BYOK setup is complete.
- `paymentAcknowledged` / `paymentAcknowledgedAt` – track whether the admin clicked the “I’ll connect Stripe later” placeholder. We block the upload step until this box is checked so future billing prompts make sense.
- `identityProvider` – `"Google"`, `"Microsoft"`, or `"Password"` based on the current user’s authentication provider. Display-only; helps admins understand which account is active while configuring keys.

The endpoint requires `Admin` or `SuperAdmin` role membership and is consumed by `/` (dashboard) and `/onboarding`.

### PUT /tenants/onboarding/security
Records which encryption path the tenant intends to use so the onboarding wizard can highlight the correct card and copy. This does **not** provision keys by itself—it simply stores the preference (`QuickStart` or `BYOK`). For quick start, the UI immediately follows up with `PUT /tenants/encryption` using the `EvermailManaged` provider so the step completes.

**Request Body**:
```json
{
  "mode": "BYOK"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "securityPreference": "BYOK"
  }
}
```

### PUT /tenants/onboarding/payment
Marks the placeholder billing step as acknowledged. Until Stripe checkout is wired up we simply capture that the admin saw the pricing copy and agreed to connect billing later.

**Request Body**:
```json
{
  "acknowledged": true
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "paymentAcknowledgedAt": "2025-11-22T10:15:00Z"
  }
}
```

---

## Mailboxes

### GET /mailboxes
List all mailboxes for the current user.

**Query Parameters**:
- `status` (optional): Filter by status (`Pending`, `Processing`, `Completed`, `Failed`)
- `page` (default: 1): Page number
- `pageSize` (default: 20): Items per page
- `tagToken` (optional, repeatable): Deterministic hash of a mailbox tag. Clients derive the hash locally using the zero-access HKDF/HMAC helper before calling the API.
- `fromToken` / `toToken` / `ccToken` / `subjectToken` (optional, repeatable): Deterministic hashes generated from sender addresses, recipient addresses, CC addresses, and subject lines that the browser extracted during an encrypted upload. Passing one or more of these parameters returns only the encrypted mailboxes whose header token sets contain every supplied hash.

> Each mailbox object now includes `normalizedSizeBytes`. When zero, only the original upload size is known; once the ingestion worker finishes normalization, this field reflects the uncompressed `.mbox` size used for progress bars and storage calculations.
**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "guid",
        "displayName": "Personal Gmail",
        "fileName": "gmail-export.mbox",
        "fileSizeBytes": 52428800,
        "status": "Completed",
        "totalEmails": 1523,
        "processedEmails": 1523,
        "failedEmails": 0,
        "processedBytes": 52428800,
        "uploadRemoved": false,
        "isPendingDeletion": false,
        "latestUpload": {
          "id": "guid",
          "fileName": "gmail-export.mbox",
          "fileSizeBytes": 52428800,
          "status": "Completed",
          "keepEmails": false,
          "createdAt": "2025-11-10T15:30:00Z",
          "processingStartedAt": "2025-11-10T15:31:00Z",
          "processingCompletedAt": "2025-11-10T15:35:00Z",
          "deletedAt": null
        },
        "createdAt": "2025-11-10T15:30:00Z",
        "processingCompletedAt": "2025-11-10T15:35:00Z"
      }
    ],
    "totalCount": 5,
    "page": 1,
    "pageSize": 20
  }
}
```

### POST /mailboxes
Upload a new .mbox file.

**Request**: `multipart/form-data`
- `file`: The .mbox file (max 5GB for Pro tier)
 - `fileType`: One of the supported archive identifiers (see below)

**Supported `fileType` values**

| Value | Description | Typical extension |
| --- | --- | --- |
| `mbox` | Single mailbox export (Gmail, Thunderbird, Apple Mail) | `.mbox` |
| `google-takeout-zip` | Google Takeout archive containing one or more `.mbox` files | `.zip` |
| `microsoft-export-zip` | Microsoft export bundle containing a `.pst` | `.zip` |
| `outlook-pst` | Direct Outlook PST export | `.pst` |
| `outlook-pst-zip` | Zipped PST handed off by enterprise admins | `.zip` |
| `eml` | Standalone `.eml` message (imported as a one-message mailbox) | `.eml` |
| `eml-zip` | Maildir/EML bundle zipped up by Apple Mail, Thunderbird, etc. | `.zip` |

> ℹ️ The backend still inspects the uploaded blob to verify it matches the claimed format. A `.zip` without `.mbox`, `.pst`, or `.eml` entries is rejected even if the `fileType` is set correctly.

**Response (202 Accepted)**:
```json
{
  "success": true,
  "data": {
    "mailboxId": "guid",
    "uploadId": "guid",
    "fileName": "archive.mbox",
    "status": "Pending",
    "message": "Upload successful. Processing will begin shortly."
  }
}
```

### GET /mailboxes/{id}
Get mailbox details.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "fileName": "gmail-export.mbox",
    "fileSizeBytes": 52428800,
    "status": "Completed",
    "totalEmails": 1523,
    "processedEmails": 1523,
    "failedEmails": 0,
    "createdAt": "2025-11-10T15:30:00Z",
    "processingStartedAt": "2025-11-10T15:31:00Z",
    "processingCompletedAt": "2025-11-10T15:35:00Z",
    "errorMessage": null
  }
}
```

### PATCH /mailboxes/{id}
Rename an existing mailbox (cosmetic only).

**Request Body**:
```json
{
  "displayName": "Client Archive (2019-2024)"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "displayName": "Client Archive (2019-2024)"
  }
}
```

### GET /mailboxes/{id}/uploads
List upload/re-import history for a mailbox.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "fileName": "gmail-export.mbox",
      "fileSizeBytes": 52428800,
        "normalizedSizeBytes": 73400320,
      "status": "Completed",
      "uploadedAt": "2025-11-10T15:30:00Z",
      "keepEmails": false,
      "deletedAt": null
    }
  ]
}
```

### POST /mailboxes/{id}/uploads
Re-import emails into an existing mailbox. This is a convenience wrapper that internally calls `POST /upload/initiate` with `mailboxId` and then `POST /upload/complete` using the returned `uploadId`. Duplicate emails are skipped using the per-message `ContentHash`.

**Response (202 Accepted)** mirrors `POST /mailboxes` and includes `uploadId`.

### POST /mailboxes/{id}/delete
Soft-delete mailbox data with granular options. Payload determines whether to remove the uploaded blob, indexed emails, or both. Unless `purgeNow` is `true` and the caller is a SuperAdmin, the request is scheduled in the recycle bin for 30 days.

**Request Body**:
```json
{
  "deleteUpload": true,
  "deleteEmails": false,
  "purgeNow": false
}
```

**Response (202 Accepted)**:
```json
{
  "success": true,
  "data": {
    "jobId": "guid",
    "executeAfter": "2025-12-18T00:00:00Z",
    "status": "Scheduled"
  }
}
```

### DELETE /mailboxes/{id}
Immediate hard-delete of a mailbox. Restricted to SuperAdmins and bypasses the recycle bin. Normal users should call `POST /mailboxes/{id}/delete`.

**Response (204 No Content)**

---

## Upload Workflow (Client Handshake)

Uploading large .mbox files happens in two steps so that the browser can stream directly to Azure Blob Storage while the API tracks `MailboxUpload` metadata.

### POST /upload/initiate
Request a short-lived SAS URL for uploading a file.

**Request Body**:
```json
{
  "fileName": "gmail-export.mbox",
  "fileSizeBytes": 52428800,
  "fileType": "mbox",
  "mailboxId": "guid (optional when re-importing)"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "uploadUrl": "https://storage.blob.core.windows.net/mbox-archives/...sig...",
    "blobPath": "mbox-archives/{tenantId}/{mailboxId}/original.mbox",
    "mailboxId": "guid",
    "uploadId": "guid",
    "expiresAt": "2025-11-18T18:30:00Z"
  }
}
```

### POST /upload/complete
Tell the API the browser finished uploading the blob so it can enqueue processing.

**Request Body**:
```json
{
  "mailboxId": "guid",
  "uploadId": "guid"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "mailboxId": "guid",
    "uploadId": "guid",
    "status": "Queued"
  }
}
```

If the client calls `/mailboxes/{id}/uploads` the server wraps both steps and returns the same payload.

### POST /mailboxes/encrypted-upload/initiate
### POST /upload/encrypted/initiate
Start a zero-access upload. Returns the upload SAS URL, mailbox/upload IDs, and the `tokenSalt` the browser needs to derive deterministic tokens from the per-upload key.

**Request**
```json
{
  "fileName": "SensitiveArchive.mbox",
  "fileSizeBytes": 134217728,
  "mailboxId": "optional-guid",
  "scheme": "zero-access/aes-gcm-chunked/v1"
}
```

**Response**
```json
{
  "success": true,
  "data": {
    "uploadUrl": "https://storage.blob.core.windows.net/mbox-archives/...&sig=...",
    "blobPath": "mbox-archives/{tenant}/{mailbox}/{guid}_encrypted.mbox",
    "mailboxId": "98d1c9b9-01f7-4fd7-9ccb-d3b18d1e1e93",
    "uploadId": "87a4f041-32d3-4f2b-930c-b52100e64bb5",
    "expiresAt": "2025-11-18T18:30:00Z",
    "tokenSalt": "rTQ6kpmzO5E8b+Ja3n8ihQ=="
  }
}
```

### POST /mailboxes/encrypted-upload/complete
### POST /upload/encrypted/complete
Finish a zero-access upload after the ciphertext has been committed. The browser provides the encryption metadata, DEK fingerprint, ciphertext sizes, and hashed deterministic token sets.

**Request**
```json
{
  "mailboxId": "98d1c9b9-01f7-4fd7-9ccb-d3b18d1e1e93",
  "uploadId": "87a4f041-32d3-4f2b-930c-b52100e64bb5",
  "scheme": "zero-access/aes-gcm-chunked/v1",
  "keyFingerprint": "7oxTWQ+R6AndVtmzvW6IFmUn+bE1qzGV1JlGN4fq6to=",
  "metadataJson": "{...}",
  "originalSizeBytes": 134217728,
  "cipherSizeBytes": 136314880,
  "tokenSets": [
    {
      "tokenType": "tag",
      "tokens": [
        "D7if5gKN7Qq6yGwq/3qTwcHOGH3R7pV8r1p4t7SqU8g=",
        "Mk3Z9bYsi/6YQM4tI8MkU7gVn1o5grquR7pMtybFstA="
      ]
    },
    {
      "tokenType": "from",
      "tokens": [
        "wEyjv/4alw4Bt6z4uhgYV38kI8vv0F+n6nxXQhYKqNA=",
        "hDkLk0kboZoAJn1Xy4QdNPpXkKsdn9rxB2KpUN7TnEY="
      ]
    },
    {
      "tokenType": "subject",
      "tokens": [
        "b8ANYKz0B7YrHp4KZZ2n8IWlg6lvrgP4f0gE7u8w7Vg="
      ]
    }
  ]
}
```

**Response (200 OK)**
```json
{
  "success": true,
  "data": {
    "mailboxId": "98d1c9b9-01f7-4fd7-9ccb-d3b18d1e1e93",
    "uploadId": "87a4f041-32d3-4f2b-930c-b52100e64bb5",
    "status": "Encrypted"
  }
}
```

`tokenSets.tokens` are Base64 HMAC-SHA256 strings derived client-side using the per-upload key, the `tokenSalt`, and HKDF per the implementation in `Evermail.WebApp/wwwroot/js/zero-access-upload.js`. The same algorithm powers mailbox tags as well as the `from` / `to` / `cc` / `subject` header indexes. The server stores the opaque hashes but cannot reverse them or distinguish their plaintext values.

### GET /mailboxes?tagToken=...
Filter the mailbox list by deterministic tag tokens (supply multiple `tagToken` query parameters for AND semantics). Clients hash the plaintext tag locally before calling this endpoint.

```
GET /api/v1/mailboxes?tagToken=D7if5gKN7Qq6yGwq%2F3qTwcHOGH3R7pV8r1p4t7SqU8g%3D
GET /api/v1/mailboxes?fromToken=wEyjv%2F4alw4Bt6z4uhgYV38kI8vv0F%2Bn6nxXQhYKqNA%3D&subjectToken=b8ANYKz0B7YrHp4KZZ2n8IWlg6lvrgP4f0gE7u8w7Vg%3D
```

> 💡 **Deriving header tokens**: The zero-access upload page already hashes these values for you. When building custom tooling, import the same HKDF/HMAC routine documented in `zero-access-upload.js` so every client produces identical Base64 tokens for a given plaintext value.

## Emails

### GET /emails/search
Search emails with full-text query, recipient/conversation filters, and advanced ranking controls.

**Query Parameters**:
- `q` (required): Search query (supports full-text syntax)
- `mailboxId` (optional): Filter by specific mailbox
- `from` (optional): Filter by sender email
- `dateFrom` (optional): ISO 8601 date, e.g., `2025-01-01`
- `dateTo` (optional): ISO 8601 date
- `hasAttachments` (optional): `true` or `false`
- `recipient` (optional): Matches any `To/Cc/Bcc/Reply-To/Sender` address or display name (case-insensitive `LIKE`)
- `conversationId` (optional): Filter to a specific thread/conversation GUID
- `page` (default: 1)
- `pageSize` (default: 50, max: 100)
- `sortBy` (default: `rank` when `q` provided, otherwise `date`): `rank`, `date`, `subject`, `from`
- `sortOrder` (default: `desc`): `asc`, `desc`
- `stopWords` (optional): Comma-separated list of terms to ignore when executing full-text search (e.g., `stopWords=the,and,or`)
- `useInflectionalForms` (optional, default: `false`): When `true`, wraps each token in `FORMSOF(INFLECTIONAL, ...)` so `plan` also matches `planned`, `planning`, etc.

> **Date filters**: When `dateFrom`/`dateTo` are provided as date-only values (no time component) the API automatically interprets `dateFrom` as the start of that day (00:00:00 UTC) and `dateTo` as the inclusive end of that day. In practice, `dateTo=2025-01-31` expands to “before 2025‑02‑01 00:00:00” so you never lose emails that arrived later that same day.

**Example Request**:
```
GET /emails/search?q="invoice NEAR payment"&hasAttachments=true&stopWords=the,and&useInflectionalForms=true&sortBy=rank&page=1&pageSize=20
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "guid",
        "mailboxId": "guid",
        "subject": "Invoice #12345 for January",
        "fromAddress": "billing@company.com",
        "fromName": "Company Billing",
        "date": "2025-01-15T10:00:00Z",
        "snippet": "Attached is your invoice for January so you can expense it right away.",
        "highlightedSnippet": "Attached is your <mark class=\"search-hit\">invoice</mark> for January so you can expense it right away.",
        "matchFields": [
          "Subject",
          "Body"
        ],
        "matchSources": [
          "Subject",
          "TextBody"
        ],
        "matchedTerms": [
          "invoice",
          "payment"
        ],
        "snippetOffset": 128,
        "snippetLength": 160,
        "hasAttachments": true,
        "attachmentCount": 1,
        "attachmentPreviews": [
          {
            "id": "fd0a21c5-1815-4c9d-a8f6-4e34cd0f57e8",
            "fileName": "invoice-12345.pdf",
            "sizeBytes": 102400
          }
        ],
        "isRead": false,
        "firstAttachmentId": "fd0a21c5-1815-4c9d-a8f6-4e34cd0f57e8",
        "isPinned": false,
        "pinnedAt": null,
        "rank": 765,
        "conversationId": "0b1a1b2c-4567-890a-bcde-ff1122334455",
        "threadSize": 7,
        "threadDepth": 3
      }
    ],
    "totalCount": 42,
    "page": 1,
    "pageSize": 20,
    "queryTime": 0.123
  }
}
```

When `q` is provided the API issues a SQL Server `CONTAINSTABLE` query across `Subject`, `TextBody`, `HtmlBody`, `RecipientsSearch`, `FromName`, and `FromAddress`. The response still exposes the raw `rank` value for telemetry and sorting, but the UI relies on `matchFields`, `matchSources`, and `matchedTerms` to explain why a row surfaced (“Matched in Subject + Body”). `snippetOffset`/`snippetLength` point to the source window (roughly 160 characters) so highlight navigators can jump directly to the span, and `highlightedSnippet` contains sanitized HTML (`<mark class="search-hit">`). `attachmentPreviews` lists up to 3 attachments (filename/id/size) so inline pills can render without fetching full detail. `isPinned`/`pinnedAt` tell the UI whether this message/thread is favorited; pinned matches are grouped at the top but remain tenant-scoped. Use `stopWords` to drop filler terms per request, and `useInflectionalForms=true` to expand each token into `FORMSOF(INFLECTIONAL, ...)`. Default sort switches to `rank desc` when `q` is present, but you can still override `sortBy`/`sortOrder`.

Key response fields:
- `matchFields` vs `matchSources`: the first describes which logical columns satisfied the query (Subject, Body, Sender, Recipients), while the second lists the concrete source (TextBody, HtmlBody, Attachment filename, etc.) so the client can render friendly chips (“Matched in subject + attachment”).
- `snippetOffset`/`snippetLength`: allow the detail view to scroll to the exact hit when `autoScrollToKeyword` is enabled. Even though SQL Server doesn’t emit offsets, the backend computes them from the snippet builder so navigation stays deterministic.
- `highlightedSnippet`: sanitized/encoded HTML that already contains `<mark class="search-hit">` spans. The cards reuse it verbatim while the plain `snippet` field is there for text-only surfaces.
- `attachmentPreviews`: up to three attachment IDs + filenames + sizes so the card layout can show inline download pills. Full attachment metadata remains on the detail endpoint.
- `isPinned`/`pinnedAt`: drive the “Pinned” section at the top of the results. Pins are scoped to the current tenant/user and respect multi-tenant filters automatically.

### GET /emails/{id}
Get full email details.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "mailboxId": "guid",
    "messageId": "<abc123@mail.gmail.com>",
    "subject": "Invoice #12345 for January",
        "fromAddress": "billing@company.com",
        "fromName": "Company Billing",
    "toAddresses": ["user@example.com"],
    "toNames": ["John Doe"],
    "ccAddresses": [],
    "date": "2025-01-15T10:00:00Z",
        "textBody": "Thank you for your payment...",
        "htmlBody": "<html>...</html>",
        "replyToAddress": "accounts@company.com",
        "senderAddress": "mailer@company.com",
        "senderName": "Company Mailer",
        "returnPath": "bounce@company.com",
        "listId": "invoices.company.com",
        "threadTopic": "Invoice #12345",
        "importance": "High",
        "priority": "1",
        "categories": "Finance;Invoices",
        "conversationId": "0b1a1b2c-4567-890a-bcde-ff1122334455",
        "threadSize": 7,
        "threadDepth": 3,
        "hasAttachments": true,
        "attachments": [
      {
        "id": "guid",
        "fileName": "invoice-12345.pdf",
        "contentType": "application/pdf",
        "sizeBytes": 102400,
        "downloadUrl": "/api/v1/attachments/guid/download"
      }
    ]
  }
}
```

Every email now returns its thread GUID + depth, letting clients prefetch adjacent messages or jump
between replies. Envelope metadata (Reply-To, Sender, Return-Path, ListId, Thread-Topic, Importance,
Priority, Categories) mirrors common Outlook/Gmail headers so downstream automation can plug in
without re-parsing raw MIME.

### GET /emails/saved-filters
Return the caller’s reusable search filters so the UI can render chips above the results list.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "a4e6c131-7c03-4eb0-8b18-1ce7a9ed6e1f",
      "name": "From Alice (30 days)",
      "definition": {
        "from": "alice@acme.com",
        "dateFrom": "2025-10-20",
        "hasAttachments": true
      },
      "orderIndex": 0,
      "isFavorite": true,
      "createdAt": "2025-11-19T08:30:00Z",
      "updatedAt": "2025-11-20T07:02:00Z"
    }
  ]
}
```

### POST /emails/saved-filters
Create a new saved filter. Definition payload mirrors `GET /emails/search` query parameters.

**Request Body**:
```json
{
  "name": "Invoices last quarter",
  "definition": {
    "q": "\"invoice\" NEAR payment",
    "dateFrom": "2025-09-01",
    "dateTo": "2025-11-30",
    "hasAttachments": true
  },
  "orderIndex": 2,
  "isFavorite": false
}
```

**Response (201 Created)** returns the saved filter resource (same shape as GET).

### PUT /emails/saved-filters/{id}
Update name, definition, order, or favorite state.

**Request Body** mirrors POST; fields you omit remain unchanged.

**Response (200 OK)** returns the updated resource.

### DELETE /emails/saved-filters/{id}
Delete a saved filter. Response is `204 No Content`.

### POST /emails/{id}/pin
Pin a specific email so it floats to the “Pinned” block whenever it matches a query. Pinned state is
per-user, per-tenant.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "emailId": "guid",
    "conversationId": "guid",
    "isPinned": true,
    "pinnedAt": "2025-11-20T09:30:00Z"
  }
}
```

### DELETE /emails/{id}/pin
Unpin a previously pinned email. Response: `204 No Content`.

### POST /emails/conversations/{conversationId}/pin
Pin an entire conversation/thread so any message within it floats to the pinned block.

**Response (200 OK)** mirrors the single-email pin response but only includes `conversationId`.

### DELETE /emails/conversations/{conversationId}/pin
Remove the pin for the given conversation. Response: `204 No Content`.

### PATCH /emails/{id}/read
Mark email as read/unread.

**Request Body**:
```json
{
  "isRead": true
}
```

**Response (204 No Content)**

---

## Attachments

### GET /attachments/{id}/download
Download an attachment.

**Response (200 OK)**:
- Content-Type: Attachment's MIME type
- Content-Disposition: `attachment; filename="invoice.pdf"`
- Body: Binary file content

**URL Generation**: Short-lived SAS token (15 minutes)

---

## User Management

### GET /users/me/profile
Retrieve the authenticated user's profile plus workspace limits. Requires any authenticated user; responses are automatically scoped to the caller's tenant.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "userId": "15f1e1a7-6ce5-46fe-b38b-9dcb9fb3099f",
    "tenantId": "7f4d74a8-5e6a-4de0-9a9f-0545e0a2f823",
    "email": "me@evermail.com",
    "firstName": "Mia",
    "lastName": "Lopez",
    "twoFactorEnabled": true,
    "createdAt": "2025-10-28T13:04:05Z",
    "lastLoginAt": "2025-11-21T09:32:14Z",
    "tenantName": "Acme Labs",
    "subscriptionTier": "Pro",
    "maxStorageGb": 5,
    "maxUsers": 1,
    "storageBytesUsed": 2147483648,
    "mailboxCount": 3,
    "roles": ["User", "Admin"]
  }
}
```

Use this payload on the `/settings` page for account identity, workspace plan limits, and 2FA badges. Admin-only information (like onboarding state) is still surfaced via `/tenants/onboarding/status`.

### POST /users/me/enable-2fa
Enable two-factor authentication.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "qrCodeDataUrl": "data:image/png;base64,...",
    "secret": "JBSWY3DPEHPK3PXP",
    "message": "Scan QR code with authenticator app, then verify with a code"
  }
}
```

### GET /users/me/settings/display
Fetch display/search preferences persisted in `UserDisplaySettings`.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "dateFormat": "MMM dd, yyyy",
    "resultDensity": "Cozy",
    "autoScrollToKeyword": true,
    "matchNavigatorEnabled": true,
    "keyboardShortcutsEnabled": true
  }
}
```

### PUT /users/me/settings/display
Update any subset of display preferences. Omitted fields remain unchanged.

**Request Body**:
```json
{
  "dateFormat": "dd.MM.yyyy",
  "resultDensity": "Compact",
  "autoScrollToKeyword": false
}
```

**Response (200 OK)** mirrors GET and returns the persisted values.

### POST /users/me/verify-2fa
Verify 2FA setup.

**Request Body**:
```json
{
  "code": "123456"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "twoFactorEnabled": true,
    "backupCodes": ["12345678", "87654321", ...]
  }
}
```

---

## Billing & Subscriptions

### GET /billing/plans
Get available subscription plans.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "name": "Pro",
      "displayName": "Professional",
      "priceMonthly": 9.00,
      "priceYearly": 90.00,
      "currency": "EUR",
      "maxStorageGB": 5,
      "maxUsers": 1,
      "features": [
        "5 GB storage",
        "Unlimited mailboxes",
        "Full-text search",
        "1-year retention"
      ]
    }
  ]
}
```

### POST /billing/checkout
Create Stripe Checkout session.

**Request Body**:
```json
{
  "planId": "guid",
  "billingPeriod": "monthly", // or "yearly"
  "successUrl": "https://evermail.com/billing/success",
  "cancelUrl": "https://evermail.com/billing/cancel"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "sessionId": "cs_test_...",
    "checkoutUrl": "https://checkout.stripe.com/pay/cs_test_..."
  }
}
```

### POST /billing/portal
Get Stripe Customer Portal URL.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "portalUrl": "https://billing.stripe.com/session/live_..."
  }
}
```

### GET /billing/subscription
Get current subscription details.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "plan": {
      "name": "Pro",
      "displayName": "Professional"
    },
    "status": "Active",
    "currentPeriodStart": "2025-11-01T00:00:00Z",
    "currentPeriodEnd": "2025-12-01T00:00:00Z",
    "cancelAtPeriodEnd": false,
    "nextBillingAmount": 9.00,
    "currency": "EUR"
  }
}
```

---

## Admin Endpoints

> ⚠️ **Status note (2025-12-16)**: The Evermail **SuperAdmin portal** is `Evermail.AdminApp` (internal-only, OAuth allowlist).  
> AdminApp currently renders ops/tenants/business views by reading SQL + probing platform services. The `/api/v1/admin/*` endpoints below are **planned** and should not be treated as implemented unless the code exists under `Evermail.WebApp/Endpoints/*`.

> ℹ️ **Tenant admin UI note**: Tenant-admin features live in the customer WebApp under `/settings/*` (e.g., `/settings/billing`, `/settings/encryption`, `/settings/compliance`, `/settings/recovery`). Legacy `/admin/*` routes remain as aliases.

### GET /admin/tenants
List all tenants (SuperAdmin only).

**Query Parameters**:
- `page`, `pageSize`, `status`

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "guid",
        "name": "Acme Corp",
        "subscriptionTier": "Team",
        "userCount": 3,
        "storageUsedGB": 12.5,
        "maxStorageGB": 50,
        "isActive": true,
        "createdAt": "2025-10-01T00:00:00Z"
      }
    ],
    "totalCount": 150,
    "page": 1,
    "pageSize": 20
  }
}
```

### GET /admin/jobs
Monitor mailbox processing jobs.

**Query Parameters**:
- `status`: `Pending`, `Processing`, `Completed`, `Failed`

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "queueDepth": 5,
    "processingCount": 2,
    "recentJobs": [
      {
        "mailboxId": "guid",
        "tenantName": "Acme Corp",
        "status": "Processing",
        "progress": 45,
        "startedAt": "2025-11-11T10:00:00Z"
      }
    ]
  }
}
```

---

## Webhooks

### POST /webhooks/stripe
Handle Stripe webhook events.

**Headers**:
- `Stripe-Signature`: Webhook signature for verification

**Request Body** (varies by event type):
```json
{
  "id": "evt_...",
  "type": "invoice.payment_succeeded",
  "data": {
    "object": { /* invoice object */ }
  }
}
```

**Response (200 OK)**:
```json
{
  "received": true
}
```

**Handled Events**:
- `checkout.session.completed`
- `invoice.payment_succeeded`
- `invoice.payment_failed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`

---

## GDPR & Data Export

### POST /users/me/export
Request a GDPR data bundle. The server immediately starts streaming the archive (profile, mailboxes, emails, audit logs) into the dedicated `gdpr-exports` blob container.

**Response (202 Accepted)**:
```json
{
  "success": true,
  "data": {
    "id": "e6b4f15e-4eb5-4e19-a388-3a566063d8e6",
    "status": "Completed",
    "requestedAt": "2025-11-23T21:10:00Z",
    "completedAt": "2025-11-23T21:10:07Z",
    "expiresAt": "2025-11-30T21:10:07Z",
    "fileSizeBytes": 15234567,
    "downloadUrl": "https://evermaildevstorage.blob.core.windows.net/gdpr-exports/...&sig=..."
  }
}
```

- Bundles stay available for 7 days. Callers should download immediately because the SAS is only valid for 15 minutes.
- Every request is audited (`UserDataExportRequested` / `UserDataExportCompleted`).

### GET /users/me/exports/{id}
Return the metadata for a previously requested bundle. Includes a new SAS URL when `status === "Completed"`.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "e6b4f15e-4eb5-4e19-a388-3a566063d8e6",
    "status": "Completed",
    "requestedAt": "2025-11-23T21:10:00Z",
    "completedAt": "2025-11-23T21:10:07Z",
    "expiresAt": "2025-11-30T21:10:07Z",
    "fileSizeBytes": 15234567,
    "downloadUrl": "https://evermaildevstorage.blob.core.windows.net/gdpr-exports/...&sig=..."
  }
}
```

### GET /users/me/exports/{id}/download
Convenience endpoint that always returns a fresh SAS link even if the caller already has the metadata cached.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "downloadUrl": "https://evermaildevstorage.blob.core.windows.net/gdpr-exports/...&sig=...",
    "expiresAt": "2025-11-23T21:25:07Z"
  }
}
```

### DELETE /users/me
Trigger the GDPR "right to be forgotten" workflow for the currently authenticated user.

**Response (202 Accepted)**

```json
{
  "success": true,
  "data": {
    "jobId": "5f488dd4-15e2-4371-a5a8-34b2211ed0a2",
    "status": "Completed",
    "requestedAt": "2025-11-23T21:12:11Z",
    "completedAt": "2025-11-23T21:12:12Z"
  }
}
```

Actions performed:
- Immediately revoke refresh tokens and anonymise the Identity record (scrubbed email/user name, `IsActive = false`, reset security stamp).
- Enqueue `MailboxDeletionQueue` jobs for every mailbox/upload to remove blobs + SQL data.
- Remove user-specific personalization tables (display settings, saved filters, etc.) and anonymise historical audit entries.
- Persist a `UserDeletionJob` row so admins can prove when the request was handled.

---

## Error Responses

All errors follow this format:

```json
{
  "success": false,
  "error": "Human-readable error message",
  "code": "ERROR_CODE",
  "validationErrors": {
    "email": ["Email is required", "Email format is invalid"]
  }
}
```

### HTTP Status Codes
- `400 Bad Request`: Validation error
- `401 Unauthorized`: Missing or invalid JWT token
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found or wrong tenant
- `409 Conflict`: Duplicate resource (e.g., email already registered)
- `413 Payload Too Large`: File size exceeds tier limit
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: Unexpected server error

---

## Rate Limiting

- **Free Tier**: 100 requests/hour
- **Pro Tier**: 1,000 requests/hour
- **Team Tier**: 10,000 requests/hour
- **Enterprise Tier**: Unlimited

**Rate Limit Headers**:
```
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 950
X-RateLimit-Reset: 1699876543
```

---

## SDK Examples

### C# Client (RestSharp)
```csharp
var client = new RestClient("https://api.evermail.com/api/v1");
client.AddDefaultHeader("Authorization", $"Bearer {token}");

var request = new RestRequest("/emails/search", Method.Get);
request.AddParameter("q", "invoice");
request.AddParameter("hasAttachments", "true");

var response = await client.ExecuteAsync<ApiResponse<SearchResult>>(request);
if (response.Data.Success)
{
    foreach (var email in response.Data.Data.Items)
    {
        Console.WriteLine($"{email.Subject} - {email.Date}");
    }
}
```

### JavaScript (Fetch API)
```javascript
const token = localStorage.getItem('evermailToken');

const response = await fetch('https://api.evermail.com/api/v1/emails/search?q=invoice', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  }
});

const result = await response.json();
if (result.success) {
  result.data.items.forEach(email => {
    console.log(`${email.subject} - ${email.date}`);
  });
}
```

---

**Last Updated**: 2025-11-20  
**API Version**: v1  
**Support**: api@evermail.com

