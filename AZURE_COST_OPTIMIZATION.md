# Azure Cost Optimization for Evermail

> Using Azure Pricing MCP for informed infrastructure decisions

## Overview

Evermail targets **90%+ gross margins** with **break-even at 7-20 paying customers**. Every infrastructure decision impacts profitability. Use the Azure Pricing MCP to make cost-informed choices.

## Quick Start

```bash
# Setup Azure Pricing MCP
./scripts/setup-azure-pricing-mcp.sh

# Test it in Cursor
"What's the cost of Azure SQL Serverless in West Europe?"
```

## Core Infrastructure Decisions

### 1. Database: Azure SQL vs PostgreSQL

**Compare costs:**
```
"Compare Azure SQL Serverless vs PostgreSQL pricing for 10GB database in West Europe"
```

**Key considerations:**
- SQL Serverless: Auto-pause, pay per use
- PostgreSQL: Fixed cost, more predictable
- Full-text search needs (SQL Server native vs pg_trgm)

### 2. Region Selection: West Europe vs North Europe

**Compare costs:**
```
"Compare all Azure service costs between West Europe and North Europe regions"
```

**Key considerations:**
- Price differences (usually 5-15%)
- Latency to target customers
- Data residency requirements (GDPR)

### 3. Blob Storage: Hot vs Cool Tier

**Estimate costs:**
```
"Compare Hot vs Cool blob storage tiers for 500GB with 100K reads per month"
```

**Key considerations:**
- Hot tier: Higher storage cost, lower transaction cost
- Cool tier: Lower storage cost, higher transaction cost
- Email attachments are read infrequently → Cool tier likely optimal

### 4. Container Apps: Consumption vs Dedicated

**Find cheapest option:**
```
"Compare Container Apps consumption vs dedicated plans for 24/7 worker processing 100GB/month"
```

**Key considerations:**
- Consumption: Pay per use, cold starts
- Dedicated: Fixed cost, always warm
- Ingestion worker likely consumption (intermittent load)
- WebApp likely dedicated (always available)

### 5. App Service Plan Selection

**Find optimal SKU:**
```
"Show me all App Service plans suitable for Blazor WASM with <10 concurrent users under €50/month"
```

**Key considerations:**
- B1 (Basic): €11/month, 1.75 GB RAM
- P1v3 (Premium): €61/month, 4 GB RAM, auto-scale
- Start with B1, scale to P1v3 as needed

## Cost Estimation Examples

### Scenario 1: 10 Paying Customers (100 GB total)

**Estimate infrastructure costs:**
```
"Estimate monthly Azure costs for:
- 100GB blob storage (Cool tier)
- Azure SQL Serverless 10GB database
- App Service B1 plan
- Container Apps consumption (5 hours/day processing)
- 1M blob transactions
- 10K SQL queries per day"
```

**Target:** <€90/month (€9/customer → €900 revenue → 90% margin)

### Scenario 2: 50 Paying Customers (500 GB total)

**Estimate infrastructure costs:**
```
"Estimate monthly Azure costs for:
- 500GB blob storage (Cool tier)
- Azure SQL Serverless 50GB database
- App Service P1v3 plan
- Container Apps consumption (20 hours/day processing)
- 5M blob transactions
- 50K SQL queries per day"
```

**Target:** <€450/month (€9/customer → €4,500 revenue → 90% margin)

### Scenario 3: AI Features (Phase 2)

**Estimate AI costs:**
```
"Estimate monthly costs for Azure OpenAI:
- GPT-4 Turbo: 1M input tokens, 100K output tokens
- text-embedding-ada-002: 10M tokens
- 1000 summarization requests/day
in West Europe region"
```

**Impact on pricing:** AI tier at €19/month needs <€2 cost per customer

## SKU Discovery

### Find Cost-Effective Options

**Background workers:**
```
"Find cheapest Azure Container Apps or VM options for running background workers under €50/month"
```

**Database options:**
```
"Show me all Azure SQL tiers suitable for 10GB database with <1000 queries/day under €30/month"
```

**Storage alternatives:**
```
"Compare Azure Blob Storage vs Azure Files for storing 1TB of email attachments"
```

## Monthly Cost Validation

Use Azure Pricing MCP **before making architecture changes**:

### Before Adding a Service

```
"What will adding Azure Functions consumption plan cost for 1M executions/month?"
```

### Before Changing Regions

```
"Compare total infrastructure costs between West Europe and North Europe for our stack"
```

### Before Upgrading Tiers

```
"Compare costs between App Service B1 vs P1v3 for 1 year (consider reserved instances)"
```

## Reserved Instances & Savings Plans

**Check savings:**
```
"What are the reserved instance savings for B1 App Service for 1 year commitment?"

"Compare pay-as-you-go vs 1-year reserved vs 3-year reserved for P1v3 App Service"

"Show me Azure savings plans for compute services"
```

**When to use:**
- Reserved Instances: When stable workload for 1-3 years
- Savings Plans: More flexible than reserved instances
- Rule: Only commit after 3+ months stable production load

## Cost Monitoring Queries

### Validate Gross Margin

```
"Calculate total monthly Azure costs for:
[list current services]

Then compare to revenue target of €X/month"
```

### Find Cost Optimization Opportunities

```
"What are cheaper alternatives to [current service] that meet these requirements:
- [requirement 1]
- [requirement 2]"
```

### Validate Pricing Assumptions

```
"Are these cost estimates still accurate for 2025?
- Azure SQL Serverless: €X/month
- Blob Storage Cool: €Y/TB
- App Service B1: €Z/month"
```

## Best Practices

### 1. Always Compare Before Committing

❌ **Don't**: Choose services based on tutorials or preferences  
✅ **Do**: Compare costs using Azure Pricing MCP first

### 2. Validate Business Model Regularly

❌ **Don't**: Assume 90% margin without checking costs  
✅ **Do**: Calculate actual infrastructure costs vs revenue monthly

### 3. Consider Reserved Instances After Validation

❌ **Don't**: Buy reserved instances on day 1  
✅ **Do**: Validate load, then commit to 1-year reserved after 3 months

### 4. Choose Regions Based on Cost + Latency

❌ **Don't**: Default to "West Europe" without checking  
✅ **Do**: Compare regions and choose based on cost/latency trade-off

### 5. Use Consumption Tiers When Possible

❌ **Don't**: Run dedicated resources for intermittent workloads  
✅ **Do**: Use consumption tiers (Container Apps, Functions) when appropriate

## Integration with Documentation

Always update `Documentation/Pricing.md` with actual costs:

1. **Query Azure Pricing MCP** for current costs
2. **Update Pricing.md** with real numbers
3. **Recalculate margins** based on updated costs
4. **Adjust pricing tiers** if margins drop below 90%

## Example Workflow

### Adding a New Service

1. **Research** service requirements
   ```
   "What are the requirements for implementing [feature]?"
   ```

2. **Get pricing** for candidate services
   ```
   "Compare costs for implementing [feature] using Service A vs Service B"
   ```

3. **Estimate impact** on margins
   ```
   "Calculate gross margin if we add €X/customer infrastructure cost to €9 subscription"
   ```

4. **Make decision** based on cost + requirements
   ```
   If margin stays >90%, proceed
   If margin <90%, adjust pricing or find cheaper alternative
   ```

5. **Document** in `Documentation/Pricing.md`
   - Add service costs
   - Update total infrastructure cost per customer
   - Recalculate margins

## Resources

- **Setup Guide**: [MCP_SETUP.md](MCP_SETUP.md)
- **Business Model**: [Documentation/Pricing.md](Documentation/Pricing.md)
- **Architecture**: [Documentation/Architecture.md](Documentation/Architecture.md)
- **Azure Pricing MCP**: https://github.com/charris-msft/azure-pricing-mcp

## Quick Reference Commands

```bash
# Setup
./scripts/setup-azure-pricing-mcp.sh

# Common queries (ask Cursor AI)
"SQL Serverless pricing West Europe"
"Compare regions for blob storage"
"Cheapest App Service plan for Blazor"
"Estimate costs for 100 customers"
"Reserved instance savings for B1"
"Compare Container Apps vs App Service"
"Total monthly cost for MVP infrastructure"
```

---

**Remember**: Every €1 infrastructure cost requires €10 revenue to maintain 90% margin. Choose wisely! 💰

