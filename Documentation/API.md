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
    "hasMailbox": false
  }
}
```

Interpretation:
- `hasAdmin` – `true` as soon as at least one user in the tenant has the `Admin` role. Because the first registrant is auto-promoted, this typically starts as `true`.
- `planConfirmed` – `true` after the tenant explicitly selects (or reconfirms) a plan inside the onboarding wizard; backed by `Tenant.OnboardingPlanConfirmedAt`.
- `subscriptionTier` – the current plan name (`Free`, `Pro`, `Team`, `Enterprise`), even if not yet confirmed.
- `encryptionConfigured` – `true` once the tenant has selected Evermail-managed, Azure Key Vault, AWS KMS, or Offline BYOK and supplied the required fields.
- `hasMailbox` – `true` once at least one mailbox is uploaded; completes the onboarding checklist.

The endpoint requires `Admin` or `SuperAdmin` role membership and is consumed by `/` (dashboard) and `/onboarding`.

---

## Mailboxes

### GET /mailboxes
List all mailboxes for the current user.

**Query Parameters**:
- `status` (optional): Filter by status (`Pending`, `Processing`, `Completed`, `Failed`)
- `page` (default: 1): Page number
- `pageSize` (default: 20): Items per page

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

### GET /users/me
Get current user profile.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "twoFactorEnabled": false,
    "tenant": {
      "id": "guid",
      "name": "John's Archive",
      "subscriptionTier": "Pro",
      "storageUsedGB": 2.5,
      "maxStorageGB": 5
    }
  }
}
```

### PATCH /users/me
Update user profile.

**Request Body**:
```json
{
  "firstName": "John",
  "lastName": "Smith"
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "firstName": "John",
    "lastName": "Smith"
  }
}
```

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
Request data export (GDPR compliance).

**Response (202 Accepted)**:
```json
{
  "success": true,
  "data": {
    "exportId": "guid",
    "status": "Pending",
    "message": "Export will be ready in 5-10 minutes. Check back soon."
  }
}
```

### GET /users/me/exports/{id}
Check export status and download.

**Response (200 OK)**:
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "status": "Completed",
    "downloadUrl": "/api/v1/users/me/exports/guid/download",
    "expiresAt": "2025-11-18T10:00:00Z",
    "fileSizeBytes": 10485760
  }
}
```

### DELETE /users/me
Delete account (GDPR "right to be forgotten").

**Response (204 No Content)**

Deletes:
- User account
- All mailboxes and emails
- All attachments from blob storage
- Audit logs (after anonymization)

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

