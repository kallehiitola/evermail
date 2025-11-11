# Recommended MCP Servers for Evermail

## ✅ Currently Configured (3)

1. **Microsoft Learn** - Official Microsoft/Azure documentation
2. **Context7** - Up-to-date library documentation (MudBlazor, MimeKit, etc.)
3. **Stripe** - Payment processing and subscription management

## 🎯 Additional Recommended MCPs

Based on your Evermail project needs, here are additional MCP servers you might want to add:

### 1. Git MCP (Optional)
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

### 4. Filesystem MCP (Built-in to Cursor)
**Use for**: File operations, directory navigation

Already available in Cursor by default. The AI can read files, list directories, etc.

### 5. Web Browser MCP (Optional - For Testing)
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

For your project, I recommend **keeping it simple** with the current 3 MCPs:

### Essential (Currently Configured) ✅
1. **Microsoft Learn** - Azure, .NET, C#, Aspire
2. **Context7** - MudBlazor, MimeKit, libraries
3. **Stripe** - Payment processing

### Nice to Have (Optional)
4. **Git MCP** - If you do a lot of git operations via AI
5. **GitHub MCP** - If you want to create issues/PRs from Cursor

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
| UI components | Context7 | "MudBlazor dialog examples" |
| Email parsing | Context7 | "MimeKit mbox streaming" |
| Azure SDKs | Context7 | "Azure.Storage.Blobs API" |
| Stripe API | Stripe MCP | "Create subscription" |
| Payment webhooks | Stripe MCP | "List payment intents" |

## 🚀 Getting Started

After restarting Cursor, try these commands:

```bash
# Test Microsoft Learn
"Show me Azure Aspire SQL Server configuration. search Microsoft Learn"

# Test Context7
"How do I use MudBlazor's MudDialog? use context7"

# Test Stripe
"List my Stripe test customers"
```

All three should work seamlessly! 🎉

## 🔗 Resources

- **Microsoft Learn MCP**: https://github.com/MicrosoftDocs/mcp
- **Context7**: https://context7.com
- **Stripe MCP**: https://github.com/stripe/agent-toolkit
- **MCP Registry**: https://github.com/punkpeye/awesome-mcp-servers

## 📝 Configuration File Location

Your MCP servers are configured in:
- **Global**: `~/.cursor/mcp.json`
- **Project Rule**: `.cursor/rules/mcp-tools.mdc` (ensures AI uses them)

---

**Last Updated**: 2025-11-11  
**Configured MCPs**: 3 (Microsoft Learn, Context7, Stripe)  
**Status**: ✅ All Active and Tested

