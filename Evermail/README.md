# Evermail - Aspire Solution

> **.NET 9** Azure Aspire solution for the Evermail SaaS platform

---

## 🏗️ Solution Structure

```
Evermail.sln
├── Evermail.AppHost/              # Aspire orchestrator
├── Evermail.ServiceDefaults/      # Shared Aspire service configurations
├── Evermail.WebApp/               # Blazor Web App (hybrid SSR + WASM)
│   ├── Evermail.WebApp/           # Server project
│   └── Evermail.WebApp.Client/    # WASM client project
├── Evermail.AdminApp/             # Blazor Server admin dashboard
├── Evermail.IngestionWorker/      # Background mbox parser service
├── Evermail.Domain/               # Domain entities and interfaces
├── Evermail.Infrastructure/       # EF Core, Blob, Queue implementations
└── Evermail.Common/               # Shared DTOs and utilities
```

##

 ✅ Phase 0 Complete - Aspire Solution Created

All 7 projects created and configured:
- ✅ AppHost configured with SQL Server + Azure Storage
- ✅ All projects targeting .NET 9.0
- ✅ Service discovery and telemetry configured
- ✅ Ready for local development

---

## 🚀 Quick Start

### Prerequisites

- .NET 9 SDK (9.0.109+)
- Docker Desktop (for SQL and Azurite containers)
- Aspire workload installed

### Run Locally

```bash
# Set DOTNET_ROOT for .NET 9 (if using Homebrew)
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"

# Start all services
cd Evermail.AppHost
dotnet run

# Access Aspire Dashboard
open http://localhost:15000
```

This starts:
- ✅ SQL Server container (localhost:1433)
- ✅ Azurite storage emulator (blob + queue)
- ✅ WebApp (https://localhost:5001)
- ✅ AdminApp (https://localhost:5002)
- ✅ IngestionWorker (background service)
- ✅ Aspire Dashboard (http://localhost:15000)

---

## 📦 Projects

### Evermail.AppHost
**Purpose**: Aspire orchestrator - configures and connects all services

**Key Configuration**:
- SQL Server with `evermaildb` database
- Azure Storage (Azurite locally) with blobs and queues
- Service references between projects
- Health checks and telemetry

### Evermail.WebApp
**Purpose**: User-facing Blazor Web App

**Rendering**:
- Static SSR: Landing pages (fast, SEO)
- Interactive Server: Search, real-time features
- Interactive WASM: Email viewer, rich UI

**Features** (to be implemented):
- User registration and authentication
- Mailbox upload (multiple files)
- Email search and viewer
- Account settings
- Billing portal (Stripe)

### Evermail.AdminApp
**Purpose**: Admin dashboard (Blazor Server)

**Features** (to be implemented):
- Tenant and user management
- Mailbox processing monitoring
- Storage analytics
- Job queue monitoring
- Revenue analytics (Stripe sync)

### Evermail.IngestionWorker
**Purpose**: Background service for mbox parsing

**Responsibilities**:
- Poll Azure Storage Queue for jobs
- Download .mbox files from Blob Storage
- Parse with MimeKit (streaming, batches)
- Store emails in database
- Save attachments to Blob Storage
- Update processing status

### Evermail.Domain
**Purpose**: Domain entities and interfaces

**Contains**:
- Entity classes (Tenant, User, Mailbox, EmailMessage, Attachment, etc.)
- Domain interfaces
- Value objects
- Domain events (future)

### Evermail.Infrastructure
**Purpose**: Infrastructure implementations

**Contains**:
- EmailDbContext (EF Core)
- Repository implementations
- Blob storage service
- Queue service
- External API clients

### Evermail.Common
**Purpose**: Shared DTOs and utilities

**Contains**:
- DTOs (EmailDto, MailboxDto, etc.)
- API response models
- Constants
- Extension methods
- Utilities

### Evermail.ServiceDefaults
**Purpose**: Shared Aspire configurations

**Provides**:
- Service discovery
- Resilience (retries, circuit breaker)
- OpenTelemetry (logs, metrics, traces)
- Health checks

---

## 🔧 Development

### Prerequisites Check

```bash
# Verify .NET 9
dotnet --version
# Should show: 9.0.109 or higher

# Verify Aspire workload
dotnet workload list
# Should show: aspire

# Verify Docker
docker --version
```

### Build Solution

```bash
dotnet build
```

### Run Tests (when implemented)

```bash
dotnet test
```

### Database Migrations (when implemented)

```bash
cd Evermail.Infrastructure
dotnet ef migrations add MigrationName -s ../Evermail.WebApp/Evermail.WebApp
dotnet ef database update -s ../Evermail.WebApp/Evermail.WebApp
```

---

## 🌐 Aspire Dashboard

When you run `dotnet run` from AppHost, the Aspire Dashboard opens automatically.

**Dashboard Features**:
- View all running services
- Monitor logs in real-time
- View traces and metrics
- Check health status
- Manage service lifecycle (start/stop/restart)

**URL**: http://localhost:15000

---

## 🔗 Service Endpoints (Local)

| Service | URL | Purpose |
|---------|-----|---------|
| **WebApp** | https://localhost:5001 | User frontend |
| **AdminApp** | https://localhost:5002 | Admin dashboard |
| **API** | https://localhost:5001/api/v1 | REST API |
| **Aspire Dashboard** | http://localhost:15000 | Monitoring |
| **SQL Server** | localhost:1433 | Database |
| **Azurite Blobs** | localhost:10000 | Blob storage emulator |
| **Azurite Queues** | localhost:10001 | Queue emulator |

---

## 📚 Next Steps

See [MVP_TODOLIST.md](../MVP_TODOLIST.md) for complete development plan.

**Phase 0 - Remaining Tasks**:
- [ ] Add EF Core to Infrastructure project
- [ ] Create domain entities
- [ ] Configure EmailDbContext
- [ ] Create initial migration
- [ ] Implement authentication (JWT, 2FA, OAuth)
- [ ] Add project references between layers

**Start with**:
```
"Add EF Core to Evermail.Infrastructure and create the Tenant entity 
following DatabaseSchema.md and multi-tenancy rules"
```

---

## 🐛 Troubleshooting

### DOTNET_ROOT Error

If you see `.NET SDK does not support targeting .NET 9.0`:

```bash
# Add to ~/.zshrc or ~/.bashrc
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"

# Or use for current session
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
```

### Aspire Workload Missing

```bash
dotnet workload install aspire
```

### Docker Not Running

```bash
# Start Docker Desktop
open -a Docker
```

---

**Created**: 2025-11-11  
**Framework**: .NET 9.0.109  
**Aspire**: **13.0.0** (latest)  
**Status**: ✅ Build successful - Ready for Phase 0 development

