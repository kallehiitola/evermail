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
        "fileName": "gmail-export.mbox",
        "fileSizeBytes": 52428800,
        "status": "Completed",
        "totalEmails": 1523,
        "processedEmails": 1523,
        "failedEmails": 0,
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

### DELETE /mailboxes/{id}
Delete a mailbox and all associated emails/attachments.

**Response (204 No Content)**

---

## Emails

### GET /emails/search
Search emails with full-text query.

**Query Parameters**:
- `q` (required): Search query (supports full-text syntax)
- `mailboxId` (optional): Filter by specific mailbox
- `from` (optional): Filter by sender email
- `dateFrom` (optional): ISO 8601 date, e.g., `2025-01-01`
- `dateTo` (optional): ISO 8601 date
- `hasAttachments` (optional): `true` or `false`
- `page` (default: 1)
- `pageSize` (default: 50, max: 100)
- `sortBy` (default: `date`): `date`, `subject`, `from`
- `sortOrder` (default: `desc`): `asc`, `desc`

**Example Request**:
```
GET /emails/search?q=invoice&dateFrom=2025-01-01&hasAttachments=true&page=1&pageSize=20
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
        "snippet": "Thank you for your payment. Attached is your invoice...",
        "hasAttachments": true,
        "attachmentCount": 1,
        "isRead": false
      }
    ],
    "totalCount": 42,
    "page": 1,
    "pageSize": 20,
    "queryTime": 0.123
  }
}
```

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

**Last Updated**: 2025-11-11  
**API Version**: v1  
**Support**: api@evermail.com

