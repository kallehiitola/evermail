# Evermail

> Modern email archive viewer and search platform powered by AI

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Azure Aspire](https://img.shields.io/badge/Azure-Aspire-blue)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Evermail is a cloud-based SaaS platform that enables users to upload, view, search, and analyze email archives from `.mbox` files. Built for individuals, small businesses, and enterprises needing powerful email archiving with AI-powered search capabilities.

## ✨ Features

### Current (MVP)
- 📦 **Upload & Parse .mbox Files** - Import mailbox archives from Gmail, Thunderbird, Apple Mail
- 🔍 **Full-Text Search** - Fast search across subjects, senders, and email content
- 📧 **Email Viewer** - Beautiful HTML rendering with attachment support
- 👤 **Multi-User** - Secure multi-tenant architecture with role-based access
- 💳 **Subscription Billing** - Integrated Stripe payment processing
- 🔐 **Security** - 2FA, encryption at rest/transit, GDPR compliance

### Planned (Phase 2+)
- 🤖 **AI-Powered Search** - Natural language queries and semantic search
- 📊 **Email Summaries** - GPT-4 powered mailbox and thread summaries
- 📥 **Gmail/Outlook Import** - Direct OAuth import from cloud email providers
- 👥 **Shared Workspaces** - Team collaboration on shared archives
- 📜 **GDPR Archive** - Immutable storage for compliance requirements
- 🔌 **API Access** - RESTful API for programmatic access

## 🏗️ Architecture

Evermail is built with modern .NET and Azure technologies:

- **Frontend**: Blazor WebAssembly with MudBlazor
- **Backend**: ASP.NET Core 8 with Minimal APIs
- **Database**: Azure SQL Serverless with Full-Text Search
- **Storage**: Azure Blob Storage + Azure Storage Queues
- **Orchestration**: Azure Aspire
- **Deployment**: Azure Container Apps
- **Email Parsing**: MimeKit
- **Payment**: Stripe
- **AI**: Azure OpenAI (Phase 2)

See [Architecture Documentation](Documentation/Architecture.md) for detailed system design.

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (for deployment)
- Visual Studio 2022 17.9+ or VS Code with C# Dev Kit

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/evermail.git
   cd evermail
   ```

2. **Install Azure Aspire workload**
   ```bash
   dotnet workload update
   dotnet workload install aspire
   ```

3. **Configure user secrets**
   ```bash
   cd Evermail.AppHost
   
   # Storage (local development uses Azurite)
   dotnet user-secrets set "ConnectionStrings:storage" "UseDevelopmentStorage=true"
   
   # SQL Server
   dotnet user-secrets set "ConnectionStrings:evermaildb" "Server=localhost,1433;Database=Evermail;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true"
   
   # Stripe (test keys from https://dashboard.stripe.com/test/apikeys)
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
   ```

4. **Start the application**
   ```bash
   dotnet run
   ```
   
   This will start all services and open the Aspire Dashboard at `http://localhost:15000`

5. **Apply database migrations**
   ```bash
   cd ../Evermail.WebApp
   dotnet ef database update --project ../Evermail.Infrastructure
   ```

6. **Access the application**
   - User App: http://localhost:5000
   - Admin Dashboard: http://localhost:5001
   - Aspire Dashboard: http://localhost:15000

## 📦 Project Structure

```
evermail/
├── Evermail.AppHost/              # Aspire orchestrator
├── Evermail.WebApp/               # User-facing Blazor WASM + APIs
├── Evermail.AdminApp/             # Admin dashboard (Blazor Server)
├── Evermail.IngestionWorker/      # Background mbox parser
├── Evermail.SearchIndexer/        # Azure AI Search sync (Phase 2)
├── Evermail.Domain/               # Domain entities and interfaces
├── Evermail.Infrastructure/       # EF Core, Blob, Queue implementations
├── Evermail.Common/               # Shared DTOs and utilities
├── Documentation/                 # Architecture and design docs
│   ├── Architecture.md
│   ├── API.md
│   ├── DatabaseSchema.md
│   ├── Deployment.md
│   ├── Security.md
│   └── Pricing.md
└── tests/
    ├── Evermail.UnitTests/
    └── Evermail.IntegrationTests/
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=./coverage

# Run integration tests only
dotnet test --filter "Category=Integration"

# Run E2E tests (requires Playwright)
cd tests/Evermail.E2ETests
playwright install
dotnet test
```

## 🚢 Deployment

### Deploy to Azure (Recommended)

Using Azure Developer CLI (azd):

```bash
# Initialize
azd init

# Provision infrastructure
azd provision

# Deploy application
azd deploy

# Apply database migrations
dotnet ef database update --connection "<azure-connection-string>"
```

See [Deployment Guide](Documentation/Deployment.md) for detailed instructions.

### Manual Deployment

See [Deployment Guide](Documentation/Deployment.md) for Bicep templates and CI/CD setup.

## 💰 Pricing

| Tier | Price | Storage | Features |
|------|-------|---------|----------|
| **Free** | €0/month | 1 GB | Basic search, 30-day retention |
| **Pro** | €9/month | 5 GB | AI search, 1-year retention, Gmail import |
| **Team** | €29/month | 50 GB | 5 users, shared workspaces, API access |
| **Enterprise** | €99/month | 500 GB | 50 users, GDPR archive, priority support |

See [Pricing Documentation](Documentation/Pricing.md) for detailed breakdown and business model.

## 📖 Documentation

- [Architecture Overview](Documentation/Architecture.md) - System design and component architecture
- [API Reference](Documentation/API.md) - REST API endpoints and examples
- [Database Schema](Documentation/DatabaseSchema.md) - Entity models and relationships
- [Deployment Guide](Documentation/Deployment.md) - Local development and Azure deployment
- [Security](Documentation/Security.md) - Authentication, authorization, and compliance
- [Pricing & Business Model](Documentation/Pricing.md) - Subscription tiers and unit economics

## 🔐 Security

Evermail takes security seriously:

- 🔒 **Encryption at rest** (Azure SQL TDE, Blob Storage SSE)
- 🔐 **Encryption in transit** (TLS 1.3)
- 🛡️ **Multi-tenant isolation** (database query filters)
- 🔑 **Strong authentication** (ASP.NET Identity + 2FA)
- 🕵️ **Audit logging** (GDPR compliance)
- 🔍 **Input validation** (SQL injection, XSS prevention)

See [Security Documentation](Documentation/Security.md) for comprehensive security practices.

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Standards

- Follow `.cursorrules` coding conventions
- Write unit tests for business logic
- Update documentation for API changes
- Run `dotnet format` before committing
- Ensure all tests pass

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [MimeKit](https://github.com/jstedfast/MimeKit) - Excellent .mbox parsing library
- [Azure Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) - Cloud-native orchestration
- [MudBlazor](https://mudblazor.com/) - Material Design components for Blazor
- [Stripe](https://stripe.com/) - Payment processing

## 📧 Support

- **Email**: support@evermail.com
- **Documentation**: [docs.evermail.com](https://docs.evermail.com)
- **Issues**: [GitHub Issues](https://github.com/yourusername/evermail/issues)
- **Discord**: [Join our community](https://discord.gg/evermail)

## 🗺️ Roadmap

### Q1 2025 - MVP Launch
- [x] Core email parsing and storage
- [x] Full-text search
- [x] User authentication
- [x] Stripe integration
- [ ] Beta launch with 10 users

### Q2 2025 - AI Features
- [ ] Azure OpenAI integration
- [ ] Semantic search
- [ ] Email summaries
- [ ] Gmail/Outlook OAuth import

### Q3 2025 - Team Features
- [ ] Shared workspaces
- [ ] Team management
- [ ] API access
- [ ] Enhanced admin dashboard

### Q4 2025 - Enterprise
- [ ] GDPR Archive tier
- [ ] Immutable storage
- [ ] Advanced compliance features
- [ ] On-premise option

---

**Made with ❤️ using .NET and Azure Aspire**

---

## Quick Links

- 🌐 [Website](https://evermail.com)
- 📚 [Documentation](https://docs.evermail.com)
- 💬 [Community](https://discord.gg/evermail)
- 🐦 [Twitter](https://twitter.com/evermailapp)
- 📰 [Blog](https://blog.evermail.com)

