# Evermail - Project Instructions

> 📖 **For complete project overview and business case, see [PROJECT_BRIEF.md](PROJECT_BRIEF.md)**

## Project Overview

Evermail is a **SaaS platform** for viewing, searching, and managing email archives from .mbox files. Built with:

- **Platform**: Microsoft Azure
- **Language**: C# **.NET 9**
- **Deployment**: Azure Aspire 9.4
- **Database**: Azure SQL Serverless (+ Elastic Pools for scale)
- **Storage**: Azure Blob Storage
- **Payment**: Stripe
- **Frontend (Web)**: Blazor Web App (hybrid SSR + Interactive WASM)
- **Frontend (Mobile)**: .NET MAUI Blazor Hybrid (Phase 2)

## Core Principles

### 1. Multi-Tenancy First
- **EVERY entity MUST have a `TenantId` property**
- ALL queries MUST filter by current tenant
- Use EF Core global query filters for automatic tenant isolation

### 2. Clean Architecture
- Follow Domain-Driven Design (DDD)
- Dependencies flow inward: Infrastructure → Application → Domain
- Use CQRS pattern for complex operations

### 3. Security by Default
- Use ASP.NET Core Identity with 2FA
- Encrypt at rest (TDE, SSE) and in transit (TLS 1.3)
- Store secrets in Azure Key Vault
- Audit all sensitive operations

### 4. Modern C# Conventions
- Use C# 13 features (.NET 9 - file-scoped namespaces, records, params collections)
- Async/await consistently with `Async` suffix
- Nullable reference types enabled
- Follow Microsoft naming conventions
- Leverage .NET 9 performance improvements

## Development Standards

### Document-Driven Development
**CRITICAL**: ALWAYS check and update Documentation/ folder BEFORE implementing features:

1. **Check existing documentation FIRST**
   - Read relevant files in `Documentation/` before coding
   - Identify where new content should go

2. **Update existing docs, don't create duplicates**
   - Add to appropriate existing document
   - Only create new doc if truly necessary

3. **Update docs BEFORE writing code**
   - Document the design first
   - Implement following the updated documentation

4. **Never create duplicate documentation**
   - Check if information already exists
   - Update existing docs rather than creating new files

### Communication Style
- Be concise and actionable
- Provide code examples over explanations
- Reference specific documentation files when needed

### Code Style
- PascalCase: classes, methods, properties
- camelCase: local variables, parameters
- _camelCase: private fields (underscore prefix)

### Documentation References
When implementing features, CHECK THESE FIRST:
- `Documentation/Architecture.md` - System design
- `Documentation/DatabaseSchema.md` - Entity models
- `Documentation/Security.md` - Auth & encryption
- `Documentation/API.md` - Endpoint patterns
- `Documentation/Deployment.md` - Infrastructure
- `Documentation/Pricing.md` - Business model

## Project Structure

```
Evermail.AppHost/              # Aspire orchestrator
Evermail.WebApp/               # User-facing Blazor WASM + APIs
Evermail.AdminApp/             # Admin dashboard
Evermail.IngestionWorker/      # Background mbox parser
Evermail.Domain/               # Domain entities
Evermail.Infrastructure/       # EF Core, Blob, Queue
Evermail.Common/               # DTOs, utilities
```

## Key Technologies

- **Email Parsing**: MimeKit (stream mbox files, never load fully)
- **Database**: EF Core with SQL Server Full-Text Search
- **Storage**: Azure Blob Storage with SAS tokens
- **Queue**: Azure Storage Queues for background jobs
- **Payment**: Stripe with webhook verification

## Business Context

This is a **side-hustle SaaS**:
- Keep it simple, ship fast
- Break-even at 7-20 paying users
- 90%+ gross margins
- MVP → Beta → Paid plans in 8-12 weeks

**Remember**: Solve real problems. Don't over-engineer. Get to paying customers quickly.

