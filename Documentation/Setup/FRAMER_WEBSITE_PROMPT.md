# Evermail Marketing Website Creation Prompt for Framer

> **⚠️ CRITICAL: MARKETING WEBSITE ONLY**
> 
> This prompt is EXCLUSIVELY for the public-facing marketing website (evermail.com).
> 
> **This is NOT for:**
> - ❌ The Blazor SaaS application (app.evermail.com)
> - ❌ Email viewer UI
> - ❌ Search interface
> - ❌ Admin dashboard
> - ❌ Any authenticated user features
> 
> **Framer = Marketing Site | Blazor/MudBlazor = Actual Application**

> **Ready to use with Framer MCP** - Copy this entire prompt and use it with your Framer project via Cursor's Framer MCP integration.

---

## 🎨 Project Brief

Create a modern, conversion-optimized **marketing website** for **Evermail** - a SaaS platform that helps people upload, explore, and search their email archives with AI. Think of it as "Dropbox for your old emails" - upload once, search from anywhere, no need to keep giant files on your computer.

**Target Message**: Simple, secure email archiving that just works. No technical jargon - focus on benefits customers care about.

## 🎯 Target Audience (Balanced Approach)
- **Individual professionals** seeking personal email archiving (€9/month)
- **Freelancers** needing organized communication history
- **Small businesses & teams** requiring collaborative email management (€29-99/month)
- **Enterprises** with compliance requirements (GDPR, legal hold)

## 🎨 Design System (2025 Best Practices)

### Color Palette (Conversion-Optimized)

**Primary Colors:**
- **Azure Blue** `#0078D4` - Trust, security, professionalism (Microsoft Azure association)
- **Deep Navy** `#001F3F` - Enterprise credibility, stability
- **Bright Cyan** `#00D4FF` - Modern, tech-forward, innovation (accent)

**Secondary Colors:**
- **Success Green** `#00C851` - Positive actions, success states
- **Warm Amber** `#FFB300` - Call attention, urgency (CTAs)
- **Soft Purple** `#7B68EE` - AI/future tech (for AI features)

**Neutrals:**
- **Pure White** `#FFFFFF` - Clean backgrounds, breathing room
- **Light Gray** `#F5F7FA` - Sections, cards, subtle contrast
- **Medium Gray** `#6C757D` - Secondary text
- **Dark Gray** `#212529` - Primary text (not pure black for softer feel)

### Typography
- **Headings:** Inter or Geist (modern, clean, tech-forward)
- **Body:** System fonts for performance
- **Font Sizes:** 
  - Hero: 56-72px (mobile: 36-42px)
  - H1: 48px (mobile: 32px)
  - H2: 36px (mobile: 28px)
  - Body: 18px (mobile: 16px)

### Design Style
- **Minimalist & Clean:** Ample white space
- **Micro-animations:** Subtle hover effects, smooth scroll
- **Glass morphism:** Semi-transparent cards with backdrop blur
- **3D illustrations:** Isometric email/archive visuals
- **Gradients:** Subtle Azure to Cyan gradients
- **Dark mode support:** Essential for 2025

---

## 📄 Page Structure

### 1. Homepage

**Navigation:**
```
[Logo: Evermail] | Features | Pricing | Security | Resources | Sign In | [START FREE TRIAL]
```

**Hero Section:**
```
Headline: "Your Email Archive. Searchable. Secure. Always Accessible."
Subheadline: "Stop drowning in .mbox files. Upload once, search forever with AI-powered intelligence. No local storage needed."

[Primary CTA: "Start Free Trial - 1GB Free"] [Secondary CTA: "See How It Works ↓"]

[Hero Visual: 3D animated mailbox with emails flowing into secure cloud vault]
```

**Trust Bar:**
- 🔒 GDPR Compliant
- ⚡ Built on Microsoft Azure
- 🤖 AI-Powered Search
- 💳 Stripe Secure Payments

**Problem-Solution Section:**
```
THE PROBLEM:
[Visual: Frustrated person with massive 5GB .mbox file]
"Email archives are massive, slow, and stuck on one device"

THE EVERMAIL SOLUTION:
[Visual: Email floating in cloud, accessible from devices]
"Upload once. Search from anywhere. AI finds what you need in seconds."
```

**Key Features (3-Column Grid):**

**🤖 AI-Powered Search**
- "Ask in plain English: 'Find emails from John about the invoice'"
- Search years of emails in seconds
- Smart filters make finding anything easy

**🔒 Free Up Space**
- "Stop storing giant email files on your computer"
- Access your archive from any device
- Secure cloud storage, just like Dropbox

**⚡ Lightning Fast**
- "Upload once, processed in minutes"
- Modern interface that's actually fast
- No more waiting for files to load

**🛡️ Bank-Level Security**
- "Your emails are encrypted and private"
- GDPR compliant - you own your data
- Same security as online banking

**💰 Honest Pricing**
- "Start free. Upgrade when you need more space."
- No hidden fees. No surprise charges.
- Save thousands vs. enterprise tools

**📱 Works Everywhere**
- "Any browser, any device"
- Mobile apps coming soon
- Share with your team (Team plan+)

**Social Proof Section:**
```
"Trusted by Professionals & Teams"

Testimonials (3 cards):
1. "Evermail saved me 20 hours searching for old client emails. The AI search is magic." 
   - Sarah Chen, Freelance Designer

2. "We needed GDPR compliance for client communications. Evermail delivered at 1/10th the cost of enterprise tools."
   - Marcus Rodriguez, Legal Counsel, TechCorp

3. "Finally, a modern email archive that doesn't feel like software from 2005."
   - Priya Patel, Startup Founder
```

**How It Works (4-Step Visual Process):**
```
1. UPLOAD → "Drag & drop your .mbox file (up to 5GB)"
2. PROCESS → "Our parser extracts metadata, indexes content"
3. SEARCH → "Use natural language or filters to find anything"
4. EXPLORE → "Read, download attachments, export results"
```

**Final CTA Section:**
```
"Ready to Free Your Email Archive?"
[Large button: "Start Free Trial - No Credit Card"] 
"1GB free forever. Upgrade to Pro for €9/month."
```

---

### 2. Pricing Page

**Toggle:** [Monthly] / [Yearly - Save 17%]

**4-Column Pricing Table:**

```
┌─────────────┬──────────────┬──────────────┬──────────────┐
│    FREE     │   PRO ⭐     │  TEAM 🔥     │  ENTERPRISE  │
│   €0/month  │  €9/month    │  €29/month   │  €99/month   │
├─────────────┼──────────────┼──────────────┼──────────────┤
│ 1 GB        │ 5 GB         │ 50 GB        │ 500 GB       │
│ 1 mailbox   │ Unlimited    │ Unlimited    │ Unlimited    │
│ 30-day keep │ 1-year keep  │ 2-year keep  │ 1-10 year    │
│ Basic search│ AI search ✨ │ AI search ✨ │ AI search ✨ │
│ ❌ Summaries│ 50/month     │ 500/month    │ Unlimited    │
│ 1 user      │ 1 user       │ 5 users      │ 50 users     │
│ ❌ Workspace│ ❌ Workspace │ ✅ Team space│ ✅ Team space│
│ ❌ API      │ ❌ API       │ 10K req/mo   │ 100K req/mo  │
│ ❌ GDPR Arc │ ❌ GDPR Arc  │ ❌ GDPR Arc  │ ✅ Immutable │
└─────────────┴──────────────┴──────────────┴──────────────┘
   [Try Free]  [START FREE    [Contact      [Contact
                TRIAL ⚡]      Sales]        Sales]
```

**Badge on Team:** "MOST POPULAR"
**Badge on Pro:** "BEST VALUE"

**FAQ Section (Accordion):**
- "Can I upgrade/downgrade anytime?" → Yes, instant, prorated
- "What happens if I exceed storage?" → Notification, option to upgrade
- "Is my data secure?" → Link to Security page
- "Do you offer refunds?" → 30-day money-back guarantee

---

### 3. Features Page

**Sections:**

**Search & Discovery**
- Search like you're talking to a person
- AI finds what you're looking for instantly
- Filter by date, sender, or attachments
- Get summaries of long email threads
- See entire conversations in context

**Easy Import**
- Drag and drop your .mbox file (from Gmail, Thunderbird, Apple Mail)
- Connect directly to Gmail/Outlook (coming soon)
- Handles old or damaged email files
- View attachments right in your browser
- Watch progress as your emails are imported

**Security You Can Trust**
- Two-factor authentication for your account
- Everything encrypted like online banking
- European data centers (GDPR compliant)
- Export your data anytime you want
- Permanent archive for legal requirements (Enterprise)
- Complete activity log of who accessed what

**Team Collaboration (Team & Enterprise)**
- Share email archives with your team
- Control who can see what
- Discuss emails with comments
- Export findings for reports

---

### 4. Security Page

**Hero:**
"Your Emails. Your Data. Completely Secure."

**Sections:**

**Encryption**
- Your emails are encrypted in transit and at rest
- Same technology banks use to protect your money
- Microsoft-managed security keys

**Access Control**
- Two-factor authentication (like Gmail and banking apps)
- Control who in your team can access what
- Your data is isolated from other customers
- Secure temporary links for downloading (expire in 15 minutes)

**Compliance**
- GDPR compliant with European data centers
- Export all your data anytime
- Delete your account and data permanently
- Legal hold option for regulated industries

**Infrastructure**
- Hosted on Microsoft Azure's secure cloud
- Multiple backup copies in different locations
- 99.9% uptime guarantee (Enterprise tier)

**Certifications (Badges):**
[GDPR] [ISO 27001] [SOC 2] [Azure Secure]

---

### 5. About Page

**Mission:**
"We believe your email history should be accessible, searchable, and secure - without requiring a supercomputer."

**Story:**
"Born from frustration with bloated email clients and expensive enterprise archiving tools, Evermail was built to make email archiving accessible to everyone."

**Built With Modern Technology:**
"Powered by Microsoft Azure cloud platform for speed, security, and reliability"

**Roadmap:**
- Q2 2025: Gmail/Outlook OAuth import
- Q3 2025: Mobile apps (iOS, Android)
- Q4 2025: Advanced AI features

---

### 6. Contact Page

**Support:**
- Email: support@evermail.com
- Documentation: docs.evermail.com
- Community: Discord

**Sales:**
- Enterprise demos: sales@evermail.com

**Form:**
[Name] [Email] [Subject] [Message] [Send]

---

## 🎯 Key UX/UI Requirements

### Navigation
- **Sticky header** with transparent-to-solid on scroll
- **Mobile hamburger menu** with smooth animation
- **Clear hierarchy:** Logo left, links center, CTA right

### CTAs (Calls-to-Action)
- **Primary CTA:** Warm Amber `#FFB300` with hover lift
- **Copy variations:**
  - "Start Free Trial" (homepage)
  - "Get Started Free" (features)
  - "Try Evermail Now" (blog)
- **Secondary CTA:** Outlined Azure Blue
- **Size:** Min 44x44px for mobile

### Mobile-First Design
- **Breakpoints:** 320px, 768px, 1024px, 1440px
- **Touch-friendly:** All buttons ≥44px
- **Performance:** <2s First Contentful Paint

### Accessibility (WCAG 2.1 AA)
- **Color contrast:** Min 4.5:1 for text
- **Alt text** on all images
- **Keyboard navigation**
- **Screen reader friendly**

### Animations
- **Page load:** Fade-in with stagger
- **Scroll:** Fade-in sections on viewport entry
- **Hover:** Scale 1.05 on cards
- **Loading:** Skeleton screens

### Trust Signals
- **Security badges** in footer
- **Customer testimonials** with photos
- **Transparent pricing**
- **Money-back guarantee**

---

## 📱 Technical Requirements

### Performance
- **Lighthouse score:** >90
- **Image optimization:** WebP with PNG fallback
- **Font loading:** `font-display: swap`

### SEO
- **Unique meta tags** per page
- **Schema.org markup**
- **Sitemap.xml**
- **Open Graph tags**

---

## 🎨 Design References

**Style inspiration:**
- Linear.app (clean, modern)
- Vercel.com (minimalist)
- Stripe.com (professional)
- Supabase.com (developer-friendly)

**Icons:** Lucide Icons (clean, modern)
**Fonts:** Inter or Geist
**Illustrations:** 3D isometric style

---

## 💡 Key Messages to Emphasize (Customer-Focused)

1. **Free Up Space:** "Stop storing giant files - access from any device"
2. **Search Like a Human:** "Ask in plain English, find anything instantly"
3. **Your Data is Safe:** "Bank-level security, you own your emails"
4. **Save Money:** "Start free, upgrade for just €9/month - not €100s"
5. **No Tech Skills Needed:** "If you can use Dropbox, you can use Evermail"

## ❌ Avoid Technical Jargon

**Don't say:**
- "Built on .NET 9 and Azure Aspire"
- "SQL Server Full-Text Search with FTS indexing"
- "Multi-tenant architecture with global query filters"
- "MimeKit streaming parser for mbox files"
- "EF Core with compiled queries"

**Instead say:**
- "Powered by Microsoft's secure cloud"
- "Lightning-fast search technology"
- "Each customer's data is completely isolated"
- "Handles all email formats automatically"
- "Optimized for speed and reliability"

**Remember**: The marketing website is for customers, not developers. Save technical details for documentation.

---

## 🚀 Conversion Goals

1. **Free trial signups** (no credit card required)
2. **Paid subscriptions** (Pro €9, Team €29, Enterprise €99)
3. **Email list** for product updates

---

## 📝 Voice & Tone

- **Modern:** Conversational, not corporate
- **Confident:** "Never lose an email again"
- **Benefit-driven:** Focus on outcomes
- **Inclusive:** "Upload your archive"

**Headline Formula:**
`[Desired Outcome] + [Without Pain Point] + [With Key Benefit]`

Examples:
- "Search Years of Email in Seconds. No AI PhD Required."
- "Your Complete Email History. Zero Local Storage."
- "Archive Smarter. Search Faster. Save Money."

---

**Ready to build the marketing website? Let's make it incredible! 🚀**

---

## 🏗️ Architecture Clarity

**This Framer website is for**:
✅ Marketing and customer acquisition (evermail.com)
✅ Landing pages, pricing, features, about, contact
✅ Converting visitors to signups

**This is NOT for**:
❌ The actual Blazor SaaS application (app.evermail.com)
❌ Email viewer interface
❌ Search functionality UI
❌ Admin dashboard

**Tech Stack Split**:
```
Marketing Website (Framer) ← YOU ARE HERE
├── evermail.com
├── Public-facing content
└── Conversion-focused design

SaaS Application (Blazor + MudBlazor) ← Separate project
├── app.evermail.com
├── Authenticated user interface
└── Actual email viewing and search
```

---

## 🔗 References

Based on 2025 web design best practices:
- [SaaS Website Best Practices 2025](https://www.webstacks.com/blog/saas-website-best-practices)
- [Modern SaaS Design Trends](https://www.pineapple.pink/blog/saas-websites-in-2025-the-ultimate-guide-for-success)
- [Conversion-Optimized Color Psychology](https://www.designity.com/blog/saas-website-design-best-practices-that-actually-convert)
- [Framer MCP Setup Guide](https://unframer.co/guides/connect-framer-mcp)

