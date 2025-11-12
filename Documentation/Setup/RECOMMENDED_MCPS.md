# Recommended MCP Servers for Evermail

## ✅ Currently Configured (5)

1. **Microsoft Learn** - Official Microsoft/Azure documentation
2. **Context7** - Up-to-date library documentation (MudBlazor, MimeKit, etc.)
3. **Stripe** - Payment processing and subscription management
4. **Azure Pricing** - Real-time Azure service pricing and cost estimation
5. **Framer** - Design and prototype websites directly from Cursor

## 🎯 Additional Recommended MCPs

Based on your Evermail project needs, here are additional MCP servers you might want to add:

### 1. Azure Pricing MCP ✅ RECOMMENDED

**Use for**: Azure service pricing, cost estimation, region comparisons

**Why Essential for Evermail**: 
- Make informed decisions about which Azure services to use
- Compare costs across regions (e.g., should we use West Europe or North Europe?)
- Estimate monthly costs before deploying services
- Find the most cost-effective SKUs for your needs

**Configuration**:
```json
{
  "azure-pricing": {
    "command": "python",
    "args": ["-m", "azure_pricing_server"],
    "cwd": "/path/to/azure_pricing_server"
  }
}
```

**Setup Steps**:
1. Clone: `git clone https://github.com/charris-msft/azure-pricing-mcp.git ~/azure-pricing-mcp`
2. Setup: `cd ~/azure-pricing-mcp && python setup.py`
3. Add to `~/.cursor/mcp.json` with correct path
4. Restart Cursor

**Use cases**:
- **Service Selection**: "Compare Azure SQL Serverless vs PostgreSQL pricing for 10GB database"
- **Region Optimization**: "Compare costs for App Service between West Europe and North Europe"
- **Cost Estimation**: "Estimate monthly costs for 100GB Blob Storage with 10K transactions"
- **SKU Discovery**: "Find the cheapest Azure Container Apps plan for our ingestion worker"
- **Savings Plans**: "Show me reserved instance savings for B1ms App Service"

**Example Prompts**:
```
"What's the cost of Azure SQL Serverless for a 10GB database in West Europe?"

"Compare App Service pricing between P1v3 and P2v3 in North Europe"

"Estimate storage costs for 500GB of email attachments with 100K reads per month"

"Find all GPU-enabled VM options under $200/month"

"What are the savings with reserved instances for our planned infrastructure?"
```

**Tools Available**:
- `azure_price_search` - Search Azure retail prices with filters
- `azure_price_compare` - Compare prices across regions/SKUs
- `azure_cost_estimate` - Estimate costs based on usage patterns
- `azure_discover_skus` - Discover available SKUs for a service
- `azure_sku_discovery` - Intelligent SKU discovery with fuzzy matching

**Benefits for Evermail**:
- ✅ Make cost-effective architecture decisions
- ✅ Plan pricing tiers based on actual Azure costs
- ✅ Compare alternatives before committing
- ✅ Validate gross margin assumptions (90%+ target)
- ✅ Optimize for break-even at 7-20 customers

### 2. Git MCP (Optional)
**Use for**: Git operations, GitHub integration, PR management

**Configuration**:
```json
{
  "git": {
    "command": "npx",
    "args": ["-y", "@modelcontextprotocol/server-git"]
  }
}
```

**Use cases**:
- Review commit history
- Create branches
- Manage pull requests
- Git workflow automation

**Example**: `"Show me recent commits related to email parsing"`

### 2. GitHub MCP (Optional)
**Use for**: GitHub API operations, issue tracking, repo management

**Configuration**:
```json
{
  "github": {
    "command": "npx",
    "args": ["-y", "@modelcontextprotocol/server-github"],
    "env": {
      "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_..."
    }
  }
}
```

**Use cases**:
- Create GitHub issues from code
- Link commits to issues
- Search repository
- Manage project boards

**Example**: `"Create a GitHub issue for implementing AI search feature"`

### 3. Postgres MCP (If switching to PostgreSQL)
**Use for**: Direct database queries, schema inspection

**Configuration**:
```json
{
  "postgres": {
    "command": "npx",
    "args": ["-y", "@modelcontextprotocol/server-postgres"],
    "env": {
      "POSTGRES_CONNECTION_STRING": "postgresql://..."
    }
  }
}
```

**Note**: You're using Azure SQL, so this is only if you switch to PostgreSQL.

### 4. Framer MCP ✅ CONFIGURED
**Use for**: Marketing website design only (NOT the Blazor application)

**⚠️ IMPORTANT**: Framer is ONLY for the public-facing marketing website (evermail.com). The actual SaaS application is built with Blazor Web App and is completely separate.

**Why Essential for Evermail**: 
- Create and iterate on the Evermail **marketing website** (landing pages, pricing, features)
- Customer-facing content and design
- Fast website iteration without coding
- Separate from the actual Blazor application development

**Configuration**:
```json
{
  "framer": {
    "type": "sse",
    "url": "https://mcp.unframer.co/sse?id=xxx&secret=xxx"
  }
}
```

**Important**: The Framer MCP plugin must be **open inside the Framer app** for the MCP to work. Only one MCP plugin can be open at a time.

**Use cases (Marketing Website Only)**:
- **Landing Pages**: "Create a modern landing page for Evermail with hero section and pricing"
- **Pricing Pages**: "Design a pricing comparison table for subscription tiers"
- **Feature Pages**: "Build a features overview page with benefit cards"
- **Content Updates**: "Update the homepage hero text to emphasize AI search"
- **Publishing**: "Publish the marketing website to production"

**NOT for**:
- ❌ Building the actual Blazor application UI
- ❌ Creating the email viewer interface
- ❌ Designing the admin dashboard
- ❌ Application-level components (use MudBlazor for those)

**Example Prompts (Marketing Website)**:
```
"Create a modern SaaS landing page for Evermail marketing site with hero, features, and pricing sections"

"Design a pricing comparison table for the marketing website showing Free, Pro, Team, and Enterprise tiers"

"Update the marketing website CTA button color to Azure Blue (#0078D4)"

"Publish the marketing website homepage to production"

"Rewrite the marketing site hero headline to focus on email archive security"
```

**Remember**: Framer = Marketing Website | Blazor = Actual Application

**Tools Available**:
- Design and create page components
- Update text and styling
- Manage layouts and sections
- Publish to production
- Export designs

**Benefits for Evermail Marketing Website**:
- ✅ Rapid marketing website iteration
- ✅ Focus on customer acquisition and conversion
- ✅ No context switching for marketing content
- ✅ AI-assisted design decisions for landing pages
- ✅ Quick A/B testing variations for CTAs and messaging

**Architecture Separation**:
```
Evermail Marketing Website (Framer)
├── evermail.com (public marketing site)
│   ├── Landing page
│   ├── Pricing page
│   ├── Features page
│   └── About/Contact pages
│
Evermail SaaS Application (Blazor)
├── app.evermail.com (authenticated application)
│   ├── Email viewer
│   ├── Search interface
│   ├── Account settings
│   └── Admin dashboard
```

**Setup Guide**: [https://unframer.co/guides/connect-framer-mcp](https://unframer.co/guides/connect-framer-mcp)

### 5. Filesystem MCP (Built-in to Cursor)
**Use for**: File operations, directory navigation

Already available in Cursor by default. The AI can read files, list directories, etc.

### 6. Web Browser MCP (Optional - For Testing)
**Use for**: Testing your deployed application, web scraping

**Configuration**:
```json
{
  "browser": {
    "command": "npx",
    "args": ["-y", "@executeautomation/playwright-mcp-server"]
  }
}
```

**Use cases**:
- Test your Blazor UI in browser
- Verify deployed application
- Screenshot testing

## 🎯 Recommended Setup for Evermail

For your project, I recommend these MCPs:

### Essential (Currently Configured) ✅
1. **Microsoft Learn** - Azure, .NET, C#, Aspire
2. **Context7** - MudBlazor, MimeKit, libraries
3. **Stripe** - Payment processing
4. **Azure Pricing** - Cost estimation and service selection
5. **Framer** - Website design and prototyping

### Nice to Have (Optional)
6. **Git MCP** - If you do a lot of git operations via AI
7. **GitHub MCP** - If you want to create issues/PRs from Cursor

### Not Needed (At least for now)
- Postgres MCP (you're using Azure SQL)
- Database MCP (EF Core handles this)
- Kubernetes MCP (you're using Azure Container Apps)
- Slack MCP (unless you want AI to post to Slack)

## 💡 Pro Tips

### 1. Add Auto-Invoke Rule

Add to Cursor User Rules (Settings → Rules → User Rules):

```
Always use context7 when I need library documentation, code examples, or setup guides.
Always search Microsoft Learn for Azure, .NET, and Microsoft technologies.
Automatically use Stripe MCP for any payment-related questions.

This means you should use these MCP tools automatically without me having to ask.
```

### 2. Library ID Cheat Sheet

Keep these handy for quick access:

```
MudBlazor: use library /mudblazor/mudblazor
MimeKit: use library /jstedfast/mimekit
Stripe.NET: use library /stripe/stripe-dotnet
```

### 3. Combine MCPs

You can use multiple MCPs in one prompt:

```
"Implement Azure Blob Storage upload with MudBlazor file picker.
search Microsoft Learn for Blob Storage patterns,
use context7 for MudBlazor MudFileUpload component"
```

## 📊 MCP Usage Guidelines

| Question Type | MCP to Use | Example |
|---------------|------------|---------|
| Azure services | Microsoft Learn | "Azure Container Apps scaling" |
| .NET framework | Microsoft Learn | "ASP.NET Core middleware" |
| Azure Aspire | Microsoft Learn | "Aspire service discovery" |
| Azure pricing | Azure Pricing | "SQL Serverless pricing" |
| Cost comparison | Azure Pricing | "Compare regions for storage" |
| Service selection | Azure Pricing | "Cheapest VM for background jobs" |
| UI components | Context7 | "MudBlazor dialog examples" |
| Email parsing | Context7 | "MimeKit mbox streaming" |
| Azure SDKs | Context7 | "Azure.Storage.Blobs API" |
| Stripe API | Stripe MCP | "Create subscription" |
| Payment webhooks | Stripe MCP | "List payment intents" |
| Marketing website | Framer MCP | "Create landing page" |
| Marketing pages | Framer MCP | "Design pricing page" |
| Marketing content | Framer MCP | "Update CTA button" |
| **Application UI** | **Context7 (MudBlazor)** | **"MudBlazor MudDataGrid"** |

## 🚀 Getting Started

After restarting Cursor, try these commands:

```bash
# Test Microsoft Learn
"Show me Azure Aspire SQL Server configuration. search Microsoft Learn"

# Test Context7
"How do I use MudBlazor's MudDialog? use context7"

# Test Stripe
"List my Stripe test customers"

# Test Azure Pricing
"What's the price of Azure SQL Serverless in West Europe?"

# Test Framer (requires Framer MCP plugin to be open in Framer app)
# NOTE: This is ONLY for the marketing website, not the Blazor application
"Create a modern landing page section for Evermail marketing website"
```

All five should work seamlessly! 🎉

**Note for Framer MCP**: Make sure the Framer MCP plugin is open inside your Framer app before using the MCP. See the [setup guide](https://unframer.co/guides/connect-framer-mcp) for details.

## 🔗 Resources

- **Microsoft Learn MCP**: https://github.com/MicrosoftDocs/mcp
- **Context7**: https://context7.com
- **Stripe MCP**: https://github.com/stripe/agent-toolkit
- **Azure Pricing MCP**: https://github.com/charris-msft/azure-pricing-mcp
- **Framer MCP**: https://unframer.co/guides/connect-framer-mcp
- **MCP Registry**: https://github.com/punkpeye/awesome-mcp-servers

## 📝 Configuration File Location

Your MCP servers are configured in:
- **Global**: `~/.cursor/mcp.json`
- **Project Rule**: `.cursor/rules/mcp-tools.mdc` (ensures AI uses them)

---

**Last Updated**: 2025-11-11  
**Configured MCPs**: 5 (Microsoft Learn, Context7, Stripe, Azure Pricing, Framer)  
**Status**: ✅ Microsoft Learn, Context7, Stripe, Framer Active | ⚙️ Azure Pricing Setup Required

