# MCP Servers Setup for Evermail

## Overview

Evermail uses **five Model Context Protocol (MCP)** servers to access official, up-to-date documentation and tools directly in Cursor AI:

1. **Microsoft Learn MCP** - Official Microsoft/Azure documentation
2. **Context7 MCP** - Up-to-date library documentation (MudBlazor, MimeKit, etc.)
3. **Stripe MCP** - Stripe payment processing tools
4. **Azure Pricing MCP** - Real-time Azure service pricing and cost estimation
5. **Framer MCP** - Marketing website design (NOT the Blazor application)

## ✅ Current Configuration

Your MCP configuration is located at: `~/.cursor/mcp.json`

```json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "http",
      "url": "https://learn.microsoft.com/api/mcp"
    },
    "context7": {
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp"]
    },
    "Stripe": {
      "command": "npx -y @stripe/mcp --tools=all",
      "env": {
        "STRIPE_SECRET_KEY": "sk_test_..."
      },
      "args": []
    },
    "azure-pricing": {
      "command": "python",
      "args": ["-m", "azure_pricing_server"],
      "cwd": "/Users/kallehiitola/azure-pricing-mcp"
    },
    "framer": {
      "type": "sse",
      "url": "https://mcp.unframer.co/sse?id=xxx&secret=xxx"
    }
  }
}
```

### Setting Up Azure Pricing MCP

1. **Clone the repository**:
   ```bash
   cd ~
   git clone https://github.com/charris-msft/azure-pricing-mcp.git azure-pricing-mcp
   cd azure-pricing-mcp
   ```

2. **Run setup**:
   ```bash
   # Automated setup (creates venv, installs dependencies)
   python setup.py
   ```

3. **Update `~/.cursor/mcp.json`** with the correct path (see config above)

4. **Restart Cursor** completely (Cmd+Q on macOS)

5. **Test it**:
   Ask Cursor: `"What's the price of Azure SQL Serverless in West Europe?"`

### Setting Up Framer MCP

⚠️ **IMPORTANT**: Framer MCP is ONLY for the marketing website (evermail.com), NOT the Blazor application!

1. **Install Framer MCP plugin** in your Framer app:
   - Visit: https://unframer.co/guides/connect-framer-mcp
   - Follow setup instructions

2. **Get your MCP credentials** from the Framer plugin

3. **Update `~/.cursor/mcp.json`** with the correct URL (see config above)

4. **Keep Framer MCP plugin open** in Framer app when using

5. **Test it**:
   Ask Cursor: `"Create a hero section for Evermail marketing website"`

**Remember**: 
- Framer = Marketing website (evermail.com)
- Blazor = Actual application (app.evermail.com)

## 🎯 When to Use Each MCP

### Microsoft Learn MCP

**Use for**: Azure services, .NET framework, C# language features, official Microsoft SDKs

**Example Prompts**:
```
"Show me the latest Azure Aspire configuration for SQL Server. search Microsoft Learn"

"What's the official way to implement JWT authentication in ASP.NET Core 8? fetch full doc"

"Give me Azure CLI commands to deploy Container Apps. search Microsoft Learn"

"Show me EF Core 8 best practices for migrations. search Microsoft Learn and fetch full doc"
```

**Tools Available**:
- `microsoft_docs_search` - Semantic search across Microsoft Learn
- `microsoft_docs_fetch` - Fetch complete documentation page as markdown
- `microsoft_code_sample_search` - Find official Microsoft code samples

### Context7 MCP

**Use for**: Library-specific documentation, NuGet packages, UI frameworks, third-party SDKs

**Example Prompts**:
```
"How do I use MudBlazor's MudDataGrid with server-side pagination? use context7"

"Parse mbox files with MimeKit streaming. use library /jstedfast/mimekit"

"Show me Azure.Storage.Blobs upload examples. use context7"

"Configure Stripe.NET for webhooks. use context7"

"Implement MudBlazor MudDialog with custom actions. use context7"
```

**Tools Available**:
- `resolve-library-id` - Find the Context7 ID for a library name
- `get-library-docs` - Get documentation for a specific library (by ID)

**Pro Tip**: If you know the library ID, use it directly:
```
"use library /mudblazor/mudblazor"
"use library /jstedfast/mimekit"
```

### Stripe MCP

**Use for**: Stripe payment operations, subscription management, customer management

**Example Prompts**:
```
"List my Stripe test customers"

"Create a subscription for the Pro tier at €9/month"

"Show me recent payment intents"

"How do I verify Stripe webhook signatures in C#?"

"Create a Stripe customer for user@example.com"
```

**Tools Available** (20+ tools):
- `create_customer`, `list_customers`
- `create_product`, `list_products`
- `create_price`, `list_prices`
- `create_payment_link`
- `create_subscription`, `list_subscriptions`, `cancel_subscription`
- `create_invoice`, `list_invoices`
- And many more...

### Azure Pricing MCP

**Use for**: Azure service pricing, cost estimation, region comparisons, service selection

**Example Prompts**:
```
"What's the cost of Azure SQL Serverless for a 10GB database in West Europe?"

"Compare App Service pricing between P1v3 and P2v3 in North Europe"

"Estimate storage costs for 500GB of email attachments with 100K reads per month"

"Find the cheapest Azure Container Apps plan for our ingestion worker"

"Compare costs for blob storage between West Europe and North Europe"

"What are the savings with reserved instances for B1ms App Service?"

"Find all VM SKUs suitable for running background workers under $100/month"
```

**Tools Available**:
- `azure_price_search` - Search Azure retail prices with flexible filters
- `azure_price_compare` - Compare prices across different regions and SKUs
- `azure_cost_estimate` - Calculate estimated costs based on usage patterns
- `azure_discover_skus` - Discover available SKUs for a service
- `azure_sku_discovery` - Intelligent SKU discovery with fuzzy matching

**Why Essential for Evermail**:
- ✅ Make cost-informed decisions about Azure services
- ✅ Validate business model (90%+ gross margin target)
- ✅ Compare alternatives before committing to infrastructure
- ✅ Ensure break-even at 7-20 paying customers is achievable
- ✅ Optimize region selection for cost vs latency

### Framer MCP

**Use for**: Marketing website design ONLY (NOT the Blazor application)

⚠️ **CRITICAL DISTINCTION**:
- **Framer** = Public marketing website (evermail.com - landing pages, pricing, features)
- **Blazor** = Actual SaaS application (app.evermail.com - email viewer, search, admin)

**Example Prompts**:
```
"Create a modern landing page hero section for Evermail marketing site"

"Design a pricing comparison table for the marketing website showing all tiers"

"Update the marketing website CTA button to emphasize free trial"

"Publish the marketing website homepage to production"

"Rewrite the hero headline to focus on AI-powered search"
```

**Tools Available**:
- Design and create page components
- Update text and styling
- Manage layouts and sections
- Publish to production
- Export designs

**Architecture Separation**:
```
Marketing Website (Framer)          SaaS Application (Blazor)
├── evermail.com                    ├── app.evermail.com
│   ├── Landing page                │   ├── Email viewer
│   ├── Pricing page                │   ├── Search interface
│   ├── Features page               │   ├── Account settings
│   └── About/Contact               │   └── Admin dashboard
```

**Why Use Framer for Marketing**:
- ✅ Rapid iteration on marketing messaging
- ✅ Focus on customer acquisition and conversion
- ✅ No context switching for marketing content
- ✅ AI-assisted design decisions
- ✅ Quick A/B testing variations

**NOT for**:
- ❌ Building the Blazor application UI
- ❌ Creating the email viewer interface
- ❌ Designing the admin dashboard
- ❌ Application-level components (use MudBlazor for those)

**Setup**: https://unframer.co/guides/connect-framer-mcp

**Note**: The Framer MCP plugin must be **open inside the Framer app** for the MCP to work.

## 📚 Key Resources for Evermail

### Context7 Libraries

Use **Context7** for these libraries in your Evermail project:

| Library | Context7 ID | Use Case |
|---------|------------|----------|
| **MudBlazor** | `/mudblazor/mudblazor` | UI components (tables, forms, dialogs) |
| **MimeKit** | `/jstedfast/mimekit` | Email parsing, mbox handling |
| **Azure.Storage.Blobs** | Auto-resolve | Blob upload/download |
| **Azure.Storage.Queues** | Auto-resolve | Background job queues |
| **Stripe.NET** | `/stripe/stripe-dotnet` | Payment SDK |
| **Entity Framework Core** | Auto-resolve | Database ORM |

### Azure Cost Planning

Use **Azure Pricing MCP** for cost-related decisions:

| Decision | Example Query |
|----------|---------------|
| **Database Choice** | "Compare Azure SQL Serverless vs PostgreSQL for 10GB database" |
| **Region Selection** | "Compare all Azure costs between West Europe and North Europe" |
| **Storage Planning** | "Estimate costs for 1TB blob storage with 1M transactions/month" |
| **Worker Tier** | "Find cheapest Container Apps plan for 24/7 worker processing 100GB/month" |
| **Cost Validation** | "Calculate total monthly infrastructure cost for 100 users" |

## 🚀 Usage Examples

### Example 1: Building Email Search UI

```
User: "Create a search page with MudBlazor components including:
- MudTextField for search input
- MudDataGrid for results with pagination
- MudCard for each email item
use context7 for MudBlazor"

AI will:
1. Use Context7 to get latest MudBlazor docs
2. Generate code with current MudBlazor API
3. Include proper component parameters
4. Use up-to-date patterns
```

### Example 2: Implementing Azure Aspire

```
User: "Configure Azure Blob Storage in Aspire with connection string management. 
search Microsoft Learn and fetch full doc"

AI will:
1. Search Microsoft Learn for Azure Aspire + Blob Storage
2. Fetch the full official documentation
3. Provide current Aspire patterns (not outdated)
4. Include proper configuration examples
```

### Example 3: Setting Up Stripe Subscriptions

```
User: "Create Stripe products and prices for Evermail tiers:
- Pro: €9/month
- Team: €29/month
- Enterprise: €99/month"

AI will:
1. Use Stripe MCP to create products
2. Use Stripe MCP to create prices
3. Return actual Stripe IDs
4. Ready to use in your application
```

## 🔧 Configuration Optimization

### Optional: Add Context7 API Key

For unlimited access (free tier is 10 requests/day), get an API key at [context7.com/dashboard](https://context7.com/dashboard).

Update `~/.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "context7": {
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp"],
      "env": {
        "CONTEXT7_API_KEY": "your_api_key_here"
      }
    }
  }
}
```

### Optional: Add Cursor Rule for Auto-Invocation

To avoid typing "use context7" every time, add this to your Cursor User Rules:

```
Always use context7 when I need code generation, setup or configuration steps, 
or library/API documentation. This means you should automatically use the Context7 
MCP tools to resolve library id and get library docs without me having to explicitly ask.
```

## 🧪 Testing MCP Servers

### Test Microsoft Learn MCP
```
"What are the Azure CLI commands to create an Azure Container App? search Microsoft Learn"
```

Expected: AI uses `microsoft_docs_search` and provides official Azure CLI commands.

### Test Context7 MCP
```
"Show me how to use MudBlazor's MudTable component. use context7"
```

Expected: AI uses `resolve-library-id` → `get-library-docs` and provides current MudBlazor examples.

### Test Stripe MCP
```
"List my Stripe test customers"
```

Expected: AI uses `list_customers` tool and returns actual Stripe data.

### Test Azure Pricing MCP
```
"What's the price of Azure SQL Serverless in West Europe?"
```

Expected: AI uses `azure_price_search` and provides current Azure pricing.

### Test Framer MCP
```
"Create a hero section for Evermail marketing website"
```

Expected: AI uses Framer MCP tools and creates/updates the marketing website.

**Note**: Framer MCP plugin must be open in Framer app.

## 📊 MCP Usage Summary

| MCP Server | Type | Primary Use | Status |
|------------|------|-------------|--------|
| **Microsoft Learn** | HTTP | Azure, .NET, Microsoft tech | ✅ Active |
| **Context7** | Local (NPM) | Libraries, NuGet packages | ✅ Active |
| **Stripe** | Local (NPM) | Payment operations | ✅ Active |
| **Azure Pricing** | Local (Python) | Azure cost estimation | ⚙️ Setup Required |
| **Framer** | SSE | Marketing website design | ✅ Active |

## 🎯 Benefits

1. **Always Up-to-Date**: Gets latest documentation in real-time
2. **No Hallucinations**: Real APIs, not made-up functions
3. **Accurate Code**: Uses official code examples
4. **Version-Specific**: Documentation matches current versions
5. **Integrated**: Works seamlessly in Cursor AI chat
6. **Cost-Informed Decisions**: Real Azure pricing for architecture choices

## 🚨 Troubleshooting

### MCP Server Not Appearing in Cursor

1. **Restart Cursor completely** (Cmd+Q / Alt+F4)
2. **Check MCP configuration**:
   ```bash
   cat ~/.cursor/mcp.json
   ```
3. **Test individual servers**:
   ```bash
   # Test Context7
   npx -y @upstash/context7-mcp
   
   # Test Stripe
   STRIPE_SECRET_KEY="sk_test_..." npx -y @stripe/mcp --tools=all
   ```

### Context7 Rate Limits

Free tier: 10 requests/day. If you need more:
1. Create account at [context7.com/dashboard](https://context7.com/dashboard)
2. Get your API key
3. Add to `mcp.json` env section

### Stripe MCP Errors

Ensure your `STRIPE_SECRET_KEY` is a test key (`sk_test_...`) for development.

For production, use live keys (`sk_live_...`) and store in environment variables.

## 🔗 Additional Resources

- [Microsoft Learn MCP Docs](https://github.com/MicrosoftDocs/mcp)
- [Context7 Documentation](https://context7.com)
- [Stripe MCP GitHub](https://github.com/stripe/agent-toolkit)
- [Azure Pricing MCP GitHub](https://github.com/charris-msft/azure-pricing-mcp)
- [MCP Specification](https://modelcontextprotocol.io)

---

**Last Updated**: 2025-11-11  
**Configuration File**: `~/.cursor/mcp.json`  
**Project Rules**: `.cursor/rules/mcp-tools.mdc`  
**MCPs Configured**: 5 (Microsoft Learn, Context7, Stripe, Azure Pricing, Framer)

