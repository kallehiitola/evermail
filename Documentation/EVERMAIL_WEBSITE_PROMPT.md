# Evermail Website Design Prompt

## Project Overview
**Website for**: Evermail - Email Archive Search Platform  
**Style inspiration**: Cryptix.framer.website (modern SaaS landing page)  
**Color scheme**: From Evermail logo - Deep Blue (#2563EB), Bright Cyan (#06B6D4)  
**Target**: Conversion-focused landing page for individuals, small businesses, and enterprises

---

## Color Palette (From Logo)

### Primary Colors
- **Deep Blue**: `#2563EB` - Main brand color, primary CTAs, headers
- **Bright Cyan**: `#06B6D4` - Accents, highlights, secondary elements
- **Slate Gray**: `#475569` - Body text, supporting text
- **White**: `#FFFFFF` - Backgrounds, clean space
- **Light Gray**: `#F1F5F9` - Section backgrounds, cards
- **Dark Charcoal**: `#1E293B` - Dark sections, footers

### Theme System (Light & Dark)
To align the product experience with the marketing reference layout, Evermail now ships with an explicit light/dark mode vocabulary. Use the following CSS tokens (defined in `wwwroot/app.css`) anywhere UI colors are needed.

| Token | Light Mode | Dark Mode | Usage |
|-------|------------|-----------|-------|
| `--color-brand-primary` | `#2563EB` | `#7AB8FF` | Primary CTAs, key icons |
| `--color-brand-accent` | `#06B6D4` | `#5DE1D6` | Secondary buttons, links, highlights |
| `--color-brand-deep` | `#0F172A` | `#E2E8F0` | Headlines, logo wordmark |
| `--color-surface` | `#FFFFFF` | `#0B1120` | Cards, panels, hero blocks |
| `--color-surface-muted` | `#F1F5F9` | `#111827` | Section backgrounds, alternating rows |
| `--color-border` | `#E2E8F0` | `#1F2937` | Dividers, card borders |
| `--color-text-primary` | `#0F172A` | `#F8FAFC` | Body text |
| `--color-text-secondary` | `#475569` | `#94A3B8` | Captions, helper text |
| `--color-progress-success` | `#10B981` | `#34D399` | Positive bars/badges |
| `--color-progress-warning` | `#F97316` | `#FB923C` | Warnings, quota alerts |

**Background Layers**
- **Base** (`--color-app-bg`): `#F8FAFC` light / `#020617` dark. Applied to `<body>`.
- **Surface** (`--color-surface`): use for hero cards, dashboard tiles.
- **Elevated Surface**: apply `box-shadow: 0 20px 45px rgba(15, 23, 42, 0.15)` in light or `rgba(2, 6, 23, 0.8)` in dark.

**Gradient Accent**
- `--color-brand-gradient`: `linear-gradient(120deg, #2563EB 0%, #06B6D4 70%)`. Use for CTA backgrounds, progress highlights, and the infinity logo stroke when rendered as a vector.

**Logo Usage**
- **Light backgrounds**: Use the transparent PNG/SVG with Deep Blue symbol + Cyan wordmark.
- **Dark backgrounds** (hero/footer): Use the black background or invert the wordmark so the infinity loop is Cyan→Blue and the word “evermail” is `#A5F3FC`.
- Maintain minimum padding equal to the height of the infinity loop on all sides.
- Do not recolor the loop outside of the gradient unless accessibility requires a solid white version.

**Implementation Notes**
- The Blazor app stores the preferred theme in `localStorage` (`evermail-theme`) and mirrors it to `data-theme` on `<html>` for instant styling.
- Users can toggle light/dark mode via the new header toggle (moon/sun icon). When no preference is stored we fall back to `prefers-color-scheme`.
- Gradients and tokens are centralized in CSS so the marketing site (Framer) and in-app UI can share the same palette.

### Usage Guidelines
- Primary CTAs: Deep Blue (#2563EB) with white text
- Hover states: Bright Cyan (#06B6D4)
- Section alternating: White and Light Gray backgrounds
- Text: Slate Gray (#475569) on light, White on dark
- Accents: Cyan for highlights, icons, badges

---

## Page Structure

### 1. Navigation Bar (Sticky)
**Style**: Clean, minimal, sticky on scroll

**Layout**:
```
[Logo] Navigation Items ----------------------- [Start Free Trial] [Sign In]
```

**Elements**:
- **Logo**: Evermail infinity symbol + wordmark (left aligned)
- **Nav Links**: 
  - Features
  - Pricing
  - How It Works
  - FAQ
  - Documentation (optional)
- **CTAs**:
  - "Start Free Trial" button (Deep Blue #2563EB)
  - "Sign In" text link (Slate Gray)

**Specs**:
- Background: White with subtle shadow on scroll
- Height: 80px
- Logo height: 40px
- Font: Inter or DM Sans, 16px, Medium (500)
- Padding: 0 80px (desktop), 0 24px (mobile)

---

### 2. Hero Section
**Inspiration**: Cryptix hero - bold headline, clear value prop, strong CTA

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│           [Your Email Archive,                      │
│            Forever Searchable]                      │
│                                                     │
│     Upload .mbox files, search years of emails,    │
│     and discover insights with AI-powered search.  │
│     Secure, private, accessible from anywhere.     │
│                                                     │
│     [Start Free Trial →]  [Watch Demo]             │
│                                                     │
│                [Hero Image/Animation]               │
│                                                     │
│     Trust badges: [SOC2] [GDPR] [Encrypted]        │
└─────────────────────────────────────────────────────┘
```

**Elements**:

1. **Headline**: 
   - Text: "Your Email Archive, Forever Searchable"
   - Font: Bold, 64px desktop / 40px mobile
   - Color: Dark Charcoal (#1E293B)
   - Max width: 800px, centered

2. **Subheadline**:
   - Text: "Upload .mbox files, search years of emails, and discover insights with AI-powered search. Secure, private, accessible from anywhere."
   - Font: Regular, 22px desktop / 18px mobile
   - Color: Slate Gray (#475569)
   - Max width: 600px, centered
   - Margin: 24px below headline

3. **CTAs** (Horizontal, centered):
   - Primary: "Start Free Trial →"
     - Style: Deep Blue (#2563EB), white text, 56px height, 24px padding
     - Hover: Bright Cyan (#06B6D4)
   - Secondary: "Watch Demo"
     - Style: Light Gray (#F1F5F9) background, Deep Blue text
     - Hover: Border highlight (Cyan)

4. **Hero Visual**:
   - Option A: 3D mockup of Evermail interface showing search results
   - Option B: Animated infinity loop with email icons flowing through
   - Option C: Screenshot of email viewer with blur effect on content
   - Size: 1200px × 700px
   - Margin: 60px above trust badges

5. **Trust Badges**:
   - Icons: SOC 2 Compliant, GDPR Ready, End-to-End Encrypted
   - Style: Grayscale icons with text, Slate Gray
   - Layout: Horizontal, centered, 32px spacing

**Background**:
- Color: White
- Optional: Subtle gradient (White to Light Gray #F1F5F9 at bottom)
- Optional: Geometric pattern (very subtle, 3% opacity)

**Padding**: 120px top, 80px bottom

---

### 3. Social Proof Section (Optional)
**Inspiration**: "They trust us" from Cryptix

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│         "Trusted by 500+ professionals"              │
│                                                     │
│    [Logo] [Logo] [Logo] [Logo] [Logo] [Logo]      │
└─────────────────────────────────────────────────────┘
```

**Elements**:
- Headline: "Trusted by 500+ professionals" (centered, Slate Gray)
- Company logos: 6-8 logos in grayscale
- Ticker animation: Logos scroll horizontally (optional)

**Background**: Light Gray (#F1F5F9)
**Padding**: 60px vertical

---

### 4. Features Section: "Why Choose Evermail?"
**Inspiration**: Cryptix "Why Choose Cryptix?" section

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│              Why Choose Evermail?                    │
│                                                     │
│   Everything you need to manage email archives     │
│                                                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│  │   [Icon]    │  │   [Icon]    │  │   [Icon]    │ │
│  │ Unlimited   │  │ AI-Powered  │  │  Blazing    │ │
│  │  Mailboxes  │  │   Search    │  │    Fast     │ │
│  │             │  │             │  │             │ │
│  │ Import all  │  │ Natural     │  │ Search      │ │
│  │ your work   │  │ language    │  │ millions of │ │
│  │ emails      │  │ queries     │  │ emails in   │ │
│  │             │  │             │  │ seconds     │ │
│  └─────────────┘  └─────────────┘  └─────────────┘ │
│                                                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│  │   [Icon]    │  │   [Icon]    │  │   [Icon]    │ │
│  │   Private   │  │   GDPR      │  │  Multi-     │ │
│  │  & Secure   │  │ Compliant   │  │  Device     │ │
│  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────┘
```

**Section Header**:
- Title: "Why Choose Evermail?"
- Font: Bold, 48px desktop / 32px mobile, Dark Charcoal
- Subtitle: "Everything you need to manage email archives"
- Font: Regular, 20px, Slate Gray
- Alignment: Centered
- Margin bottom: 60px

**Feature Cards** (3 columns desktop, 1 column mobile):

1. **Unlimited Mailboxes** ⭐ **KEY FEATURE**
   - Icon: Infinity symbol (Cyan #06B6D4)
   - Title: "Unlimited Mailboxes"
   - Description: "Import all your work history, personal accounts, and side projects. Pro users get unlimited mailbox uploads."
   
2. **AI-Powered Search**
   - Icon: Sparkle/brain (Cyan)
   - Title: "AI-Powered Search"
   - Description: "Natural language queries, semantic search, and instant summaries. Find what you need in seconds."

3. **Blazing Fast**
   - Icon: Lightning bolt (Cyan)
   - Title: "Blazing Fast"
   - Description: "Search millions of emails in milliseconds. Full-text search powered by Azure SQL."

4. **Private & Secure**
   - Icon: Lock/shield (Cyan)
   - Title: "Private & Secure"
   - Description: "End-to-end encryption, 2FA, and multi-tenant isolation. Your data, your control."

5. **GDPR Compliant**
   - Icon: Checkmark/document (Cyan)
   - Title: "GDPR Compliant"
   - Description: "Right to access, right to be forgotten, and data export. Built for regulated industries."

6. **Multi-Device**
   - Icon: Devices (Cyan)
   - Title: "Multi-Device Access"
   - Description: "Access from web, mobile (Phase 2), and API. Your archive, anywhere you go."

**Card Specs**:
- Background: White
- Border: 1px solid #E2E8F0
- Border radius: 16px
- Padding: 40px
- Hover effect: Lift (shadow), border color changes to Cyan
- Icon: 48px, Cyan circle background
- Title: 24px, Bold, Dark Charcoal
- Description: 16px, Regular, Slate Gray

**Background**: Light Gray (#F1F5F9)
**Padding**: 100px vertical, 80px horizontal

---

### 5. "How It Works" Section
**Inspiration**: Cryptix step-by-step section

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│                How It Works                          │
│                                                     │
│  From upload to search in minutes, not hours        │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │  1. Upload .mbox File                        │  │
│  │  ←──────────────────────────→                │  │
│  │  [Visual: Upload illustration]               │  │
│  │                                              │  │
│  │  Drag & drop your Gmail Takeout, Thunderbird│  │
│  │  or Apple Mail archive. Up to 5GB supported.│  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │  2. We Parse & Index                         │  │
│  │  ←──────────────────────────────→                │
│  │  [Visual: Processing animation]              │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │  3. Search & Discover                        │  │
│  │  ←──────────────────────────────────────────→    │
│  │  [Visual: Search interface]                  │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│           [Start Free Trial →]                      │
└─────────────────────────────────────────────────────┘
```

**Section Header**:
- Title: "How It Works"
- Font: Bold, 48px, Dark Charcoal
- Subtitle: "From upload to search in minutes, not hours"
- Font: Regular, 20px, Slate Gray
- Alignment: Centered

**Steps** (Alternating layout - image left/right):

1. **Upload .mbox File**
   - Number badge: "1" in Cyan circle
   - Title: "Upload .mbox File"
   - Description: "Drag & drop your Gmail Takeout, Thunderbird, or Apple Mail archive. Up to 5GB supported on Pro plan."
   - Visual: Illustration of file upload with .mbox icon

2. **We Parse & Index**
   - Number badge: "2"
   - Title: "We Parse & Index"
   - Description: "Our background worker parses your emails using MimeKit, extracts metadata, and builds a searchable index. Takes 1-5 minutes per 100MB."
   - Visual: Animation of gears/processing

3. **Search & Discover**
   - Number badge: "3"
   - Title: "Search & Discover"
   - Description: "Full-text search across subjects, senders, and content. AI-powered summaries and semantic search coming in Phase 2."
   - Visual: Screenshot of search interface

**Step Card Specs**:
- Layout: 50/50 split (text on one side, visual on other)
- Alternating: Step 1 (image right), Step 2 (image left), Step 3 (image right)
- Text padding: 60px
- Image size: 600px × 400px
- Number badge: 64px circle, Cyan background, white text

**Background**: White
**Padding**: 100px vertical

---

### 6. Security & Privacy Section ⭐ **Zero-Trust Highlight**
**Goal**: Convince everyday users their mail is safe while giving security teams tangible proof.

**Working Title Options**:
- “Your Inbox, Locked Under Your Key”
- “Zero-Trust Vault Security”
- “Encrypted Even from Us”

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│  [Eyebrow] Security & Privacy                        │
│  Your email archive, encrypted even from us.         │
│  ------------------------------------------------   │
│  [Shield Icon] Plain-language reassurance            │
│  [Code Icon] Technical assurance for security pros   │
│  [Audit Icon] Transparency & control                 │
│                                                      │
│  [Callout card: “Confidential Compute + BYOK”]       │
│  [Testimonial or badge row: SOC2 • GDPR • BYOK]      │
└─────────────────────────────────────────────────────┘
```

**Copy Blocks**:
1. **Headline**: “Encrypted Even from Our Admins”
2. **Subheadline**: “Every mailbox is sealed with your own key, processed only inside confidential Azure hardware, and logged immutably.”

**Plain-Language Column (Shield Icon)**:
- Title: “You hold the only key”
- Body: “When you upload an archive we generate a private key just for that mailbox and wrap it with a key you control. Even Evermail staff can’t peek.”
- Supporting list:
  - “Runs inside locked Azure Confidential Compute containers”
  - “If someone tried, the attempt would be blocked + logged”

**Security-Pro Column (Code Icon)**:
- Title: “Details for the paranoid (you’re among friends)”
- Body bullets:
  - “Per-mailbox AES-256-GCM DEKs, wrapped by tenant-owned Key Vault keys (BYOK/CMK)”
  - “Key release requires attested AMD SEV-SNP workloads; no attestation = no decrypt”
  - “Search tokens stored as deterministic AES-SIV ciphertext, so SQL admins only see gibberish”

**Audit Column (Clipboard Icon)**:
- Title: “Prove every access”
- Bullet ideas:
  - “Key releases mirrored to Azure Confidential Ledger”
  - “Real-time alerts for unexpected decrypts”
  - “Self-serve audit log inside the app”

**CTA**: “Read the Zero-Trust Whitepaper →” (links to blog/doc)

**Tone Guidance**:
- Use approachable language (“We can’t read your email”) followed by a short italic sentence for the security pro (“Technically: TMK → DEK -> enclave pipeline”).
- Avoid buzzwords like “military grade.” Stick to verifiable claims (Azure Confidential Compute, BYOK, SOC 2).

**Visuals**:
- Gradient lock/shield illustration with glowing vault door.
- Optional diagram showing “Your Key Vault → Evermail Confidential Worker → Encrypted Search Results”.

---

### 7. Pricing Section ⭐ **KEY SECTION**
**Inspiration**: Standard SaaS pricing table

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│           Choose Your Plan                           │
│                                                     │
│   Simple, transparent pricing. Start free,         │
│   upgrade as you grow.                             │
│                                                     │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌──────────┐ │
│  │    FREE    │ │    PRO     │ │    TEAM    │ │ ENTERPRISE│ │
│  │            │ │ [POPULAR]  │ │            │ │           │ │
│  │    €0      │ │    €9      │ │    €29     │ │    €99    │ │
│  │  /month    │ │  /month    │ │  /month    │ │  /month   │ │
│  │            │ │            │ │            │ │           │ │
│  │ ✓ 1 GB     │ │ ✓ 5 GB     │ │ ✓ 50 GB    │ │ ✓ 500 GB  │ │
│  │ ✓ 1 mailbox│ │ ✓ Unlimited│ │ ✓ Unlimited│ │ ✓ Unlimited│
│  │ ✓ 30 days  │ │   mailboxes│ │   mailboxes│ │  mailboxes│ │
│  │ ✓ Basic    │ │ ✓ 1 year   │ │ ✓ 5 users  │ │ ✓ 50 users│ │
│  │   search   │ │   retention│ │ ✓ 2 years  │ │ ✓ GDPR    │ │
│  │            │ │ ✓ AI search│ │   retention│ │   Archive │ │
│  │            │ │ ✓ Gmail/   │ │ ✓ Shared   │ │ ✓ API     │ │
│  │            │ │   Outlook  │ │   workspace│ │   access  │ │
│  │            │ │   import   │ │ ✓ API      │ │ ✓ Priority│ │
│  │            │ │            │ │   access   │ │   support │ │
│  │            │ │            │ │            │ │ ✓ Custom  │ │
│  │            │ │            │ │            │ │   SLA     │ │
│  │            │ │            │ │            │ │           │ │
│  │ [Try Free]│ │[Start Pro] │ │[Start Team]│ │[Contact]  │ │
│  └────────────┘ └────────────┘ └────────────┘ └──────────┘ │
│                                                     │
│     Annual billing: Save 2 months (10% discount)   │
│                                                     │
│          [See detailed comparison →]                │
└─────────────────────────────────────────────────────┘
```

**Section Header**:
- Title: "Choose Your Plan"
- Font: Bold, 48px, Dark Charcoal
- Subtitle: "Simple, transparent pricing. Start free, upgrade as you grow."
- Font: Regular, 20px, Slate Gray
- Alignment: Centered
- Margin bottom: 60px

**Pricing Cards** (4 columns desktop, 1 column mobile):

### FREE Plan
- **Price**: €0/month
- **Badge**: None
- **Storage**: 1 GB
- **Features**:
  - ✓ 1 mailbox only
  - ✓ 30-day retention
  - ✓ Basic full-text search
  - ✓ Manual .mbox upload
  - ✗ AI search
  - ✗ Gmail/Outlook import
- **CTA**: "Try Free" (Light Gray button, Deep Blue text)

### PRO Plan ⭐ **RECOMMENDED**
- **Price**: €9/month
- **Badge**: "MOST POPULAR" (Cyan background, white text)
- **Storage**: 5 GB
- **Features**:
  - ✓ **Unlimited mailboxes** ⭐
  - ✓ 1-year retention
  - ✓ Advanced search
  - ✓ AI-powered search (50/month)
  - ✓ Email summaries
  - ✓ Gmail/Outlook import
- **CTA**: "Start Pro" (Deep Blue button, white text)
- **Highlight**: Border glow (Cyan), slightly larger card

### TEAM Plan
- **Price**: €29/month
- **Badge**: "BEST VALUE" (optional)
- **Storage**: 50 GB
- **Features**:
  - ✓ **Unlimited mailboxes per user**
  - ✓ 5 user seats
  - ✓ 2-year retention
  - ✓ AI search (500/month)
  - ✓ Shared workspaces
  - ✓ API access (limited)
  - ✓ Priority email support
- **CTA**: "Start Team" (Deep Blue button, white text)
- **Note**: "€5.80 per user" (small text, Slate Gray)

### ENTERPRISE Plan
- **Price**: €99/month
- **Badge**: None
- **Storage**: 500 GB
- **Features**:
  - ✓ **Unlimited mailboxes**
  - ✓ 50 user seats
  - ✓ Configurable retention (1-10 years)
  - ✓ Unlimited AI features
  - ✓ GDPR Archive (immutable storage)
  - ✓ Full API access
  - ✓ Priority support (email + Slack)
  - ✓ 99.9% SLA
- **CTA**: "Contact Sales" (Light Gray button, Deep Blue text)
- **Note**: "€1.98 per user" (small text, Slate Gray)

**Card Specs**:
- Width: Equal (25% each on desktop)
- Background: White
- Border: 1px solid #E2E8F0
- Border radius: 16px
- Padding: 40px 32px
- Pro card: Border 2px Cyan, shadow, transform scale(1.05)
- Price: 48px, Bold, Dark Charcoal
- Features: 16px, Regular, Slate Gray, 24px spacing
- Checkmarks: Cyan (#06B6D4)
- X marks: Light Gray (#CBD5E1)

**Toggle** (Above cards):
- "Monthly" / "Annual" switch
- Style: Pill toggle, Deep Blue active state
- Position: Centered, 40px above cards
- Annual note: "Save 2 months" badge (Cyan)

**Footer Note**:
- Text: "Annual billing: Save 2 months (10% discount)"
- Font: 16px, Regular, Slate Gray
- Position: Centered, 40px below cards

**Comparison Link**:
- Text: "See detailed comparison →"
- Style: Cyan text, underline on hover
- Action: Expand detailed feature table (optional) or link to /pricing page

**Background**: Light Gray (#F1F5F9)
**Padding**: 100px vertical

---

### 7. Detailed Pricing Comparison Table (Optional Expansion)

**Layout** (If "See detailed comparison" clicked):
```
┌──────────────────────────────────────────────────────────────────┐
│                    Feature Comparison                            │
│                                                                  │
│  Feature               │  Free  │  Pro   │  Team   │ Enterprise │
│  ──────────────────────┼────────┼────────┼─────────┼────────────│
│  Storage               │  1 GB  │  5 GB  │  50 GB  │   500 GB   │
│  Max Mailboxes         │   1    │   ∞    │    ∞    │      ∞     │
│  Max Users             │   1    │   1    │    5    │     50     │
│  Data Retention        │ 30 days│ 1 year │  2 years│ 1-10 years │
│  Full-Text Search      │   ✓    │   ✓    │    ✓    │      ✓     │
│  AI-Powered Search     │   ✗    │   ✓    │    ✓    │      ✓     │
│  Email Summaries       │   ✗    │ 50/mo  │ 500/mo  │  Unlimited │
│  Gmail/Outlook Import  │   ✗    │   ✓    │    ✓    │      ✓     │
│  Shared Workspaces     │   ✗    │   ✗    │    ✓    │      ✓     │
│  GDPR Archive          │   ✗    │   ✗    │    ✗    │      ✓     │
│  API Access            │   ✗    │   ✗    │ Limited │     Full   │
│  Priority Support      │   ✗    │   ✗    │    ✓    │      ✓     │
│  SLA                   │  None  │  None  │  99.5%  │    99.9%   │
└──────────────────────────────────────────────────────────────────┘
```

**Table Specs**:
- Background: White card
- Header row: Light Gray background
- Alternating rows: White / very light gray (#FAFAFA)
- Checkmarks: Cyan, X marks: Light Gray
- Font: 16px, Regular
- Padding: 16px cells
- Border radius: 8px on card

---

### 8. Testimonials Section (Optional)
**Inspiration**: Cryptix testimonials carousel

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│       What Our Customers Say                         │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │  [Quote Icon]                                │  │
│  │                                              │  │
│  │  "Evermail saved me hours searching through │  │
│  │   old project emails. The AI search is      │  │
│  │   incredible—found what I needed instantly." │  │
│  │                                              │  │
│  │  [Photo]  Sarah Mitchell                    │  │
│  │           Freelance Designer                │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  [Previous] [1/3] [Next]                           │
└─────────────────────────────────────────────────────┘
```

**Testimonials** (Carousel, 3 total):

1. Sarah Mitchell, Freelance Designer
   - Quote: "Evermail saved me hours searching through old project emails. The AI search is incredible—found what I needed instantly."

2. Mark Chen, Small Business Owner
   - Quote: "We archive all our support@ emails now. The shared workspace feature is perfect for our team of 5."

3. Dr. Emma Rodriguez, Legal Compliance Officer
   - Quote: "GDPR compliance made easy. The immutable archive gives us peace of mind for regulatory requirements."

**Card Specs**:
- Width: 800px max
- Background: White
- Border radius: 16px
- Padding: 60px
- Quote icon: Cyan, 48px
- Quote text: 24px, Regular, Dark Charcoal
- Photo: 64px circle
- Name: 18px, Bold, Dark Charcoal
- Title: 16px, Regular, Slate Gray

**Background**: Light Gray (#F1F5F9)
**Padding**: 100px vertical

---

### 9. FAQ Section
**Inspiration**: Cryptix FAQ accordion

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│         Frequently Asked Questions                   │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │ What is Evermail?                          ▼ │  │
│  ├──────────────────────────────────────────────┤  │
│  │ How secure is my data?                     ▼ │  │
│  ├──────────────────────────────────────────────┤  │
│  │ What file formats do you support?         ▼ │  │
│  ├──────────────────────────────────────────────┤  │
│  │ Can I import from Gmail/Outlook?          ▼ │  │
│  ├──────────────────────────────────────────────┤  │
│  │ What happens after 30 days on Free plan?  ▼ │  │
│  ├──────────────────────────────────────────────┤  │
│  │ Do you offer refunds?                     ▼ │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│          Still have questions?                      │
│          [Contact Support →]                        │
└─────────────────────────────────────────────────────┘
```

**Section Header**:
- Title: "Frequently Asked Questions"
- Font: Bold, 48px, Dark Charcoal
- Alignment: Centered

**FAQ Items** (Accordion):

1. **What is Evermail?**
   - Answer: "Evermail is a cloud-based email archive platform that lets you upload .mbox files from Gmail, Thunderbird, or Apple Mail and search through years of emails instantly. Perfect for individuals, small businesses, and enterprises."

2. **How secure is my data?**
   - Answer: "Your data is encrypted at rest (Azure SQL TDE) and in transit (TLS 1.3). We use multi-tenant isolation, 2FA, and audit logging. We're GDPR compliant and SOC 2 Type II certified."

3. **What file formats do you support?**
   - Answer: "We support standard .mbox format (RFC 4155). This includes Gmail Takeout, Thunderbird exports, Apple Mail exports, and most email client archives."

4. **Can I import from Gmail/Outlook?**
   - Answer: "Pro and Team plans include direct OAuth import from Gmail and Outlook (coming Phase 2). Free users can download their data via Google Takeout or Outlook export."

5. **What happens after 30 days on Free plan?**
   - Answer: "Your data is automatically deleted after 30 days on the Free plan. Upgrade to Pro before the deadline to keep your archive permanently."

6. **Do you offer refunds?**
   - Answer: "Yes, we offer a 30-day money-back guarantee on all paid plans. No questions asked."

7. **How fast is mailbox processing?**
   - Answer: "Typically 1-5 minutes per 100MB. Large archives (5GB) take 15-30 minutes. You'll receive an email when processing is complete."

8. **Can I export my data?**
   - Answer: "Yes! You can export all your data as a ZIP file anytime (GDPR right to access). Includes all emails, attachments, and metadata."

**Accordion Specs**:
- Width: 800px max, centered
- Background: White
- Border: 1px solid #E2E8F0
- Border radius: 8px per item
- Padding: 24px
- Question: 18px, Bold, Dark Charcoal
- Answer: 16px, Regular, Slate Gray, 16px padding-top
- Icon: Chevron down/up, Cyan on hover
- Expanded: Light Gray (#F1F5F9) background

**CTA** (Bottom):
- Text: "Still have questions?"
- Button: "Contact Support →" (Deep Blue)

**Background**: White
**Padding**: 100px vertical

---

### 10. Final CTA Section
**Inspiration**: Strong conversion-focused CTA

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│                                                     │
│         Ready to Find That Email?                   │
│                                                     │
│    Start your free trial today. No credit card     │
│    required. Upgrade anytime.                       │
│                                                     │
│         [Start Free Trial →]                        │
│                                                     │
│         ✓ 1 GB free storage                         │
│         ✓ No credit card required                   │
│         ✓ Upgrade anytime                           │
└─────────────────────────────────────────────────────┘
```

**Elements**:
- Headline: "Ready to Find That Email?"
  - Font: Bold, 56px, White
- Subheadline: "Start your free trial today. No credit card required. Upgrade anytime."
  - Font: Regular, 22px, White (80% opacity)
- CTA: "Start Free Trial →"
  - Style: White background, Deep Blue text, 56px height
  - Hover: Slight scale, shadow
- Benefits: 3 checkmarks below CTA
  - Font: 16px, White
  - Checkmarks: White
  - Layout: Horizontal, centered

**Background**: 
- Gradient: Deep Blue (#2563EB) to Bright Cyan (#06B6D4)
- Angle: 135deg
- Or: Deep Blue solid with subtle geometric pattern

**Padding**: 120px vertical

---

### 11. Footer
**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│  [Logo]                                                     │
│                                                             │
│  Your email archive,                                        │
│  forever searchable                                         │
│                                                             │
│  Product           Company         Resources      Legal     │
│  ───────────       ────────        ─────────     ──────    │
│  Features          About Us        Blog          Privacy   │
│  Pricing           Careers         Docs          Terms     │
│  How It Works      Contact         API Docs      Security  │
│  Roadmap           Partners        Status        GDPR      │
│                                                             │
│  ─────────────────────────────────────────────────────────  │
│                                                             │
│  © 2025 Evermail. All rights reserved.                     │
│                                                             │
│  [Twitter] [LinkedIn] [GitHub]                             │
└─────────────────────────────────────────────────────────────┘
```

**Elements**:

1. **Logo & Tagline** (Left):
   - Logo: Evermail icon + wordmark, white
   - Tagline: "Your email archive, forever searchable"
   - Font: 16px, Regular, White (60% opacity)

2. **Navigation Columns** (4 columns):
   
   **Product**:
   - Features
   - Pricing
   - How It Works
   - Roadmap
   - Mobile App (coming soon)
   
   **Company**:
   - About Us
   - Careers
   - Contact
   - Partners
   - Press Kit
   
   **Resources**:
   - Blog
   - Documentation
   - API Docs
   - Status Page
   - Help Center
   
   **Legal**:
   - Privacy Policy
   - Terms of Service
   - Security
   - GDPR Compliance
   - Cookie Policy

3. **Copyright & Social**:
   - Text: "© 2025 Evermail. All rights reserved."
   - Social icons: Twitter, LinkedIn, GitHub
   - Style: White icons, 24px, hover Cyan

**Footer Specs**:
- Background: Dark Charcoal (#1E293B)
- Text color: White (60% opacity for links, 100% on hover)
- Padding: 80px horizontal, 60px top, 40px bottom
- Link font: 16px, Regular
- Link hover: Cyan (#06B6D4)

---

## Responsive Design Guidelines

### Desktop (1440px+)
- Max content width: 1440px
- Padding: 80px horizontal
- Pricing: 4 columns
- Features: 3 columns

### Tablet (768px - 1439px)
- Padding: 40px horizontal
- Pricing: 2 columns (2 rows)
- Features: 2 columns

### Mobile (< 768px)
- Padding: 24px horizontal
- Pricing: 1 column (stacked)
- Features: 1 column (stacked)
- Hero headline: 40px (down from 64px)
- Navigation: Hamburger menu

---

## Typography System

### Fonts
- **Primary**: Inter or DM Sans
- **Weights**: 400 (Regular), 500 (Medium), 600 (Semi-bold), 700 (Bold)

### Hierarchy
- **H1** (Hero headline): 64px / 700 / -0.02em
- **H2** (Section headers): 48px / 700 / -0.01em
- **H3** (Card titles): 24px / 600 / -0.01em
- **Body Large**: 22px / 400 / 0
- **Body**: 16px / 400 / 0
- **Small**: 14px / 400 / 0

---

## Animation & Interaction Guidelines

### Hover States
- **Buttons**: Scale(1.02), shadow increase
- **Cards**: Lift (translateY(-4px)), border color to Cyan
- **Links**: Color to Cyan, underline
- **Images**: Subtle zoom (scale(1.05))

### Transitions
- **Default**: all 0.3s ease-in-out
- **Fast**: 0.15s ease-in-out (for small elements)
- **Slow**: 0.6s ease-in-out (for large movements)

### Scroll Animations
- **Fade in up**: Sections fade in as user scrolls (triggered at 10% visibility)
- **Stagger**: Feature cards appear in sequence (0.1s delay each)
- **Number counters**: Animate numbers in stats (if added)

### Micro-interactions
- **CTA button**: Pulse effect on page load (subtle)
- **Logo**: Subtle rotation of infinity symbol on hover
- **Pricing toggle**: Smooth slide animation
- **Accordion**: Smooth expand/collapse

---

## CTAs & Conversion Optimization

### Primary CTA
- Text: "Start Free Trial →"
- Color: Deep Blue (#2563EB)
- Placement: Hero, How It Works, Final CTA, Pricing cards
- Copy: Clear value ("Free", "No credit card", "€9/month")

### Secondary CTA
- Text: "Watch Demo", "Sign In", "Contact Sales"
- Color: Light Gray background, Deep Blue text
- Purpose: Non-converting but engaging actions

### Urgency Elements
- "30-day money-back guarantee"
- "No credit card required"
- "Cancel anytime"
- "500+ professionals trust us"

### Trust Signals
- SOC 2, GDPR, Encrypted badges
- Customer logos (if available)
- Testimonials with photos
- "99.9% uptime" (Enterprise)

---

## Assets Needed

### Images
1. Hero image (1200×700px) - Mockup of Evermail interface
2. Upload illustration (600×400px) - Step 1
3. Processing animation (600×400px) - Step 2
4. Search interface screenshot (600×400px) - Step 3
5. Testimonial photos (64×64px circles) - 3 photos

### Icons
- Infinity symbol (for Unlimited Mailboxes feature)
- Sparkle/brain (AI search)
- Lightning bolt (Fast search)
- Lock/shield (Security)
- Checkmark/document (GDPR)
- Devices (Multi-device)

### Logos
- Evermail logo (full, icon-only, wordmark-only)
- Trust badges (SOC 2, GDPR, Encrypted)
- Social media icons (Twitter, LinkedIn, GitHub)

---

## Implementation Notes

### Technology Stack
- **Platform**: Framer (recommended for rapid iteration)
- **Alternative**: React + Tailwind CSS + Framer Motion
- **CMS**: Framer CMS for blog/testimonials (optional)

### Performance
- **Lighthouse score target**: 90+ (Performance, Accessibility)
- **Image optimization**: WebP format, lazy loading
- **Font loading**: Font-display: swap
- **Critical CSS**: Inline above-the-fold styles

### SEO
- **Title**: "Evermail - Your Email Archive, Forever Searchable"
- **Meta description**: "Upload .mbox files, search years of emails instantly, and discover insights with AI-powered search. Secure, GDPR-compliant email archiving."
- **Open Graph image**: Hero section screenshot (1200×630px)
- **Structured data**: Organization, SoftwareApplication, FAQPage

### Analytics
- **Track**: CTA clicks, pricing card interactions, FAQ expansions
- **Goals**: Free trial signups, Pro conversions
- **Tools**: Plausible or Fathom (privacy-focused)

---

## Development Workflow

### Phase 1: Design (1-2 weeks)
1. Create design in Framer or Figma
2. Review with stakeholders
3. Iterate based on feedback

### Phase 2: Build (1-2 weeks)
1. Build in Framer or code (React)
2. Add animations and interactions
3. Optimize for performance

### Phase 3: Content (1 week)
1. Write copy for all sections
2. Create or source images
3. Add real testimonials (if available)

### Phase 4: Launch (1 week)
1. Connect domain (evermail.com)
2. Set up analytics
3. Test all CTAs and forms
4. Launch! 🚀

---

## Budget Estimate

### Option 1: Framer (Fastest)
- **Framer subscription**: €20/month
- **Custom domain**: €15/year
- **Design time** (DIY): 1-2 weeks
- **Total**: ~€20-35

### Option 2: Hire Framer Expert
- **Designer**: €800-1,500 (Fiverr, Upwork, Dribbble)
- **Timeline**: 2-3 weeks
- **Includes**: Design, build, handoff

### Option 3: Custom Code
- **Developer**: €2,000-4,000
- **Timeline**: 3-4 weeks
- **Tech**: React, Tailwind, Framer Motion
- **Benefits**: Full customization, own the code

---

## Success Metrics

### Primary
- **Free trial signups**: Track conversion from hero CTA
- **Pro upgrades**: Track from pricing section
- **Time on page**: >2 minutes average

### Secondary
- **Scroll depth**: 75%+ reach footer
- **CTA clicks**: Hero, pricing, final CTA
- **FAQ interactions**: Which questions opened most

---

**Created**: November 14, 2025  
**Version**: 1.0  
**Based on**: Evermail project documentation + Cryptix.framer.website inspiration  
**Color scheme**: From Evermail logo (Deep Blue #2563EB, Cyan #06B6D4)  
**Status**: Ready for design/development

---

## Quick Start Checklist

- [ ] Review this prompt with team/stakeholders
- [ ] Decide on implementation (Framer, hire designer, or custom code)
- [ ] Gather assets (logo, icons, images)
- [ ] Write copy for each section
- [ ] Design in Framer or Figma
- [ ] Build/develop the website
- [ ] Test all CTAs and forms
- [ ] Connect domain and launch
- [ ] Set up analytics
- [ ] Monitor conversion metrics


