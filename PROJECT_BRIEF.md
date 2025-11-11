# Evermail - Project Brief

> **The Complete Guide to Evermail**  
> For new developers, contributors, and AI agents

---

## 📧 What is Evermail?

**Evermail** is a cloud-based SaaS platform that enables users to **upload, view, search, and analyze email archives** from `.mbox` files.

### The Problem We Solve

**Problem**: People have email archives (`.mbox` files from Gmail exports, Thunderbird, Apple Mail) sitting on their hard drives with no easy way to:
- View emails in a modern interface
- Search through years of correspondence
- Find specific conversations or attachments
- Access from any device
- Understand what's in them (AI summaries)

**Current Alternatives Are Bad**:
- ❌ **Local tools** (Thunderbird, Apple Mail) - Clunky, desktop-only, no cloud sync
- ❌ **Enterprise solutions** (MailStore, Barracuda) - €50-100/user/year, overkill for individuals
- ❌ **Gmail search** - Limited history, no advanced features, privacy concerns
- ❌ **Manual inspection** - Time-consuming, inefficient

### Our Solution

**Evermail provides**:
- ✅ **Upload .mbox files** - Drag and drop or Gmail/Outlook direct import
- ✅ **Modern web interface** - Beautiful Blazor UI with MudBlazor
- ✅ **Powerful search** - Full-text search across all emails
- ✅ **AI-powered features** - Summaries, semantic search, entity extraction
- ✅ **Mobile apps** (Phase 2) - Access from anywhere
- ✅ **Team collaboration** - Shared archives for businesses
- ✅ **GDPR compliance** - For regulated industries

### Target Audience

1. **Individuals** (Free/Pro tier)
   - Freelancers needing to search old project emails
   - People archiving personal correspondence
   - Anyone with Gmail Takeout archives

2. **Small Businesses** (Team tier)
   - Companies archiving support@ mailboxes
   - Small law firms needing email discovery
   - HR departments archiving employee emails

3. **Enterprises** (Enterprise tier)
   - Regulated industries (finance, healthcare, legal)
   - Companies needing GDPR-compliant archiving
   - Organizations with compliance requirements

---

## 💰 Business Case - Why This Makes Sense

### Unit Economics (The Math)

#### Infrastructure Costs (Monthly)

**MVP (0-100 users)**:
- Azure SQL Serverless: €15-30/month (auto-pause when idle)
- Azure Blob Storage: €5-10/month
- Azure Container Apps: €40-60/month
- Other (queues, insights): €5-10/month
- **Total**: €65-110/month

**At Scale (1000 users)**:
- Infrastructure: €180-250/month
- Stripe fees: 2.9% + €0.30/transaction
- **Variable cost per user**: ~€1.50/month

#### Revenue Model

| Tier | Price | Target % | Example (100 users) |
|------|-------|----------|---------------------|
| **Free** | €0 | 50% | 50 users = €0 |
| **Pro** | €9/month | 30% | 30 users = €270/month |
| **Team** | €29/month | 15% | 15 users = €435/month |
| **Enterprise** | €99/month | 5% | 5 users = €495/month |
| **Total** | - | 100 users | **€1,200/month revenue** |

**Gross Margin**: (€1,200 - €110 infra - €35 Stripe fees) / €1,200 = **88%**

#### Break-Even Analysis

**Fixed costs**: €100/month  
**Average revenue per paying user (ARPU)**: €15/month (blended across tiers)

**Break-even formula**:
```
Paying Users Needed = Fixed Cost / ARPU
                     = €100 / €15
                     = 7 paying users
```

**7 paying users at €15 average = €105/month revenue**

✅ **Break-even at just 7-20 paying users** (very achievable!)

#### Lifetime Value (LTV)

**Assumptions**:
- ARPU: €15/month
- Average customer lifetime: 24 months
- Gross margin: 88%

**LTV = €15 × 24 × 0.88 = €316**

#### Customer Acquisition Cost (CAC)

**Channels**:
- Organic (SEO, content): €0-10 per user
- Paid ads: €20-50 per user
- Referrals: €5 per user

**Target CAC**: €25 per paying user

**LTV:CAC Ratio**: €316 / €25 = **12.6:1** (Excellent! Healthy SaaS is 3:1)

### Why This Business Model Works

1. **Low Fixed Costs** ✅
   - €100/month infrastructure
   - No office, no employees initially
   - Solo founder or small team

2. **High Gross Margins** ✅
   - 88-90% typical SaaS margins
   - Scalable infrastructure
   - Minimal variable costs

3. **Fast Break-Even** ✅
   - 7-20 users to profitability
   - Achievable in 1-3 months
   - Low risk for side-hustle

4. **Scalable Economics** ✅
   - Margins improve with scale
   - 100 users = €1,200 MRR (88% margin)
   - 1000 users = €12,000 MRR (90% margin)

5. **Multiple Revenue Opportunities** ✅
   - Subscriptions (primary)
   - Mobile app (Phase 2)
   - AI add-ons (Phase 2)
   - API access (Team/Enterprise)
   - White-label (future)

### Competitive Positioning

| Competitor | Price | Target | Strength | Weakness |
|------------|-------|--------|----------|----------|
| **Evermail** | €0-99/mo | Individuals + SMBs | Modern UI, AI, affordable | New entrant |
| **MailStore** | €50-100/user/year | Enterprises | Mature, on-premise option | Expensive, dated UI |
| **Barracuda** | €100+/user/year | Enterprises | Strong compliance | Very expensive |
| **CloudHQ** | $10/month | Individuals | Simple Gmail backup | Limited search, no AI |
| **Gmail Built-in** | $2-10/month | Individuals | Integrated, cheap | No archiving focus |

**Evermail's Position**: Premium features at mid-tier pricing, with AI differentiation

---

## 🎯 Vision & Strategy

### Phase 1: MVP (Weeks 1-4)
**Goal**: Validate core value proposition

**Features**:
- ✅ Upload .mbox files
- ✅ Full-text search
- ✅ Email viewer
- ✅ User authentication
- ✅ Stripe Free + Pro tiers

**Success Metric**: 10 beta users, 3 paying users

### Phase 2: Beta Launch (Weeks 5-6)
**Goal**: Refine product-market fit

**Features**:
- ✅ Admin dashboard
- ✅ Usage analytics
- ✅ Team tier launch
- ✅ Gmail/Outlook OAuth import

**Success Metric**: 50 total users, 10 paying users (break-even)

### Phase 3: Growth (Weeks 7-12)
**Goal**: Scale to profitability

**Features**:
- ✅ AI-powered search and summaries
- ✅ Shared workspaces
- ✅ Mobile app (.NET MAUI)
- ✅ API access

**Success Metric**: 200 users, 50 paying users, €750 MRR

### Phase 4: Scale (Year 2)
**Goal**: Sustainable business

**Features**:
- ✅ Enterprise tier
- ✅ GDPR Archive (compliance)
- ✅ Multi-region deployment
- ✅ White-label option

**Success Metric**: 1000 users, 300 paying users, €4,500 MRR

---

## 🏗️ Technical Architecture (Simplified)

### What Makes It Work

```
User uploads .mbox file
         ↓
Azure Blob Storage (permanent archive)
         ↓
Background worker picks up job
         ↓
MimeKit parses email by email (streaming, never loads full file)
         ↓
Stores in Azure SQL (metadata, body text)
         ↓
Attachments saved to Blob Storage
         ↓
Full-Text Search index created
         ↓
User searches via web app (Blazor)
         ↓
Results returned from SQL FTS
         ↓
AI summaries (Phase 2) via Azure OpenAI
```

### Technology Stack (Simple Version)

| Component | Technology | Why |
|-----------|-----------|-----|
| **Language** | C# (.NET 9) | Mature, performant, great tooling |
| **Frontend** | Blazor Web App | Modern UI, works on web + mobile |
| **Database** | Azure SQL Serverless | Auto-pause saves money, full-text search built-in |
| **Storage** | Azure Blob Storage | Cheap, scalable, reliable |
| **Email Parser** | MimeKit | Industry-standard, battle-tested |
| **Payment** | Stripe | Best SaaS payment solution |
| **Deployment** | Azure Aspire | Modern orchestration, easy deployment |

### Multi-Tenancy (Simple Explanation)

**Every table has a `TenantId` column**:
```sql
-- Example: EmailMessages table
CREATE TABLE EmailMessages (
    Id UNIQUEIDENTIFIER,
    TenantId NVARCHAR(64),  -- ← This isolates tenants
    UserId NVARCHAR(64),
    Subject NVARCHAR(1024),
    FromAddress NVARCHAR(512),
    -- ... other columns
)

-- Every query automatically filters by TenantId
SELECT * FROM EmailMessages WHERE TenantId = 'tenant-123'
```

**Benefits**:
- ✅ One database for all tenants (cost-efficient)
- ✅ Complete data isolation (security)
- ✅ Easy to scale
- ✅ Simple to manage

**Scale strategy**:
- **0-100 users**: Shared database (€15-30/month)
- **100-1000 users**: Elastic pools (€100-200/month)
- **1000+ users**: Sharding (€300-500/month)

---

## 📊 Key Metrics & Goals

### Technical Metrics

| Metric | Target | Why |
|--------|--------|-----|
| **Mailbox processing time** | <1 min per 100MB | User satisfaction |
| **Search latency** | <500ms | Responsive UI |
| **Uptime** | >99.5% | Reliability |
| **Zero data loss** | 100% | Trust |

### Business Metrics

| Metric | Target | Timeline |
|--------|--------|----------|
| **Break-even** | 7-20 paying users | Month 2 |
| **First 100 users** | 50 total, 15 paying | Month 3 |
| **Profitability** | 100 paying users, €1,500 MRR | Month 6 |
| **Sustainable** | 300 paying users, €4,500 MRR | Month 12 |

### Conversion Metrics

| Funnel Stage | Target % | Strategy |
|--------------|----------|----------|
| **Signup** | 100% | Free tier, no credit card |
| **Active use** | 50% | Upload at least 1 mailbox |
| **Free → Paid** | 3-5% | 30-day retention limit, AI feature teasers |
| **Churn** | <5%/month | Value delivery, annual plans |

---

## 🎨 Product Philosophy

### Core Values

1. **Simple** - No bloat, solve real problems
2. **Fast** - Ship MVP in 4 weeks, iterate based on feedback
3. **Affordable** - €9/month accessible to individuals
4. **Private** - Your email data, your control
5. **Modern** - Beautiful UI, AI-powered

### What We're NOT Building

- ❌ Not an email client (use Gmail/Outlook for that)
- ❌ Not a backup service (use native tools)
- ❌ Not enterprise-only (start with individuals)
- ❌ Not over-engineered (keep it simple)

### What We ARE Building

- ✅ Email archive viewer (specialized tool)
- ✅ Powerful search (full-text + AI)
- ✅ Accessible pricing (€0-99/month)
- ✅ Modern SaaS (web + mobile)

---

## 🚀 Why This is a Great Side-Hustle

### 1. Low Risk ✅
- **Break-even at 7 users** - Achievable quickly
- **€100/month fixed costs** - Minimal burn rate
- **Solo founder viable** - No team needed initially
- **Can quit day job at 100 paying users** (€1,500 MRR)

### 2. High Margins ✅
- **88-90% gross margins** - Typical SaaS economics
- **Scalable infrastructure** - Azure auto-scales
- **No COGS** - Pure software, no physical goods

### 3. Clear Value Proposition ✅
- **Solves real problem** - People have mbox files they can't use
- **Underserved market** - No good affordable solutions
- **Growing need** - More people exporting from Gmail/Outlook

### 4. Technical Feasibility ✅
- **Proven technology stack** - .NET 9, Azure, Aspire
- **MimeKit handles complexity** - Email parsing solved
- **Microsoft-validated patterns** - Documented approaches
- **4-8 week MVP** - Fast time to market

### 5. Scalability ✅
- **7 users → break-even**
- **100 users → €1,200 MRR** (profitable side-hustle)
- **1000 users → €12,000 MRR** (full-time income)
- **10,000 users → €120,000 MRR** (real business)

---

## 🧮 Financial Model (Detailed)

### Revenue Projections (Conservative)

| Month | Total Users | Paying Users | Conv Rate | MRR | Costs | Profit |
|-------|-------------|--------------|-----------|-----|-------|--------|
| **1** | 20 | 2 | 10% | €18 | €100 | -€82 |
| **2** | 50 | 10 | 20% | €100 | €100 | €0 ✅ Break-even |
| **3** | 100 | 25 | 25% | €300 | €110 | €190 |
| **6** | 300 | 90 | 30% | €1,200 | €140 | €1,060 |
| **12** | 1000 | 300 | 30% | €4,500 | €250 | €4,250 |

### Cost Structure (Per 100 Users)

**Fixed Costs** (MVP):
- Azure SQL Serverless: €25/month
- Container Apps: €50/month
- Storage & Queue: €10/month
- Monitoring: €10/month
- **Total Fixed**: €95/month

**Variable Costs**:
- Storage (50GB avg): €0.50/month
- Compute (processing): €5/month
- Stripe fees (2.9%): €35/month on €1,200 revenue
- **Total Variable**: €40/month

**Gross Margin**: (€1,200 - €95 - €40) / €1,200 = **88.8%**

### Capital Requirements

**Initial investment**: €0 (use free Azure credits)  
**Monthly burn** (pre-revenue): €100/month  
**Runway needed**: 2-3 months to break-even = **€300 total**

**This is achievable as a side-hustle!**

---

## 🎯 Market Opportunity

### Market Size (TAM/SAM/SOM)

**TAM (Total Addressable Market)**:
- 4 billion email users worldwide
- 10% have exported email archives = 400 million potential users
- @ €9/month average = **€3.6 billion/year market**

**SAM (Serviceable Addressable Market)**:
- English-speaking markets (US, UK, EU, AU)
- Individuals + SMBs with email archiving needs
- Estimated: 50 million users
- @ €9/month = **€450 million/year**

**SOM (Serviceable Obtainable Market)**:
- Realistic capture in 3-5 years: 0.1% of SAM
- 50,000 users
- @ €15/month average = **€750,000/year**

**Year 1 Goal**: 1,000 users, 300 paying = €4,500 MRR = **€54,000/year**

### Competitive Advantages

1. **Modern UI** ✅ - Better than desktop tools
2. **AI-powered** ✅ - Unique differentiation
3. **Affordable** ✅ - 10x cheaper than enterprise solutions
4. **Cloud-based** ✅ - Access from anywhere
5. **Privacy-focused** ✅ - Your data, your control
6. **Developer-friendly** ✅ - API access

---

## 🏛️ Technical Architecture (Overview)

### System Components

```
┌─────────────────────────────────────────────────────┐
│              Users (Web + Mobile)                    │
└──────────────┬──────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────┐
│  Evermail.WebApp (Blazor Web App)                   │
│  - Upload .mbox files                               │
│  - Search emails (full-text)                        │
│  - View emails + attachments                        │
│  - Manage billing (Stripe)                          │
└──────┬──────────────┬──────────────┬────────────────┘
       │              │              │
┌──────▼─────┐  ┌─────▼──────┐  ┌───▼────────────────┐
│ Azure      │  │ Azure SQL  │  │  Azure Blob Storage│
│ Queue      │  │ Serverless │  │  - .mbox files     │
│            │  │            │  │  - Attachments     │
└──────┬─────┘  └────────────┘  └────────────────────┘
       │
┌──────▼─────────────────────────┐
│  Evermail.IngestionWorker      │
│  - Parse .mbox with MimeKit    │
│  - Extract emails              │
│  - Store in database           │
│  - Save attachments to blob    │
└────────────────────────────────┘
```

### Data Flow

1. **Upload**: User uploads .mbox → Azure Blob Storage
2. **Queue**: Job message sent to Azure Storage Queue
3. **Process**: Worker downloads blob, parses with MimeKit (streaming)
4. **Store**: Emails → Azure SQL, Attachments → Blob Storage
5. **Index**: SQL Full-Text Search catalog created
6. **Search**: User searches → SQL FTS returns results
7. **View**: User views email → Rendered with Blazor

### Multi-Tenancy (Simple)

**Every entity has `TenantId`**:
- Users can only see their own tenant's data
- Database query filters enforce isolation
- Blob paths include tenant ID

**Example**: `mbox-archives/{tenantId}/{mailboxId}/original.mbox`

---

## 🔐 Security & Compliance

### Security Features

- ✅ **Encryption at rest** - Azure SQL TDE, Blob Storage SSE
- ✅ **Encryption in transit** - TLS 1.3
- ✅ **Authentication** - ASP.NET Core Identity + 2FA
- ✅ **Authorization** - JWT tokens, role-based access
- ✅ **Multi-tenant isolation** - Database query filters
- ✅ **Secrets management** - Azure Key Vault
- ✅ **Audit logging** - All sensitive operations logged

### GDPR Compliance

- ✅ **Right to access** - Export all data as ZIP
- ✅ **Right to be forgotten** - Delete account + all data
- ✅ **Data retention** - Configurable per tier
- ✅ **Consent management** - User controls data
- ✅ **Immutable storage** - For compliance tier (Enterprise)

---

## 💻 Technology Decisions (Simplified)

### Why .NET 9 + Azure?

**Reasons**:
1. ✅ **Familiar** - You have 25 years of C# experience
2. ✅ **Productive** - Fast development, great tooling
3. ✅ **Scalable** - Azure services auto-scale
4. ✅ **Cost-effective** - Serverless, consumption-based pricing
5. ✅ **Modern** - Latest .NET features, Aspire orchestration

### Why Blazor Web App?

**Reasons**:
1. ✅ **One language** - C# for frontend + backend
2. ✅ **Code reuse** - Share components with mobile app (Phase 2)
3. ✅ **Modern** - Hybrid rendering (SSR + WASM)
4. ✅ **SEO-friendly** - Server-side rendering
5. ✅ **Fast initial load** - Better than pure WebAssembly

### Why Azure SQL Serverless?

**Reasons**:
1. ✅ **Auto-pause** - Saves money when idle (side-hustle!)
2. ✅ **Full-text search** - Built-in, no separate service
3. ✅ **Elastic pools** - Scale strategy for growth
4. ✅ **Familiar** - Standard SQL, great tooling
5. ✅ **Cost-effective** - €15-30/month MVP

### Why Shared Database (Not Separate)?

**Microsoft Learn Recommendation**:
> "Shared multitenant databases provide the highest tenant density 
> and lowest financial cost. Recommended for B2C SaaS."

**Reasons**:
1. ✅ **10x cheaper** - €15-30/month vs €150-300/month
2. ✅ **Simpler management** - One database to maintain
3. ✅ **Industry standard** - How successful SaaS companies start
4. ✅ **Easy to scale** - Add elastic pools when needed
5. ✅ **Microsoft-validated** - Official best practice

**When to separate**: Only for Enterprise tier (compliance, isolation requirements)

---

## 📱 Mobile Strategy (Phase 2)

### Why .NET MAUI Blazor Hybrid?

**Single Codebase**:
```
Evermail.Shared.UI (Razor Component Library)
├── Components/
│   ├── EmailListItem.razor    # ← Same component in web + mobile!
│   ├── EmailViewer.razor      # ← Same component in web + mobile!
│   └── SearchBox.razor        # ← Same component in web + mobile!
└── Used by:
    ├── Evermail.WebApp (Web)
    └── Evermail.MobileApp (iOS, Android, Windows, Mac)
```

**Benefits**:
- ✅ **80-90% code reuse** - Don't rebuild UI from scratch
- ✅ **Native features** - Offline, push notifications, biometric auth
- ✅ **App Store revenue** - Additional monetization
- ✅ **Competitive advantage** - Most competitors don't have mobile
- ✅ **Single C# codebase** - No Swift, Kotlin, React Native needed

**Timeline**: Month 6-12 (after MVP is proven)

---

## 🎓 For New Developers

### Start Here (In Order)

1. **This document** (PROJECT_BRIEF.md) - Overview & business case
2. **README.md** - Technical setup instructions
3. **AGENTS.md** - Development principles
4. **Documentation/Architecture.md** - System design details
5. **ARCHITECTURE_DECISIONS.md** - Why each decision was made

### Key Concepts to Understand

**Multi-Tenancy**:
- Every entity has `TenantId`
- All queries filter by tenant
- Complete data isolation

**Document-Driven Development**:
- Always check `Documentation/` folder first
- Update docs before code
- Never create duplicate documentation

**Azure Aspire**:
- Orchestrates all services
- Service discovery (no hardcoded URLs)
- Automatic configuration

**Blazor Web App**:
- Mix of SSR, Server, and WASM rendering
- Share components with future mobile app
- Modern web framework

### How to Contribute

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

**Quick version**:
1. Fork repository
2. Create feature branch
3. Follow `.cursor/rules/*.mdc` conventions
4. Update documentation
5. Submit pull request

---

## 🤖 For AI Agents

### Context Summary

This is a **side-hustle SaaS project** with:
- **Target**: €1,500 MRR by month 6 (100 paying users)
- **Break-even**: 7-20 paying users (€100-€300 revenue)
- **Margins**: 88-90% gross margin
- **Strategy**: MVP → Beta → Growth → Scale

### Development Principles

1. **Document first, code second** - Always check/update `Documentation/`
2. **Multi-tenancy always** - Every entity must have `TenantId`
3. **Security by default** - Encryption, 2FA, audit logging
4. **Keep it simple** - Don't over-engineer for side-hustle
5. **Use official docs** - Microsoft Learn MCP, Context7 MCP, Stripe MCP

### MCP Servers Available

- **Microsoft Learn** - Azure, .NET, Aspire, Blazor, EF Core
- **Context7** - MudBlazor, MimeKit, Azure SDKs, libraries
- **Stripe** - Payment processing operations
- **Azure Pricing** (optional) - Cost estimation

### Technology Constraints

- ✅ Use **.NET 9** (not .NET 8)
- ✅ Use **Blazor Web App** (not pure WASM)
- ✅ Use **Azure SQL Serverless** (not PostgreSQL)
- ✅ Use **Shared database** with TenantId (not separate DBs per tenant)
- ✅ Use **MimeKit** for email parsing (streaming, never load fully)
- ✅ Use **Azure Aspire 9.4** for orchestration

### Files to Reference

Before implementing any feature, check:
- `Documentation/Architecture.md` - System design
- `Documentation/DatabaseSchema.md` - Entity models
- `Documentation/API.md` - Endpoint patterns
- `Documentation/Security.md` - Security patterns
- `Documentation/Pricing.md` - Business model
- `.cursor/rules/*.mdc` - Development rules

---

## 🎊 Success Criteria

### MVP Success (Week 4)
- ✅ Users can upload .mbox files
- ✅ Emails are parsed and searchable
- ✅ Users can search and view emails
- ✅ Stripe Free + Pro tiers work
- ✅ 10 beta users signed up

### Business Success (Month 6)
- ✅ 100 total users
- ✅ 25 paying users (3-5% conversion)
- ✅ €300-€450 MRR
- ✅ Profitable (above break-even)

### Product-Market Fit (Month 12)
- ✅ 1000 total users
- ✅ 300 paying users
- ✅ €4,500 MRR
- ✅ <5% monthly churn
- ✅ Positive user feedback

---

## 📞 Contact & Resources

### Project Links

- **Repository**: https://github.com/kallehiitola/evermail
- **Documentation**: `Documentation/*.md`
- **Architecture**: `Documentation/Architecture.md`
- **Business Model**: `Documentation/Pricing.md`

### Key Documents

- **PROJECT_BRIEF.md** (this file) - Complete overview
- **README.md** - Setup instructions
- **AGENTS.md** - Development principles
- **ARCHITECTURE_DECISIONS.md** - Why each choice was made
- **FINAL_PROJECT_STATUS.md** - Current status

---

## 🎯 The Bottom Line

### Why Evermail Will Succeed

1. **Real Problem** ✅ - People have mbox files they can't use
2. **Affordable Solution** ✅ - €0-99/month vs €50-100/user/year competitors
3. **Fast Break-Even** ✅ - 7-20 paying users (achievable in 1-3 months)
4. **High Margins** ✅ - 88-90% gross margin (typical SaaS)
5. **Scalable** ✅ - Architecture validated by Microsoft Learn
6. **Modern** ✅ - AI-powered, beautiful UI, mobile-ready
7. **Low Risk** ✅ - €300 total investment to break-even

### Next Steps

1. **Build MVP** (4 weeks)
2. **Launch beta** (week 5)
3. **Get 7 paying users** (break-even)
4. **Iterate to 100 users** (profitable side-hustle)
5. **Scale to 1000 users** (full-time income potential)

---

**This is a viable, well-architected SaaS business.** The numbers work, the technology is proven, and the market exists. 

**Time to build!** 🚀

---

**Created**: 2025-11-11  
**For**: New developers, contributors, investors, AI agents  
**Status**: ✅ Complete project overview and business case  
**Validation**: Architecture validated via Microsoft Learn MCP

