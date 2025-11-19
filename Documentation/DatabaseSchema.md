# Evermail - Database Schema

## Overview

Evermail uses **Azure SQL Serverless** as the primary relational database. The schema is designed for:
- **Multi-tenancy**: Every table has `TenantId` for isolation
- **Performance**: Strategic indexes on frequently queried columns
- **Full-Text Search**: SQL Server Full-Text Search on email content
- **Audit Compliance**: Comprehensive logging for GDPR

### Schema Deployment (Evermail.MigrationService)
- The Aspire AppHost launches `Evermail.MigrationService` before the WebApp or Worker. It runs `dotnet ef database update` using the same connection string so new tables/columns (`MailboxUploads`, `MailboxDeletionQueue`, `ContentHash` indexes, etc.) exist everywhere.
- Manual invocation (optional): `dotnet run --project Evermail.MigrationService`.
- Verify success via Aspire logs: `Waiting for resource 'migrations'` → `Finished waiting for resource 'migrations'`.
- No manual SQL or `dotnet ef database update` is required in CI/CD — `azd deploy` already orchestrates the migration step.

## Entity Relationship Diagram

```
┌──────────────┐
│   Tenants    │
└──────┬───────┘
       │ 1
       │
       │ N
┌──────▼───────┐         ┌──────────────┐
│    Users     │────────▶│  UserRoles   │
└──────┬───────┘ 1     N └──────────────┘
       │
       │ 1
       │
       │ N
┌──────▼───────────┐
│    Mailboxes     │
└──────┬───────────┘
       │ 1
       │
       │ N
┌──────▼──────────────┐       ┌──────────────────┐
│   EmailMessages     │◀──────│   Attachments    │
└─────────────────────┘ 1   N └──────────────────┘

┌─────────────────┐
│ SubscriptionPlans│
└────────┬────────┘
         │ 1
         │
         │ N
    ┌────▼──────────┐
    │ Subscriptions │
    └───────────────┘

┌──────────────┐
│  AuditLogs   │  (standalone, logs all access)
└──────────────┘
```

## Core Tables

### Tenants
Represents a SaaS tenant (customer account, can have multiple users).

```sql
CREATE TABLE Tenants (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(256) NOT NULL,
    Slug NVARCHAR(100) NOT NULL UNIQUE, -- URL-friendly identifier
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    
    -- Subscription
    SubscriptionTier NVARCHAR(50) NOT NULL DEFAULT 'Free', -- Free, Pro, Team, Enterprise
    StripeCustomerId NVARCHAR(255) NULL,
    
    -- Limits
    MaxStorageGB INT NOT NULL DEFAULT 1,
    MaxUsers INT NOT NULL DEFAULT 1,
    
    -- Status
    IsActive BIT NOT NULL DEFAULT 1,
    SuspensionReason NVARCHAR(500) NULL,
    
    INDEX IX_Tenants_Slug (Slug),
    INDEX IX_Tenants_StripeCustomerId (StripeCustomerId)
);
```

**Sample Data**:
```sql
INSERT INTO Tenants (Id, Name, Slug, SubscriptionTier, MaxStorageGB, MaxUsers)
VALUES 
    ('A1B2C3D4-...', 'Acme Corp', 'acme-corp', 'Team', 50, 5),
    ('E5F6G7H8-...', 'John Doe', 'john-doe', 'Free', 1, 1);
```

### Users
User accounts within a tenant.

```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    
    -- Identity
    Email NVARCHAR(256) NOT NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    SecurityStamp NVARCHAR(MAX) NULL,
    
    -- Profile
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    
    -- 2FA
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    TwoFactorSecret NVARCHAR(255) NULL,
    
    -- Timestamps
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2 NULL,
    
    -- Status
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_Users_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    INDEX IX_Users_TenantId (TenantId),
    INDEX IX_Users_Email (Email)
);
```

### UserRoles
Maps users to roles (User, Admin, SuperAdmin).

```sql
CREATE TABLE UserRoles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role NVARCHAR(50) NOT NULL, -- User, Admin, SuperAdmin
    
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX IX_UserRoles_UserId (UserId)
);
```

### Mailboxes
Metadata for uploaded .mbox files.

```sql
CREATE TABLE Mailboxes (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    
    -- User-friendly metadata
    DisplayName NVARCHAR(500) NULL,
    
    -- Latest upload snapshot (for backwards compatibility)
    FileName NVARCHAR(500) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    BlobPath NVARCHAR(1000) NOT NULL, -- mbox-archives/{tenantId}/{mailboxId}/original.mbox
    
    -- Processing Status
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
    ProcessingStartedAt DATETIME2 NULL,
    ProcessingCompletedAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    
    -- Statistics
    TotalEmails INT NOT NULL DEFAULT 0,
    ProcessedEmails INT NOT NULL DEFAULT 0,
    FailedEmails INT NOT NULL DEFAULT 0,
    
    -- Lifecycle flags
    LatestUploadId UNIQUEIDENTIFIER NULL, -- FK -> MailboxUploads.Id (set post creation)
    UploadRemovedAt DATETIME2 NULL,
    UploadRemovedByUserId UNIQUEIDENTIFIER NULL,
    IsPendingDeletion BIT NOT NULL DEFAULT 0,
    SoftDeletedAt DATETIME2 NULL,
    SoftDeletedByUserId UNIQUEIDENTIFIER NULL,
    PurgeAfter DATETIME2 NULL,
    
    -- Timestamps
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    
    CONSTRAINT FK_Mailboxes_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Mailboxes_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX IX_Mailboxes_Tenant_User (TenantId, UserId),
    INDEX IX_Mailboxes_Status (Status),
    INDEX IX_Mailboxes_Purge (IsPendingDeletion, PurgeAfter)
);
```

> `LatestUploadId` is populated only when the mailbox has at least one entry in `MailboxUploads`.

### MailboxUploads

Keeps a history of every upload/re-import tied to a logical mailbox. Enables deleting an upload while keeping the indexed emails.

```sql
CREATE TABLE MailboxUploads (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    MailboxId UNIQUEIDENTIFIER NOT NULL,
    UploadedByUserId UNIQUEIDENTIFIER NOT NULL,
    
    FileName NVARCHAR(500) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    BlobPath NVARCHAR(1000) NOT NULL,
    
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed, Deleted
    ProcessingStartedAt DATETIME2 NULL,
    ProcessingCompletedAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    
    TotalEmails INT NOT NULL DEFAULT 0,
    ProcessedEmails INT NOT NULL DEFAULT 0,
    FailedEmails INT NOT NULL DEFAULT 0,
    
    KeepEmails BIT NOT NULL DEFAULT 0, -- true when upload deleted but emails retained
    DeletedAt DATETIME2 NULL,
    DeletedByUserId UNIQUEIDENTIFIER NULL,
    PurgeAfter DATETIME2 NULL,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_MailboxUploads_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_MailboxUploads_Mailbox FOREIGN KEY (MailboxId) REFERENCES Mailboxes(Id) ON DELETE CASCADE,
    INDEX IX_MailboxUploads_Mailbox (MailboxId),
    INDEX IX_MailboxUploads_Status (Status)
);

ALTER TABLE Mailboxes
    ADD CONSTRAINT FK_Mailboxes_LatestUpload
        FOREIGN KEY (LatestUploadId) REFERENCES MailboxUploads(Id);
```

### EmailMessages
Core email data extracted from .mbox files. As of November 2025 the table also captures
conversation metadata, additional SMTP headers, and a flattened recipient search column so
threading + advanced queries can run without JSON parsing.

```sql
CREATE TABLE EmailMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    MailboxId UNIQUEIDENTIFIER NOT NULL,
    MailboxUploadId UNIQUEIDENTIFIER NULL,
    
    -- Email Headers
    MessageId NVARCHAR(512) NULL, -- SMTP Message-ID header
    InReplyTo NVARCHAR(512) NULL,
    References NVARCHAR(MAX) NULL, -- For threading
    
    Subject NVARCHAR(1024) NULL,
    
    -- Sender / envelope metadata
    FromAddress NVARCHAR(512) NOT NULL,
    FromName NVARCHAR(512) NULL,
    ReplyToAddress NVARCHAR(512) NULL,
    SenderAddress NVARCHAR(512) NULL,
    SenderName NVARCHAR(512) NULL,
    ReturnPath NVARCHAR(512) NULL,
    ListId NVARCHAR(512) NULL,
    ThreadTopic NVARCHAR(1024) NULL,
    Importance NVARCHAR(32) NULL,
    Priority NVARCHAR(32) NULL,
    Categories NVARCHAR(512) NULL,
    
    -- Recipients (still stored as JSON for backwards compatibility)
    ToAddresses NVARCHAR(MAX) NULL, -- JSON array: ["email1", "email2"]
    ToNames NVARCHAR(MAX) NULL,     -- JSON array: ["Name 1", "Name 2"]
    CcAddresses NVARCHAR(MAX) NULL,
    CcNames NVARCHAR(MAX) NULL,
    BccAddresses NVARCHAR(MAX) NULL,
    BccNames NVARCHAR(MAX) NULL,
    RecipientsSearch NVARCHAR(2000) NULL, -- Flattened "to/cc/bcc/reply-to" blob for FTS
    
    -- Date
    Date DATETIME2 NOT NULL,
    
    -- Content
    Snippet NVARCHAR(512) NULL,     -- First 200 chars of text body
    TextBody NVARCHAR(MAX) NULL,
    HtmlBody NVARCHAR(MAX) NULL,
    ContentHash VARBINARY(32) NULL,
    
    -- Metadata
    HasAttachments BIT NOT NULL DEFAULT 0,
    AttachmentCount INT NOT NULL DEFAULT 0,
    IsRead BIT NOT NULL DEFAULT 0,  -- User-specific flag (future: move to UserEmailState table)
    
    -- Threading
    ConversationId UNIQUEIDENTIFIER NULL,
    ConversationKey NVARCHAR(512) NULL,
    ThreadDepth INT NOT NULL DEFAULT 0,
    
    -- Timestamps
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_EmailMessages_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_EmailMessages_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_EmailMessages_Mailbox FOREIGN KEY (MailboxId) REFERENCES Mailboxes(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EmailMessages_MailboxUpload FOREIGN KEY (MailboxUploadId) REFERENCES MailboxUploads(Id) ON DELETE SET NULL,
    CONSTRAINT FK_EmailMessages_Thread FOREIGN KEY (ConversationId) REFERENCES EmailThreads(Id) ON DELETE NO ACTION,
    
    INDEX IX_EmailMessages_Tenant_User (TenantId, UserId),
    INDEX IX_EmailMessages_Mailbox (MailboxId),
    INDEX IX_EmailMessages_Date (Date),
    INDEX IX_EmailMessages_FromAddress (FromAddress),
    INDEX IX_EmailMessages_Subject (Subject),
    INDEX IX_EmailMessages_Conversation (TenantId, ConversationId),
    UNIQUE (TenantId, MailboxId, ContentHash) WHERE ContentHash IS NOT NULL
);

-- Full-Text Search Catalog
CREATE FULLTEXT CATALOG EmailSearchCatalog AS DEFAULT;

CREATE FULLTEXT INDEX ON EmailMessages(Subject, TextBody, HtmlBody, RecipientsSearch, FromName, FromAddress)
    KEY INDEX PK__EmailMessages
    ON EmailSearchCatalog
    WITH STOPLIST = SYSTEM;
```

**Query Filter for Multi-Tenancy (EF Core)**:
```csharp
modelBuilder.Entity<EmailMessage>()
    .HasQueryFilter(e => e.TenantId == _currentTenant.Id);
```

### Attachments
Email attachment metadata (files stored in Blob Storage).

```sql
CREATE TABLE Attachments (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    EmailMessageId UNIQUEIDENTIFIER NOT NULL,
    
    -- File Info
    FileName NVARCHAR(500) NOT NULL,
    ContentType NVARCHAR(255) NOT NULL, -- MIME type
    SizeBytes BIGINT NOT NULL,
    BlobPath NVARCHAR(1000) NOT NULL, -- attachments/{tenantId}/{mailboxId}/{messageId}/{filename}
    
    -- Metadata (for future AI features)
    IsInline BIT NOT NULL DEFAULT 0, -- e.g., embedded images
    ContentId NVARCHAR(255) NULL,    -- For inline attachments
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_Attachments_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Attachments_EmailMessage FOREIGN KEY (EmailMessageId) REFERENCES EmailMessages(Id) ON DELETE CASCADE,
    
    INDEX IX_Attachments_EmailMessage (EmailMessageId),
    INDEX IX_Attachments_Tenant (TenantId)
);
```

### MailboxDeletionQueue

Deferred deletion tasks processed by the background worker. Supports the "recycle bin" retention window (30 days) while letting SuperAdmins purge immediately.

```sql
CREATE TABLE MailboxDeletionQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    MailboxId UNIQUEIDENTIFIER NOT NULL,
    MailboxUploadId UNIQUEIDENTIFIER NULL,
    
    DeleteUpload BIT NOT NULL,
    DeleteEmails BIT NOT NULL,
    RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExecuteAfter DATETIME2 NOT NULL,
    ExecutedAt DATETIME2 NULL,
    ExecutedByUserId UNIQUEIDENTIFIER NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Scheduled', -- Scheduled, Running, Completed, Failed
    Notes NVARCHAR(MAX) NULL,
    
    CONSTRAINT FK_MailboxDeletionQueue_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_MailboxDeletionQueue_Mailbox FOREIGN KEY (MailboxId) REFERENCES Mailboxes(Id) ON DELETE CASCADE,
    CONSTRAINT FK_MailboxDeletionQueue_Upload FOREIGN KEY (MailboxUploadId) REFERENCES MailboxUploads(Id) ON DELETE SET NULL,
    INDEX IX_MailboxDeletionQueue_Status (Status, ExecuteAfter)
);
```

The worker polls rows where `Status='Scheduled' AND ExecuteAfter <= SYSUTCDATETIME()` and performs blob + DB cleanup. SuperAdmins can set `ExecuteAfter = GETUTCDATE()` to bypass the 30-day wait.

## Subscription & Billing

### SubscriptionPlans
Predefined subscription tiers.

```sql
CREATE TABLE SubscriptionPlans (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE, -- Free, Pro, Team, Enterprise
    DisplayName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    
    -- Pricing
    PriceMonthly DECIMAL(10,2) NOT NULL,
    PriceYearly DECIMAL(10,2) NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    
    -- Stripe Integration
    StripePriceIdMonthly NVARCHAR(255) NULL,
    StripePriceIdYearly NVARCHAR(255) NULL,
    
    -- Limits
    MaxStorageGB INT NOT NULL,
    MaxUsers INT NOT NULL,
    MaxMailboxes INT NOT NULL,
    
    -- Features (JSON)
    Features NVARCHAR(MAX) NULL, -- JSON: ["Full-text search", "AI summaries", ...]
    
    IsActive BIT NOT NULL DEFAULT 1,
    DisplayOrder INT NOT NULL DEFAULT 0,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Seed data
INSERT INTO SubscriptionPlans (Name, DisplayName, PriceMonthly, MaxStorageGB, MaxUsers, MaxMailboxes)
VALUES
    ('Free', 'Free Tier', 0.00, 1, 1, 1),
    ('Pro', 'Professional', 9.00, 5, 1, 10),
    ('Team', 'Team', 29.00, 50, 5, 100),
    ('Enterprise', 'Enterprise', 99.00, 500, 50, 1000);
```

### Subscriptions
Active subscriptions per tenant.

```sql
CREATE TABLE Subscriptions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionPlanId UNIQUEIDENTIFIER NOT NULL,
    
    -- Stripe Integration
    StripeSubscriptionId NVARCHAR(255) NOT NULL UNIQUE,
    StripeCustomerId NVARCHAR(255) NOT NULL,
    StripePriceId NVARCHAR(255) NOT NULL,
    
    -- Status
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active', -- Active, Canceled, PastDue, Unpaid
    
    -- Billing Period
    CurrentPeriodStart DATETIME2 NOT NULL,
    CurrentPeriodEnd DATETIME2 NOT NULL,
    CancelAtPeriodEnd BIT NOT NULL DEFAULT 0,
    
    -- Timestamps
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CanceledAt DATETIME2 NULL,
    
    CONSTRAINT FK_Subscriptions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Subscriptions_Plan FOREIGN KEY (SubscriptionPlanId) REFERENCES SubscriptionPlans(Id),
    
    INDEX IX_Subscriptions_Tenant (TenantId),
    INDEX IX_Subscriptions_StripeSubscriptionId (StripeSubscriptionId),
    INDEX IX_Subscriptions_Status (Status)
);
```

### PaymentTransactions
Log of all payment transactions (for reconciliation).

```sql
CREATE TABLE PaymentTransactions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionId UNIQUEIDENTIFIER NULL,
    
    -- Stripe Data
    StripeInvoiceId NVARCHAR(255) NOT NULL,
    StripeChargeId NVARCHAR(255) NULL,
    StripePaymentIntentId NVARCHAR(255) NULL,
    
    -- Transaction Info
    Amount DECIMAL(10,2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    Status NVARCHAR(50) NOT NULL, -- Succeeded, Failed, Refunded
    
    -- Timestamps
    TransactionDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_PaymentTransactions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PaymentTransactions_Subscription FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(Id),
    
    INDEX IX_PaymentTransactions_Tenant (TenantId),
    INDEX IX_PaymentTransactions_StripeInvoiceId (StripeInvoiceId)
);
```

## Audit & Compliance

### AuditLogs
Comprehensive audit log for GDPR compliance and security.

```sql
CREATE TABLE AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NULL, -- NULL for system actions
    
    -- Action
    Action NVARCHAR(100) NOT NULL, -- EmailViewed, MailboxUploaded, MailboxDeleted, DataExported, UserLoggedIn
    ResourceType NVARCHAR(100) NULL, -- EmailMessage, Mailbox, User
    ResourceId UNIQUEIDENTIFIER NULL,
    
    -- Context
    IpAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL,
    Details NVARCHAR(MAX) NULL, -- JSON for additional context
    
    -- Timestamp
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_AuditLogs_Tenant (TenantId),
    INDEX IX_AuditLogs_User (UserId),
    INDEX IX_AuditLogs_Timestamp (Timestamp),
    INDEX IX_AuditLogs_Action (Action)
);
```

**Example Log Entries**:
```sql
-- User viewed an email
INSERT INTO AuditLogs (TenantId, UserId, Action, ResourceType, ResourceId, IpAddress)
VALUES ('tenant-id', 'user-id', 'EmailViewed', 'EmailMessage', 'email-id', '192.168.1.100');

-- User exported data (GDPR)
INSERT INTO AuditLogs (TenantId, UserId, Action, Details)
VALUES ('tenant-id', 'user-id', 'DataExported', '{"format":"zip","sizeBytes":1048576}');
```

Mailbox lifecycle operations MUST emit similar entries. Example when deleting an upload but keeping the indexed emails:

```json
{
  "action": "MailboxUploadDeleted",
  "resourceType": "Mailbox",
  "resourceId": "mailbox-id",
  "details": {
    "uploadId": "upload-id",
    "deleteUpload": true,
    "deleteEmails": false,
    "purgeAfter": "2025-12-18T00:00:00Z"
  }
}
```

## Phase 2 Tables (Future)

### Workspaces
Shared archive spaces for teams.

```sql
CREATE TABLE Workspaces (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
    
    CONSTRAINT FK_Workspaces_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Workspaces_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id) ON DELETE NO ACTION,
    
    INDEX IX_Workspaces_Tenant (TenantId)
);

CREATE TABLE WorkspaceMembers (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    WorkspaceId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Member', -- Owner, Admin, Member, Viewer
    
    JoinedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_WorkspaceMembers_Workspace FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(Id) ON DELETE CASCADE,
    CONSTRAINT FK_WorkspaceMembers_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    
    UNIQUE (WorkspaceId, UserId),
    INDEX IX_WorkspaceMembers_User (UserId)
);

-- Add WorkspaceId to Mailboxes table
ALTER TABLE Mailboxes ADD WorkspaceId UNIQUEIDENTIFIER NULL;
ALTER TABLE Mailboxes ADD CONSTRAINT FK_Mailboxes_Workspace 
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(Id) ON DELETE SET NULL;
```

### EmailEmbeddings
Vector embeddings for AI semantic search.

```sql
CREATE TABLE EmailEmbeddings (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EmailMessageId UNIQUEIDENTIFIER NOT NULL,
    
    -- Embedding Vector (1536 dimensions for text-embedding-3-small)
    Embedding VARBINARY(MAX) NOT NULL, -- Store as binary, convert to/from float[] in app
    
    -- Or use specialized vector type if available (SQL Server 2022+)
    -- EmbeddingVector VECTOR(1536) NOT NULL,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_EmailEmbeddings_EmailMessage FOREIGN KEY (EmailMessageId) 
        REFERENCES EmailMessages(Id) ON DELETE CASCADE,
    
    UNIQUE (EmailMessageId)
);
```

### PIIDetection
Detected personally identifiable information (for GDPR Archive tier).

```sql
CREATE TABLE PIIDetection (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    EmailMessageId UNIQUEIDENTIFIER NOT NULL,
    
    -- Detected Entities
    PIIType NVARCHAR(100) NOT NULL, -- Email, PhoneNumber, SSN, CreditCard, etc.
    Value NVARCHAR(500) NOT NULL,
    Confidence DECIMAL(5,4) NOT NULL, -- 0.0 to 1.0
    
    -- Location in email
    FoundIn NVARCHAR(50) NOT NULL, -- Subject, TextBody, HtmlBody
    
    DetectedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_PIIDetection_EmailMessage FOREIGN KEY (EmailMessageId) 
        REFERENCES EmailMessages(Id) ON DELETE CASCADE,
    
    INDEX IX_PIIDetection_EmailMessage (EmailMessageId),
    INDEX IX_PIIDetection_Tenant_PIIType (TenantId, PIIType)
);
```

### EmailThreads
Normalizes conversation metadata so multiple uploads (or future live imports) can converge on the
same thread, enabling instant grouping + analytics.

```sql
CREATE TABLE EmailThreads (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    ConversationKey NVARCHAR(512) NOT NULL, -- normalized root Message-Id
    RootMessageId NVARCHAR(512) NULL,
    Subject NVARCHAR(1024) NULL,
    ParticipantsSummary NVARCHAR(MAX) NOT NULL, -- JSON array of unique addresses
    FirstMessageDate DATETIME2 NOT NULL,
    LastMessageDate DATETIME2 NOT NULL,
    MessageCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_EmailThreads_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    INDEX IX_EmailThreads_Tenant_Key (TenantId, ConversationKey),
    INDEX IX_EmailThreads_Tenant_LastMessage (TenantId, LastMessageDate DESC)
);
```

Whenever a new email arrives the ingestion worker locates/creates the thread via `ConversationKey`,
updates `MessageCount`, `LastMessageDate`, and merges the unique participant list.

### EmailRecipients
Relational child table that stores every recipient (To/Cc/Bcc/Reply-To/Sender) so filters can run
without scanning JSON strings.

```sql
CREATE TABLE EmailRecipients (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    EmailMessageId UNIQUEIDENTIFIER NOT NULL,
    RecipientType NVARCHAR(16) NOT NULL, -- To, Cc, Bcc, ReplyTo, Sender
    Address NVARCHAR(512) NOT NULL,
    DisplayName NVARCHAR(512) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_EmailRecipients_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EmailRecipients_Email FOREIGN KEY (EmailMessageId) REFERENCES EmailMessages(Id) ON DELETE CASCADE,
    INDEX IX_EmailRecipients_Tenant_Address (TenantId, Address),
    INDEX IX_EmailRecipients_Tenant_Type (TenantId, RecipientType, Address)
);
```

The ingestion worker still writes the legacy JSON arrays for backwards compatibility, but the UI/API
now query `EmailRecipients` (and the `RecipientsSearch` column in `EmailMessages`) for fast
recipient filters.

## Indexes Summary

### Performance-Critical Indexes
```sql
-- Multi-tenant isolation (ALWAYS filtered)
CREATE INDEX IX_EmailMessages_Tenant_User ON EmailMessages(TenantId, UserId);

-- Search by date range
CREATE INDEX IX_EmailMessages_Date ON EmailMessages(Date DESC);

-- Search by sender
CREATE INDEX IX_EmailMessages_FromAddress ON EmailMessages(FromAddress);

-- Mailbox status monitoring
CREATE INDEX IX_Mailboxes_Status ON Mailboxes(Status) WHERE Status IN ('Processing', 'Failed');

-- Audit log queries
CREATE INDEX IX_AuditLogs_Tenant_Timestamp ON AuditLogs(TenantId, Timestamp DESC);
```

### Full-Text Indexes
```sql
-- Email content search (November 2025 refresh)
CREATE FULLTEXT INDEX ON EmailMessages(Subject, TextBody, HtmlBody, RecipientsSearch, FromName, FromAddress)
    KEY INDEX PK__EmailMessages
    ON EmailSearchCatalog;
```

## Query Examples

### Search Emails (Full-Text)
```sql
SELECT TOP 100 
    Id, Subject, FromAddress, FromName, Date, Snippet
FROM EmailMessages
WHERE TenantId = @TenantId
    AND UserId = @UserId
    AND CONTAINS((Subject, TextBody, FromName), @SearchTerm)
ORDER BY Date DESC;
```

### Get Mailbox Statistics
```sql
SELECT 
    m.Id,
    m.FileName,
    m.Status,
    m.TotalEmails,
    m.ProcessedEmails,
    SUM(e.HasAttachments) AS EmailsWithAttachments,
    SUM(a.SizeBytes) / 1024.0 / 1024.0 AS TotalAttachmentsMB
FROM Mailboxes m
LEFT JOIN EmailMessages e ON e.MailboxId = m.Id
LEFT JOIN Attachments a ON a.EmailMessageId = e.Id
WHERE m.TenantId = @TenantId AND m.UserId = @UserId
GROUP BY m.Id, m.FileName, m.Status, m.TotalEmails, m.ProcessedEmails;
```

### Tenant Storage Usage
```sql
SELECT 
    t.Id,
    t.Name,
    t.SubscriptionTier,
    SUM(m.FileSizeBytes) / 1024.0 / 1024.0 / 1024.0 AS StorageUsedGB,
    t.MaxStorageGB,
    (SUM(m.FileSizeBytes) / 1024.0 / 1024.0 / 1024.0) / t.MaxStorageGB * 100 AS PercentageUsed
FROM Tenants t
LEFT JOIN Mailboxes m ON m.TenantId = t.Id
WHERE t.Id = @TenantId
GROUP BY t.Id, t.Name, t.SubscriptionTier, t.MaxStorageGB;
```

## Migration Strategy

### Entity Framework Core Setup
```csharp
public class EmailDbContext : DbContext
{
    private readonly TenantContext _tenantContext;

    public EmailDbContext(DbContextOptions<EmailDbContext> options, TenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global query filter for multi-tenancy
        modelBuilder.Entity<EmailMessage>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        
        modelBuilder.Entity<Mailbox>()
            .HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);

        // Configure relationships, indexes, etc.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmailDbContext).Assembly);
    }
}
```

### Initial Migration Commands
```bash
# Create initial migration
dotnet ef migrations add InitialCreate \
    --project Evermail.Infrastructure \
    --startup-project Evermail.WebApp

# Apply to database
dotnet ef database update \
    --project Evermail.Infrastructure \
    --startup-project Evermail.WebApp

# Generate SQL script (for review)
dotnet ef migrations script \
    --project Evermail.Infrastructure \
    --output migration.sql
```

## Data Retention & Cleanup

### Free Tier Auto-Cleanup
```sql
-- Delete mailboxes older than 30 days for free tier tenants
DELETE FROM Mailboxes
WHERE TenantId IN (
    SELECT Id FROM Tenants WHERE SubscriptionTier = 'Free'
)
AND CreatedAt < DATEADD(DAY, -30, GETUTCDATE());
```

### Audit Log Retention (1 year)
```sql
-- Archive audit logs older than 1 year to cold storage
-- Then delete from hot database
DELETE FROM AuditLogs
WHERE Timestamp < DATEADD(YEAR, -1, GETUTCDATE());
```

---

**Last Updated**: 2025-11-11  
**Schema Version**: 1.0  
**Next Review**: Before Phase 2 implementation (Workspaces, AI features)

