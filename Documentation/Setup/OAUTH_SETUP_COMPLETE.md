# OAuth Setup Complete - Testing Guide

> **Date**: 2025-11-14  
> **Status**: ✅ Google OAuth Working | ⏳ Microsoft OAuth Needs Credentials

---

## ✅ What's Implemented

### Authentication Infrastructure
- ✅ **JWT Token Service** with ES256 (ECDSA) signing
- ✅ **Authentication State Management** with localStorage
- ✅ **Custom AuthenticationStateProvider** for Blazor
- ✅ **Auto-Registration** for OAuth users (creates tenant + user)
- ✅ **Role Seeding** (User, Admin, SuperAdmin) on startup

### OAuth Providers
- ✅ **Google OAuth** - Fully configured and tested
- ⏳ **Microsoft OAuth** - Code ready, needs credentials

### User Experience
- ✅ **Login/Register Pages** with InteractiveServer render mode
- ✅ **OAuth Buttons** (Google, Microsoft) in Login page
- ✅ **Token Storage** in localStorage after successful auth
- ✅ **Navigation Menu** shows user email and logout button
- ✅ **Protected Routes** (/emails, /settings) require authentication
- ✅ **Auto-Redirect** after OAuth callback (stores token, updates state)

---

## 🧪 Testing Guide

### Test Google OAuth (Ready Now)

1. **Start Aspire:**
   ```bash
   cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
   aspire run
   ```

2. **Navigate to Login:**
   - Open WebApp URL from Aspire dashboard (e.g., `https://localhost:7136`)
   - Click "Login" in navigation menu

3. **Test Google OAuth:**
   - Click "Sign in with Google" button
   - Authenticate with your Google account
   - ✅ **Expected**: Auto-registered (tenant + user created)
   - ✅ **Expected**: Redirected to homepage
   - ✅ **Expected**: See your email in navigation menu
   - ✅ **Expected**: See "Emails" and "Settings" menu items
   - ✅ **Expected**: See "Logout" button

4. **Verify Protected Routes:**
   - Navigate to `/emails` - should work (shows user info)
   - Navigate to `/settings` - should work (shows account info)
   - Click "Logout" - should clear token and hide protected menu items

5. **Test Login Again:**
   - Click "Sign in with Google" again
   - ✅ **Expected**: Logs in with existing user (no duplicate registration)

### Test Email/Password Registration

1. **Navigate to Register:**
   - Click "Register" in navigation menu

2. **Fill Form:**
   - Email: `test@example.com`
   - Password: `SecurePass123!` (12+ chars, complexity required)
   - First Name: `John`
   - Last Name: `Doe`
   - Tenant Name: `My Company`

3. **Submit:**
   - ✅ **Expected**: User registered and auto-logged in
   - ✅ **Expected**: Redirected to homepage
   - ✅ **Expected**: See user email in navigation

---

## 🔑 Microsoft OAuth Setup (TODO)

**You need to create Microsoft OAuth credentials before testing Microsoft login.**

### Step 1: Create Azure AD App Registration

**Option A: Azure Portal** (Recommended)

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to: **Microsoft Entra ID** → **App registrations** → **New registration**
3. **Name**: `Evermail`
4. **Supported account types**: 
   - Select: **"Accounts in any organizational directory (Any Azure AD directory - Multitenant) and personal Microsoft accounts (e.g. Skype, Xbox)"**
5. **Redirect URI**: 
   - Platform: `Web`
   - URI: `https://localhost:7136/signin-microsoft` (update port if different)
6. Click **Register**

**Option B: Azure CLI**

```bash
# Login
az login

# Create app registration
az ad app create \
  --display-name "Evermail" \
  --sign-in-audience AzureADandPersonalMicrosoftAccount \
  --web-redirect-uris "https://localhost:7136/signin-microsoft"

# Note the appId (Client ID) from the response
```

### Step 2: Create Client Secret

**Azure Portal:**
1. In your new app registration, go to: **Certificates & secrets**
2. Click **New client secret**
3. Description: `Evermail Dev`
4. Expires: `24 months`
5. Click **Add**
6. **⚠️ IMPORTANT**: Copy the **Value** immediately (you can't see it again!)

**Azure CLI:**
```bash
# Create client secret (replace {APP_ID} with your Application ID)
az ad app credential reset --id {APP_ID} --append

# Copy the "password" value from the response
```

### Step 3: Store Credentials in User Secrets

**Get Application (Client) ID:**
- Azure Portal: **Overview** page → **Application (client) ID**
- Azure CLI: `appId` from creation response

**Store in user secrets:**
```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.WebApp/Evermail.WebApp

dotnet user-secrets set "Authentication:Microsoft:ClientId" "YOUR_CLIENT_ID_HERE"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "YOUR_CLIENT_SECRET_HERE"
```

**Verify stored:**
```bash
dotnet user-secrets list
```

Expected output:
```
Authentication:Google:ClientId = 341587598590-i1pijqvog5fbdk6u9v50reptfh1fqjak.apps.googleusercontent.com
Authentication:Google:ClientSecret = [hidden]
Authentication:Microsoft:ClientId = YOUR_CLIENT_ID
Authentication:Microsoft:ClientSecret = [hidden]
```

### Step 4: Update Redirect URI if Port Changes

If Aspire assigns a different port to WebApp:
1. Check current port in Aspire dashboard
2. Update redirect URI in Azure Portal:
   - **Microsoft Entra ID** → **App registrations** → **Evermail**
   - **Authentication** → **Platform configurations** → **Web**
   - Update redirect URI to match current port

### Step 5: Test Microsoft OAuth

1. Restart Aspire after adding credentials
2. Navigate to `/login`
3. Click "Sign in with Microsoft"
4. ✅ **Expected**: Redirected to Microsoft login
5. ✅ **Expected**: Authenticate with personal or work account
6. ✅ **Expected**: Auto-registered (tenant + user created)
7. ✅ **Expected**: Redirected to homepage with auth state

---

## 🏗️ Architecture Overview

### Authentication Flow

```
┌─────────────────────────────────────────────────────────────┐
│ OAuth Flow (Google/Microsoft)                               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ 1. User clicks "Sign in with Google/Microsoft"             │
│    └─> Login.razor → /api/v1/auth/{provider}/login         │
│                                                             │
│ 2. Redirect to OAuth provider                              │
│    └─> Google/Microsoft authentication                     │
│                                                             │
│ 3. OAuth callback                                          │
│    └─> /api/v1/auth/{provider}/callback                    │
│                                                             │
│ 4. Find or create user                                     │
│    ├─> User exists? → Fetch user                           │
│    └─> New user? → Create Tenant + User + Assign "User" role│
│                                                             │
│ 5. Generate JWT token (15min expiry)                       │
│    └─> Claims: sub, tenant_id, email, roles                │
│                                                             │
│ 6. Redirect to home with token                             │
│    └─> /?token=eyJhbGciOiJFUzI1NiIs...                     │
│                                                             │
│ 7. Login.razor receives token                              │
│    ├─> Store in localStorage                                │
│    ├─> Notify AuthenticationStateProvider                  │
│    └─> Redirect to clean URL                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Email/Password Flow                                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ 1. User fills registration form                            │
│    └─> POST /api/v1/auth/register                          │
│    └─> Creates: Tenant + User (with hashed password)       │
│                                                             │
│ 2. Returns JWT token                                       │
│    └─> Register.razor stores token in localStorage         │
│                                                             │
│ 3. User fills login form                                   │
│    └─> POST /api/v1/auth/login                             │
│    └─> Validates credentials + 2FA (if enabled)            │
│                                                             │
│ 4. Returns JWT token                                       │
│    └─> Login.razor stores token in localStorage            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Token Storage & State Management

```csharp
// 1. AuthenticationStateService (localStorage wrapper)
await AuthStateService.SetTokenAsync(token);  // Store
var token = await AuthStateService.GetTokenAsync();  // Retrieve
await AuthStateService.RemoveTokenAsync();  // Logout

// 2. CustomAuthenticationStateProvider (auth state)
public override Task<AuthenticationState> GetAuthenticationStateAsync()
{
    // Validates JWT token
    // Returns ClaimsPrincipal if valid
    // Returns anonymous if invalid/missing
}

// 3. Notification after login/logout
if (AuthStateProvider is CustomAuthenticationStateProvider provider)
{
    provider.NotifyAuthenticationStateChanged();
}
```

### Protected Routes

```razor
@page "/emails"
@rendermode InteractiveServer
@attribute [Authorize]  ← Requires authentication

<AuthorizeView>
    <Authorized>
        @* Content for authenticated users *@
        <p>Welcome, @context.User.Identity?.Name</p>
    </Authorized>
    <NotAuthorized>
        <p>Please <a href="/login">login</a>.</p>
    </NotAuthorized>
</AuthorizeView>
```

---

## 🔒 Security Features

### JWT Token
- **Algorithm**: ES256 (ECDSA with P-256 curve)
- **Expiry**: 15 minutes
- **Claims**: `sub`, `tenant_id`, `email`, `given_name`, `family_name`, `role`
- **Issuer**: `https://api.evermail.com`
- **Audience**: `evermail-webapp`

### Password Requirements (Email/Password Registration)
- Minimum 12 characters
- Requires: uppercase, lowercase, digit, non-alphanumeric
- Lockout: 5 failed attempts = 15 minutes lockout

### OAuth Security
- **EmailConfirmed = true** for OAuth users (pre-verified by provider)
- **Tenant isolation** - each user gets their own tenant
- **Role-based access** - default "User" role assigned

---

## 📁 Files Created/Modified

### New Files
```
Evermail.WebApp/Services/
├── AuthenticationStateService.cs       # Token management service
└── CustomAuthenticationStateProvider.cs # Blazor auth state provider

Evermail.WebApp/Components/Pages/
├── Emails.razor                        # Protected route example
└── Settings.razor                      # Protected route example
```

### Modified Files
```
Evermail.WebApp/
├── Program.cs                          # Added Microsoft OAuth, auth services
├── Endpoints/OAuthEndpoints.cs         # Auto-registration logic
├── Components/Pages/Login.razor        # Token handling, OAuth flow
├── Components/Pages/Register.razor     # Token handling
└── Components/Layout/NavMenu.razor     # User display, logout, conditional menu

Evermail.Infrastructure/
└── Data/DataSeeder.cs                  # Role seeding (User, Admin, SuperAdmin)
```

---

## 🧪 Test Checklist

### Google OAuth ✅
- [ ] Click "Sign in with Google"
- [ ] Authenticate with Google account
- [ ] Verify: User auto-registered (check database)
- [ ] Verify: Redirected to homepage
- [ ] Verify: Email shown in navigation
- [ ] Verify: Can access /emails and /settings
- [ ] Verify: Logout works
- [ ] Verify: Login again with same account (no duplicate)

### Microsoft OAuth ⏳
- [ ] Set up Azure AD app registration
- [ ] Store credentials in user secrets
- [ ] Update redirect URI if needed
- [ ] Click "Sign in with Microsoft"
- [ ] Authenticate with Microsoft account
- [ ] Verify: Same flow as Google

### Email/Password ✅
- [ ] Register new account
- [ ] Verify: Auto-logged in after registration
- [ ] Logout
- [ ] Login with email/password
- [ ] Verify: Token stored, navigation updated

### Protected Routes ✅
- [ ] Try accessing /emails without login → redirected
- [ ] Login → can access /emails
- [ ] Verify /settings also requires auth

---

## 📊 Database Changes

When OAuth user registers, database records created:

**Tenants Table:**
```sql
Id: new Guid
Name: "John Doe" (from OAuth profile)
CreatedAt: UTC now
```

**AspNetUsers Table:**
```sql
Id: new Guid
TenantId: linked to tenant
UserName: email from OAuth
Email: email from OAuth
EmailConfirmed: true (OAuth pre-verified)
FirstName: from OAuth claims
LastName: from OAuth claims
PasswordHash: NULL (OAuth users don't have passwords)
CreatedAt: UTC now
```

**AspNetRoles Table (seeded on startup):**
```sql
- User (default for all users)
- Admin (for tenant administrators)
- SuperAdmin (for platform administrators)
```

**AspNetUserRoles Table:**
```sql
UserId: user.Id
RoleId: "User" role ID
```

---

## 🔧 Configuration Files

### User Secrets Location
```
~/.microsoft/usersecrets/a7d4b7bc-5b4a-44de-ad89-7868680ed698/secrets.json
```

### Current Secrets (Google Only)
```json
{
  "Authentication:Google:ClientId": "341587598590-i1pijqvog5fbdk6u9v50reptfh1fqjak.apps.googleusercontent.com",
  "Authentication:Google:ClientSecret": "[REDACTED]"
}
```

### Secrets Needed for Microsoft
```json
{
  "Authentication:Microsoft:ClientId": "YOUR_AZURE_APP_CLIENT_ID",
  "Authentication:Microsoft:ClientSecret": "YOUR_AZURE_CLIENT_SECRET"
}
```

---

## 🚀 How It Works

### Component Render Modes

**Login/Register Pages:**
```razor
@page "/login"
@rendermode InteractiveServer  ← Enables @onclick, forms, API calls
```

**Why InteractiveServer?**
- ✅ Buttons work (@onclick)
- ✅ Forms work (OnValidSubmit)
- ✅ Can make API calls (HttpClient)
- ✅ Fast initial load (no WASM download)
- ✅ Secure (code stays on server)

**Navigation Menu:**
```razor
@rendermode InteractiveServer  ← Needed for logout button
```

### Token Flow

```
OAuth Callback → ?token=JWT_TOKEN
       ↓
Login.OnInitializedAsync()
       ↓
AuthStateService.SetTokenAsync(token)
       ↓
localStorage.setItem("evermail_auth_token", token)
       ↓
CustomAuthenticationStateProvider.NotifyAuthenticationStateChanged()
       ↓
GetAuthenticationStateAsync()
       ↓
Validates JWT → Returns ClaimsPrincipal
       ↓
AuthorizeView components update
       ↓
Navigation menu shows user info + logout
```

### Protected Route Behavior

```razor
@attribute [Authorize]

When user navigates to /emails:
1. AuthenticationStateProvider checks for token
2. Token exists? → Validate JWT
3. Valid? → Show page content
4. Invalid/Missing? → Show NotAuthorized content
```

---

## 🎯 Next Session Tasks

### Immediate (After Microsoft OAuth Setup)
- [ ] Create Azure AD app registration
- [ ] Store Microsoft OAuth credentials
- [ ] Test Microsoft login flow
- [ ] Verify both providers work

### Phase 1 - Email Parsing (Week 2)
- [ ] Install MimeKit package
- [ ] Create mbox upload endpoint
- [ ] Implement streaming parser
- [ ] Configure Azure Blob Storage
- [ ] Configure Azure Storage Queues
- [ ] Create IngestionWorker service
- [ ] Test email parsing and storage

---

## 📚 Reference Documentation

**Microsoft OAuth Setup:**
- [Microsoft Account external login setup](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-10.0)
- [Register an application with Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=client-secret)

**Blazor Authentication:**
- [ASP.NET Core Blazor authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)
- [ASP.NET Core Blazor render modes (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)

**JWT Tokens:**
- [JWT Introduction](https://jwt.io/introduction)
- [ES256 Algorithm](https://datatracker.ietf.org/doc/html/rfc7518#section-3.4)

---

## ✅ Success Criteria

**Authentication is working when:**
1. ✅ Google OAuth creates new users automatically
2. ✅ Email/password registration works
3. ✅ Email/password login works
4. ✅ Token stored in localStorage
5. ✅ Navigation menu shows user email
6. ✅ Protected routes require login
7. ✅ Logout removes token and updates UI
8. ⏳ Microsoft OAuth works (after credentials added)

---

## 🐛 Known Issues

### Port Changes
- **Issue**: WebApp port is dynamic (Aspire assigns it)
- **Fix**: Update redirect URIs in OAuth providers when port changes
- **Google Console**: https://console.cloud.google.com/apis/credentials
- **Azure Portal**: https://portal.azure.com/ → Entra ID → App registrations

### Token in URL
- **Current**: Token passed as query parameter (`?token=...`)
- **Phase 2**: Implement secure cookie-based token storage for better security
- **Phase 2**: Implement token refresh mechanism (15min expiry)

---

**Ready to test!** 🚀

Google OAuth should work immediately. Microsoft OAuth needs credentials first.

