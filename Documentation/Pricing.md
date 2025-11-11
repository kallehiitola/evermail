# Evermail - Pricing & Business Model

## Business Model Overview

Evermail is a **Freemium SaaS** with tiered subscriptions. Users can start free and upgrade as their storage and feature needs grow.

**Revenue Streams**:
1. **Subscriptions** (primary): Monthly/yearly recurring revenue
2. **Usage-based Storage** (future): Overage charges beyond tier limits
3. **AI Add-ons** (Phase 2): AI-powered features as paid modules

## Subscription Tiers

| Feature | Free | Pro | Team | Enterprise |
|---------|------|-----|------|-----------|
| **Price** | €0/month | €9/month | €29/month | €99/month |
| **Max Storage** | 1 GB | 5 GB | 50 GB | 500 GB |
| **Max Users** | 1 | 1 | 5 | 50 |
| **Max Mailboxes** | 1 | Unlimited | Unlimited | Unlimited |
| **Data Retention** | 30 days | 1 year | 2 years | Configurable (1-10 years) |
| **Full-Text Search** | ✅ Basic | ✅ Advanced | ✅ Advanced | ✅ Advanced |
| **AI-Powered Search** | ❌ | ✅ | ✅ | ✅ |
| **Email Summaries** | ❌ | ✅ (50/month) | ✅ (500/month) | ✅ Unlimited |
| **Gmail/Outlook Import** | ❌ | ✅ | ✅ | ✅ |
| **Shared Workspaces** | ❌ | ❌ | ✅ | ✅ |
| **GDPR Archive (Immutable)** | ❌ | ❌ | ❌ | ✅ |
| **API Access** | ❌ | ❌ | ✅ Limited | ✅ Full |
| **Priority Support** | ❌ | ❌ | ❌ | ✅ |
| **SLA** | None | None | 99.5% | 99.9% |

## Detailed Tier Breakdown

### Free Tier (€0/month)
**Target**: Individuals trying out the service, one-time archiving needs

**Limits**:
- 1 GB total storage (roughly 5,000-10,000 emails)
- 1 mailbox upload
- Auto-delete after 30 days
- No AI features
- Manual .mbox upload only

**Value Proposition**: "Try before you buy, quick email archive search"

**Conversion Strategy**:
- After 30 days, prompt to upgrade to keep data
- Show AI feature teasers ("Upgrade to get AI summaries")
- Notify when approaching 1 GB limit

### Pro Tier (€9/month or €90/year)
**Target**: Individual professionals, freelancers, personal archiving

**Limits**:
- 5 GB storage (~25,000-50,000 emails)
- Unlimited mailbox uploads
- 1-year data retention
- 50 AI summaries per month
- Gmail/Outlook direct import

**Value Proposition**: "Your personal email archive with AI-powered search"

**Pricing Rationale**:
- Comparable to basic Dropbox/Google One plans
- Affordable monthly commitment
- 10-month equivalent if paid annually (2 months free)

### Team Tier (€29/month or €290/year)
**Target**: Small businesses, shared team archives, compliance needs

**Limits**:
- 50 GB storage (~250,000+ emails)
- 5 user seats
- 2-year data retention
- 500 AI summaries per month (shared across team)
- Shared workspaces for team collaboration
- API access (limited to 10,000 requests/month)

**Value Proposition**: "Centralized email archive for your team"

**Pricing Rationale**:
- €5.80 per user (if 5 seats used)
- Cheaper than enterprise email archiving solutions (€20-50/user/month)
- Attractive for startups, small law firms, HR departments

### Enterprise Tier (€99/month or €990/year)
**Target**: Regulated industries (law, finance), large teams, compliance requirements

**Limits**:
- 500 GB storage (~2.5 million emails)
- 50 user seats
- Configurable retention (1-10 years)
- Unlimited AI features
- GDPR Archive with immutable storage (WORM)
- Full API access (100,000 requests/month)
- Priority support (email + Slack channel)
- 99.9% SLA

**Value Proposition**: "Enterprise-grade email archiving with compliance"

**Pricing Rationale**:
- €1.98 per user (if 50 seats used)
- Comparable to enterprise archiving: MailStore (~€50-100/user/year), Barracuda (~€100/user/year)
- Significant savings for compliance-focused companies

### Custom/On-Premise (Contact Sales)
For customers requiring:
- On-premise deployment
- Multi-region data residency
- Custom SLAs (99.99%)
- Dedicated infrastructure
- White-label solution

**Pricing**: €500+ per month depending on requirements

## Cost Structure

### Infrastructure Costs (Monthly)

#### Fixed Costs (Baseline)
| Resource | Configuration | Monthly Cost (EUR) |
|----------|---------------|-------------------|
| Azure SQL Serverless | 2 vCores, 10 GB storage | €15-30 |
| Azure Container Apps | 3 containers, 0.5 vCPU, 1 GB RAM each | €40-60 |
| Application Insights | 5 GB ingestion | €10 |
| Key Vault | Secrets only | €1 |
| **Total Fixed** | | **€66-101** |

#### Variable Costs (Per User/GB)
| Resource | Usage | Cost per Unit | Notes |
|----------|-------|---------------|-------|
| Blob Storage (Hot) | 1 GB | €0.008/GB/month | Email archives |
| Blob Storage (Cool) | 1 GB | €0.004/GB/month | After 90 days |
| Database Storage | 1 GB | €0.05/GB/month | Metadata |
| Compute (Worker) | 1 hour | €0.10/hour | Mailbox processing |
| Stripe Fees | 1 transaction | 2.9% + €0.30 | Payment processing |

#### Example Variable Cost Calculation
**For 100 users** (mixed tiers):
- 50 Free users: 1 GB each = 50 GB blob + 2 GB DB = €0.50
- 30 Pro users: 5 GB each = 150 GB blob + 7 GB DB = €1.55
- 15 Team users: 10 GB each (avg) = 150 GB blob + 7 GB DB = €1.55
- 5 Enterprise users: 50 GB each (avg) = 250 GB blob + 12 GB DB = €2.60

**Total Storage Cost**: €6.20/month

**Compute Cost** (mailbox processing):
- 100 new mailboxes per month @ 5 minutes avg = 8.3 hours = €0.83

**Total Variable**: ~€7/month for 100 users

**Grand Total**: €73-108/month infrastructure cost for 100 users

### Revenue Model (100 Users Example)

| Tier | Users | Monthly Revenue | Annual Revenue |
|------|-------|----------------|----------------|
| Free | 50 | €0 | €0 |
| Pro | 30 | €270 | €3,240 |
| Team | 15 | €435 | €5,220 |
| Enterprise | 5 | €495 | €5,940 |
| **Total** | **100** | **€1,200** | **€14,400** |

**Gross Margin**: (€1,200 - €108) / €1,200 = **91%**

**Key Insight**: High gross margin typical of SaaS. Break-even around **15-20 paying users**.

## Unit Economics

### Customer Acquisition Cost (CAC)
**Channels**:
- **Organic** (SEO, content marketing): €0-10 per user
- **Paid Ads** (Google, LinkedIn): €20-50 per user
- **Referrals**: €5 per user (10% commission)

**Target CAC**: €25 per paying user

### Lifetime Value (LTV)
**Assumptions**:
- Average revenue per user (ARPU): €15/month (blend of tiers)
- Average customer lifetime: 24 months (2 years)
- Gross margin: 90%

**LTV Calculation**:
```
LTV = ARPU × Lifetime × Gross Margin
LTV = €15 × 24 × 0.90 = €324
```

**LTV:CAC Ratio**: €324 / €25 = **12.96**

**Verdict**: Excellent ratio (healthy SaaS is 3:1, great is 5:1+)

### Churn Rate Targets
- **Month 1-3**: 20% (free trial conversions)
- **Month 4-12**: 5% per month (early adopters)
- **Month 12+**: 2% per month (sticky customers)

**Mitigation Strategies**:
- Email retention campaigns at 20 days (Free tier)
- "Why are you leaving?" survey
- Downgrade option instead of cancel
- Annual plans (lower churn, upfront cash)

## Pricing Strategy

### Anchoring
- Free tier sets low anchor (€0)
- Pro tier is "only €9/month" (feels cheap)
- Team tier positioned as "best value" (highlighted)
- Enterprise tier justifies high price with compliance features

### Decoy Pricing
- Team tier is intentionally attractive (€29 for 5 users = €5.80/user)
- Makes Enterprise tier seem even better value (€99 for 50 users = €1.98/user)

### Annual Discount
- 10-month equivalent pricing on annual plans (2 months free)
- Reduces churn, improves cash flow
- Standard SaaS practice

### Fair Usage Policy
For AI features:
- Pro: 50 summaries/month (~1.5 per day, enough for individuals)
- Team: 500 summaries/month (~16 per day, enough for small team)
- Enterprise: Unlimited (fair usage ~5,000/month)

**Overage**: €0.10 per additional AI summary (avoid surprise bills, prompt upgrade instead)

## Competitor Comparison

| Product | Target | Pricing | Storage | Strengths | Weaknesses |
|---------|--------|---------|---------|-----------|------------|
| **Evermail** | Individuals, SMBs | €0-99/month | 1GB-500GB | Modern UI, AI search, affordable | New entrant, limited integrations |
| **MailStore** | Enterprises | €50-100/user/year | Unlimited | Mature, on-premise option | Expensive, dated UI |
| **Barracuda Email Archiving** | Enterprises | €100+/user/year | Unlimited | Strong compliance | Expensive, overkill for SMBs |
| **CloudHQ** | Individuals | $10/month | Unlimited | Simple Gmail backup | Limited search, no AI |
| **Gmail Built-in** | Individuals | $2-10/month (Google One) | 15GB-2TB | Integrated, cheap | No advanced search, not archiving-focused |

**Evermail's Position**: Premium features at mid-tier pricing for SMBs, with AI differentiation.

## Growth Projections

### Conservative (12-Month)
| Metric | Month 3 | Month 6 | Month 12 |
|--------|---------|---------|----------|
| Total Users | 50 | 200 | 500 |
| Paying Users | 10 (20%) | 50 (25%) | 150 (30%) |
| MRR | €100 | €600 | €2,000 |
| Infrastructure Cost | €100 | €120 | €180 |
| **Profit** | **€0** | **€480** | **€1,820** |

### Optimistic (12-Month)
| Metric | Month 3 | Month 6 | Month 12 |
|--------|---------|---------|----------|
| Total Users | 100 | 500 | 1,500 |
| Paying Users | 25 (25%) | 150 (30%) | 500 (33%) |
| MRR | €300 | €2,000 | €7,000 |
| Infrastructure Cost | €110 | €180 | €400 |
| **Profit** | **€190** | **€1,820** | **€6,600** |

### Break-Even Analysis
**Fixed Cost**: €100/month  
**Average Revenue per Paying User**: €15/month

**Break-Even Formula**:
```
Paying Users Needed = Fixed Cost / ARPU
                     = €100 / €15
                     = 7 paying users
```

**Target**: Achieve 7 paying users by Month 2 (very achievable).

## Monetization Roadmap

### Phase 1 (Months 1-3): Core Subscriptions
- Launch Free, Pro, Team tiers
- Stripe integration
- Focus on organic growth (SEO, content)

### Phase 2 (Months 4-6): AI Add-Ons
- AI search and summaries for Pro+ users
- Metered billing for overages
- "Smart Attachments" module (€5/month add-on)

### Phase 3 (Months 7-12): Enterprise & Integrations
- Launch Enterprise tier
- GDPR Archive features
- Gmail/Outlook OAuth import
- API access (usage-based pricing)

### Phase 4 (Year 2+): Vertical Expansion
- Legal compliance vertical (eDiscovery, legal hold)
- HR vertical (employee offboarding archives)
- White-label option for MSPs
- On-premise deployment for large enterprises

## Payment Processing (Stripe)

### Subscription Management
- **Checkout**: Hosted Stripe Checkout (PCI compliance handled)
- **Billing Portal**: Stripe Customer Portal (update cards, view invoices, cancel)
- **Webhooks**: Handle subscription lifecycle events

### Pricing IDs (Stripe)
```
Free: N/A (free, no payment)
Pro Monthly: price_pro_monthly (€9)
Pro Yearly: price_pro_yearly (€90)
Team Monthly: price_team_monthly (€29)
Team Yearly: price_team_yearly (€290)
Enterprise Monthly: price_enterprise_monthly (€99)
Enterprise Yearly: price_enterprise_yearly (€990)
```

### Tax Handling
- **Stripe Tax**: Automatically calculates and collects VAT for EU customers
- Enable in Stripe Dashboard → Settings → Tax
- No manual VAT registration needed initially (under €10k threshold)

## Pricing Optimization

### A/B Testing Ideas
- Test €7 vs €9 for Pro tier (psychological pricing)
- Test positioning ("Most Popular" badge on Team vs Enterprise)
- Test annual discount (10% vs 20%)

### Metrics to Track
- **Conversion Rate**: Free → Paid (target: 3-5%)
- **Upgrade Rate**: Pro → Team (target: 10%)
- **Churn Rate**: Target <5%/month
- **ARPU**: Track trend, aim to increase via upsells
- **CAC Payback Period**: Target <6 months

### Price Increases
- **Grandfathering**: Existing customers keep old pricing for 12 months
- **Notification**: 30-day notice before increase
- **Justification**: New features, AI costs, inflation adjustment
- **Frequency**: Annual review, max 10% increase

---

## Summary

**Core Value**: Affordable email archiving with AI-powered search for individuals and small teams.

**Key Differentiators**:
1. **AI Features** (smart search, summaries)
2. **Modern UX** (Blazor, fast, beautiful)
3. **Transparent Pricing** (no hidden fees)
4. **Compliance Options** (GDPR Archive for regulated industries)

**Economics**: 
- Break-even at 7 paying users (~Month 2)
- 90%+ gross margins (typical SaaS)
- LTV:CAC of 12:1 (excellent)
- Path to €10k MRR within 12 months (optimistic)

**Next Steps**:
1. Launch MVP with Free + Pro tiers
2. Validate pricing with first 100 users
3. Iterate based on feedback
4. Add Team tier once 50+ paying users
5. Build Enterprise features once 200+ paying users

---

**Last Updated**: 2025-11-11  
**Pricing Version**: 1.0  
**Next Review**: After first 100 paying customers

