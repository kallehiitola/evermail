# Azure Storage Setup for Evermail

## 🎯 Recommended Architecture (from Microsoft Learn)

**Use ONE Storage Account for all tenants:**
- ✅ Capacity: 5 PB (5,000 TB) - handles 2,500+ Enterprise customers
- ✅ No limit on containers or blobs
- ✅ 40,000 requests/second
- ✅ Much simpler to manage
- ✅ Lower cost (only pay for usage)

**Path structure (already implemented):**
```
mailbox-archives/
├── {tenant-id-1}/{mailbox-id}/file.mbox
├── {tenant-id-2}/{mailbox-id}/file.mbox
└── {tenant-id-n}/{mailbox-id}/file.mbox
```

Multi-tenant isolation via path prefixes + SAS tokens scoped to tenant paths.

---

## 🚀 Quick Setup (2 Minutes in Azure Portal)

### Step 1: Create Storage Account

1. **Go to:** https://portal.azure.com
2. **Click:** "Create a resource" → "Storage account"
3. **Fill in:**
   - **Subscription:** Evermail
   - **Resource group:** `evermail-dev` (select existing or create)
   - **Storage account name:** `evermaildevstorage` 
     - Must be globally unique
     - Try `evermaildev2024` or `evermail` + random numbers if taken
   - **Region:** West Europe (or your preferred region)
   - **Performance:** Standard
   - **Redundancy:** LRS (Locally-redundant - cheapest for dev)
4. **Advanced tab:**
   - ✅ Require secure transfer (HTTPS): **Enabled**
   - ✅ Allow Blob public access: **Disabled**
   - ✅ Minimum TLS version: **1.2**
5. **Click:** "Review + create" → "Create"

**Wait ~30 seconds** for deployment to complete.

---

### Step 2: Get Connection String

1. **Go to your new storage account**
2. **Click:** "Access keys" in left menu
3. **Click:** "Show" next to key1
4. **Copy:** The **"Connection string"** value

It looks like:
```
DefaultEndpointsProtocol=https;AccountName=evermaildevstorage;AccountKey=ABC123...;EndpointSuffix=core.windows.net
```

---

### Step 3: Add to Your App

```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.WebApp/Evermail.WebApp

# Store connection string in user secrets (NOT in git)
dotnet user-secrets set "ConnectionStrings:blobs" "PASTE_YOUR_CONNECTION_STRING_HERE"
```

**Also add for queue:**
```bash
dotnet user-secrets set "ConnectionStrings:queues" "PASTE_YOUR_CONNECTION_STRING_HERE"
```

(Same connection string - Azure Storage handles both blobs and queues)

---

### Step 4: Restart Aspire

```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.AppHost
# Stop current (Ctrl+C if running in foreground, or):
pkill -f "Evermail.AppHost"

# Start fresh
dotnet run
```

---

### Step 5: Test Upload!

1. Hard refresh browser (`Cmd+Shift+R`)
2. Go to `/upload`
3. Upload `test-10mb.mbox`
4. **It will go to real Azure Blob Storage** with HTTPS! ✅

---

## ✅ Benefits of Real Azure Storage

- ✅ **HTTPS by default** - no mixed content errors
- ✅ **Unlimited space** - no disk space issues
- ✅ **Same as production** - testing real environment
- ✅ **Geo-redundant** options available
- ✅ **Built-in monitoring** in Azure Portal
- ✅ **Cost:** ~€0.02/GB/month for LRS

---

## 💰 Cost Estimate for Development

**Storage:** €0.0184/GB/month
- 10 GB test data: €0.18/month
- 100 GB test data: €1.84/month
- Practically free for dev! 

**Transactions:** €0.004 per 10,000 operations
- Upload 10GB file: €0.01
- Negligible for testing

**Total dev cost:** < €5/month even with heavy testing

---

## 🔒 Security

**Already configured for security:**
- ✅ HTTPS only
- ✅ No public blob access
- ✅ TLS 1.2 minimum
- ✅ SAS tokens with expiration (2 hours)
- ✅ Path-based isolation per tenant

---

## 📝 What to Do Now

**Fastest way:**
1. Open Azure Portal: https://portal.azure.com
2. Create storage account (2 minutes)
3. Copy connection string
4. Run: `dotnet user-secrets set "ConnectionStrings:blobs" "YOUR_CONNECTION_STRING"`
5. Restart Aspire
6. Upload works! 🎉

---

**Tell me when you've created the storage account and I'll help with the connection string setup!**

