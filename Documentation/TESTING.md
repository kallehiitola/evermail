# Testing Evermail - Practical Guide

> **How to test what we've built so far (Phase 0)**  
> No unit tests needed - just practical API testing

---

## 🚀 Prerequisites

1. **Aspire running**: 
   ```bash
   cd Evermail/Evermail.AppHost
   dotnet run
   ```
   Dashboard token will print in console

2. **REST Client** installed:
   - VS Code: [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
   - Or use `curl` commands below
   - Or use Postman

---

## ✅ What We Can Test Right Now

### Phase 0 Complete - Authentication is Live!

We can test:
1. ✅ **User Registration** - Create tenant and user
2. ✅ **User Login** - Get JWT token
3. ✅ **2FA Setup** - Enable two-factor auth
4. ✅ **2FA Verification** - Validate TOTP code
5. ✅ **Database** - Check data was saved

---

## 📋 Testing Steps

### Step 1: Start Aspire

```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
dotnet run
```

**Look for**:
```
Login to the dashboard at https://localhost:17134/login?t=TOKEN_HERE
```

**Open the dashboard** to monitor logs and database.

---

### Step 2: Find the WebApp URL

In the Aspire Dashboard (https://localhost:17134), look at the **Resources** tab.

Find **webapp** and note its endpoint (probably `https://localhost:XXXXX`)

Let's assume it's: **https://localhost:5001**

---

### Step 3: Test User Registration

**Create a file**: `test-api.http` (VS Code REST Client format)

```http
### Register a new user
POST https://localhost:5001/api/v1/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePass123!",
  "firstName": "Test",
  "lastName": "User",
  "tenantName": "Test Company"
}
```

**Or use curl**:
```bash
curl -k -X POST https://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!",
    "firstName": "Test",
    "lastName": "User",
    "tenantName": "Test Company"
  }'
```

**Expected Response**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGci...long-jwt-token",
    "expiresAt": "2025-11-12T02:30:00Z",
    "user": {
      "id": "guid",
      "tenantId": "guid",
      "email": "test@example.com",
      "firstName": "Test",
      "lastName": "User",
      "twoFactorEnabled": false
    }
  }
}
```

**✅ What This Tests**:
- User registration works
- Tenant is created automatically
- Password validation works
- JWT token is generated
- Database insert works

---

### Step 4: Test User Login

```http
### Login
POST https://localhost:5001/api/v1/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePass123!"
}
```

**Or curl**:
```bash
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!"
  }'
```

**Expected**: Same response as registration (JWT token)

**✅ What This Tests**:
- Login authentication works
- Password verification works
- JWT generation works
- LastLoginAt updates

---

### Step 5: Test 2FA Setup

**Copy the token from Step 3 or 4**, then:

```http
### Enable 2FA (requires JWT token)
POST https://localhost:5001/api/v1/auth/enable-2fa
Authorization: Bearer YOUR_TOKEN_HERE
```

**Or curl**:
```bash
curl -k -X POST https://localhost:5001/api/v1/auth/enable-2fa \
  -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"
```

**Expected Response**:
```json
{
  "success": true,
  "data": {
    "qrCodeUrl": "https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=...",
    "secret": "JBSWY3DPEHPK3PXP"
  }
}
```

**✅ What This Tests**:
- JWT authentication works
- 2FA secret generation works
- QR code generation works

**Open the QR code URL** in browser to see the QR code!

---

### Step 6: Verify Database

**Option A: Use Aspire Dashboard**

1. Open dashboard: https://localhost:17134
2. Go to **Resources** tab
3. Find **sql** resource
4. You can't query directly, but you can see it's running

**Option B: Connect with a DB tool**

The SQL Server container is running. Connection details:
- **Server**: localhost (port shown in Aspire dashboard)
- **Database**: Evermail
- **Auth**: Usually SQL auth with generated password

**Check in Aspire Dashboard** under **sql** resource → Environment variables to see connection string.

**Option C: Use EF Core CLI** (easiest):

```bash
cd Evermail
dotnet ef dbcontext info --project Evermail.Infrastructure --startup-project Evermail.WebApp/Evermail.WebApp
```

This shows if database is accessible.

---

## 🔍 What to Check in Database

After testing registration, the database should have:

**Tables Created** (from Identity + our entities):
- AspNetUsers (your test user)
- AspNetRoles
- Tenants (your test tenant)
- SubscriptionPlans (4 plans: Free, Pro, Team, Enterprise)
- Subscriptions (empty for now)
- Mailboxes (empty for now)
- EmailMessages (empty for now)
- Attachments (empty for now)
- AuditLogs (empty for now)

---

## 🐛 Troubleshooting

### "Connection refused" on API

**Check**:
1. Is Aspire running? (`dotnet run` in AppHost)
2. Check the actual port in Aspire Dashboard → Resources → webapp
3. Use that port number (not 5001)

### "Unauthorized" on protected endpoints

**Make sure**:
1. You copied the full JWT token from register/login response
2. Token hasn't expired (15 minutes)
3. Header is: `Authorization: Bearer TOKEN` (with space after Bearer)

### "Invalid token" in Aspire Dashboard

**This is normal**:
- Token changes each restart
- Copy the new token from console output: `Login to the dashboard at...`
- Click the full URL (includes `?t=token`)

### Database connection errors

**Check**:
1. SQL container is running (Aspire Dashboard → Resources → sql)
2. Wait 30 seconds after starting Aspire (SQL takes time to start)
3. Check Aspire Dashboard → sql → Console Logs for errors

---

## ✅ Success Criteria

You should be able to:
- ✅ Register a new user → Get JWT token
- ✅ Login with that user → Get JWT token
- ✅ Enable 2FA with token → Get QR code URL
- ✅ See user in database (if you can access it)
- ✅ No errors in Aspire Dashboard logs

---

## 🎯 What We CAN'T Test Yet

These aren't implemented yet (coming in Phase 1+):
- ❌ Mailbox upload (Phase 1)
- ❌ Email search (Phase 2)
- ❌ Email viewing (Phase 3)
- ❌ Stripe payments (Phase 4)
- ❌ Admin dashboard features (Phase 5)

---

## 📝 Quick Test Script

Save as `test-auth.sh`:

```bash
#!/bin/bash

API_URL="https://localhost:5001"

echo "1. Registering user..."
RESPONSE=$(curl -k -s -X POST $API_URL/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!",
    "firstName": "Test",
    "lastName": "User",
    "tenantName": "Test Company"
  }')

echo "$RESPONSE" | jq '.'
TOKEN=$(echo "$RESPONSE" | jq -r '.data.token')

echo ""
echo "2. Logging in..."
curl -k -s -X POST $API_URL/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!"
  }' | jq '.'

echo ""
echo "3. Enabling 2FA..."
curl -k -s -X POST $API_URL/api/v1/auth/enable-2fa \
  -H "Authorization: Bearer $TOKEN" | jq '.'

echo ""
echo "✅ Tests complete!"
```

**Run**: `chmod +x test-auth.sh && ./test-auth.sh`

(Requires `jq` installed: `brew install jq`)

---

## 🎊 Summary

**Phase 0 Testing**:
- ✅ Can test registration, login, 2FA setup
- ✅ Can verify JWT tokens are generated
- ✅ Can check database tables exist
- ✅ Can monitor in Aspire Dashboard

**Next**: Build email parsing (Phase 1) and test mailbox upload!

---

**Last Updated**: 2025-11-12  
**What's Testable**: Authentication endpoints  
**What's Not**: Everything else (not built yet)

