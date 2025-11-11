# Azure Subscription Setup for Evermail

## Current Status

**Existing Subscription**:
- Name: "Triviai"
- ID: `ea32cc6b-3647-4f47-b218-aabb0aeef3b6`
- Status: ✅ Active

## Options for Evermail

### Option 1: Use Resource Groups (RECOMMENDED) ⭐

**Best for**: Side-hustle, quick start, cost-effective

Instead of creating a new subscription, use **Resource Groups** to isolate Evermail resources within your existing subscription. This is the **Azure best practice** and what most SaaS companies do.

**Benefits**:
- ✅ **No additional subscription needed** - Save money
- ✅ **Immediate start** - No billing setup
- ✅ **Clean organization** - All Evermail resources in one group
- ✅ **Easy cost tracking** - Azure Cost Management by resource group
- ✅ **Proper isolation** - Separate from other projects
- ✅ **Standard practice** - How Azure recommends organizing projects

**Implementation**:
```bash
# Create dedicated resource group for Evermail
az group create \
  --name evermail-prod-rg \
  --location westeurope \
  --tags Project=Evermail Environment=Production Owner=kallehiitola

# All Evermail resources go here
# - SQL Database
# - Storage Account
# - Container Apps
# - Key Vault
# - Application Insights
```

**Cost Tracking**:
```bash
# View costs for Evermail only
az consumption usage list \
  --resource-group evermail-prod-rg \
  --start-date 2025-11-01 \
  --end-date 2025-11-30

# Or use Azure Portal → Cost Management → filter by resource group
```

**Azure Developer CLI (azd) will use this**:
```bash
# When you run 'azd up', it will:
# 1. Create resource group: rg-evermail-prod-westeurope
# 2. Put all resources there
# 3. Tag with environment name
```

---

### Option 2: Create New Subscription (Manual Process)

**Best for**: Enterprise, separate billing, multiple projects

Creating a new Azure subscription requires manual steps through the Azure Portal:

#### Step 1: Go to Azure Portal

1. Visit: https://portal.azure.com
2. Login with your Microsoft account
3. Navigate to **Subscriptions**

#### Step 2: Create Subscription

**If you have an Enterprise Agreement (EA)**:
1. Click **+ Add**
2. Select subscription type
3. Name it "Evermail"
4. Set up billing

**If you have Pay-As-You-Go**:
1. You may need to create via: https://azure.microsoft.com/free/
2. Requires credit card setup
3. Name the subscription "Evermail"

**If you have Microsoft Partner Network (MPN)**:
1. Use partner portal
2. Create dev/test subscription

#### Step 3: Switch to New Subscription

Once created:
```bash
# List subscriptions (should show "Evermail")
az account list --output table

# Set as default
az account set --subscription "Evermail"

# Verify
az account show --output table
```

---

### Option 3: Rename Existing Subscription (Not Recommended)

You could rename "Triviai" to "Evermail" if it's not being used:

```bash
# This only changes the display name, not the ID
az account set --subscription "Triviai"
# Then rename via Portal (Subscription → Edit name)
```

**Caveat**: If "Triviai" is actively used for other resources, **don't do this**.

---

## 🎯 RECOMMENDATION: Use Option 1 (Resource Groups)

### Why Resource Groups are Better for You

1. **Immediate Start** ⏱️
   - No subscription creation wait
   - No billing setup
   - Start building today

2. **Cost-Effective** 💰
   - No additional subscription costs
   - Share free Azure credits across projects
   - One billing account to manage

3. **Proper Organization** 📁
   - All Evermail resources isolated in `evermail-prod-rg`
   - Easy to view costs per project
   - Can delete entire project with one command

4. **Azure Best Practice** ✅
   - This is how Microsoft recommends organizing projects
   - Used by most SaaS companies
   - Documented in Azure Well-Architected Framework

5. **Aspire Integration** 🚀
   - `azd` automatically creates resource groups
   - Names them: `rg-{appname}-{environment}-{location}`
   - Example: `rg-evermail-prod-westeurope`

### Implementation Plan

**Step 1**: Use existing "Triviai" subscription with dedicated resource groups

**Step 2**: When you run `azd up` for Evermail, it will create:
```
Subscription: Triviai (existing)
└── Resource Group: rg-evermail-prod-westeurope (NEW)
    ├── SQL Server: evermail-sql-server
    ├── SQL Database: Evermail
    ├── Storage Account: evermailstorage
    ├── Container Apps Environment: evermail-env
    ├── Container Apps: evermail-webapp, evermail-worker, evermail-admin
    ├── Key Vault: evermail-kv
    └── Application Insights: evermail-appinsights
```

**Costs are tracked separately** via resource group tags and Azure Cost Management.

---

## 🔒 Resource Isolation

### How It Works

```
Azure Subscription: Triviai
│
├── Resource Group: triviai-resources (your other projects)
│   └── (other resources)
│
└── Resource Group: evermail-prod-rg (EVERMAIL ONLY)
    ├── SQL Database (Evermail data)
    ├── Storage Account (Evermail blobs)
    ├── Container Apps (Evermail services)
    └── All resources isolated from other projects
```

**Benefits**:
- ✅ Complete logical isolation
- ✅ Separate cost tracking
- ✅ Independent lifecycle
- ✅ Can delete all Evermail resources with: `az group delete --name evermail-prod-rg`

---

## 💡 Recommended Next Steps

### 1. Keep Using "Triviai" Subscription ✅

```bash
# Verify it's the default
az account show --query name -o tsv
# Output: Triviai
```

### 2. Let azd Create Resource Groups Automatically

When you run `azd up`, it will ask:
- Environment name: `evermail-prod`
- Location: `westeurope`

Then it creates: `rg-evermail-prod-westeurope`

**All Evermail resources go there**, cleanly separated from other projects.

### 3. Track Costs by Resource Group

```bash
# View Evermail costs only
az consumption usage list \
  --resource-group rg-evermail-prod-westeurope \
  --query "[].{Date:usageStart, Cost:pretaxCost, Currency:currency}" \
  --output table
```

Or use **Azure Portal → Cost Management → Filter by Resource Group**.

---

## 📊 Cost Tracking Without Separate Subscription

### Method 1: Resource Group Tags (Best)

```bash
# Tag all Evermail resources
az group create \
  --name evermail-prod-rg \
  --location westeurope \
  --tags \
    Project=Evermail \
    Environment=Production \
    CostCenter=Evermail \
    Owner=kallehiitola
```

**Azure Cost Management** then shows costs by these tags.

### Method 2: Resource Group Filtering

In Azure Portal:
1. Go to **Cost Management + Billing**
2. Select **Cost Analysis**
3. Filter by **Resource Group**: `evermail-prod-rg`
4. See all Evermail costs isolated

### Method 3: Export Costs to Billing

```bash
# Get costs for Evermail resource group
az consumption usage list \
  --resource-group evermail-prod-rg \
  --start-date 2025-11-01 \
  --end-date 2025-11-30 \
  --output json > evermail-costs.json
```

Then charge customers based on these costs.

---

## 🎯 Decision: What Should We Do?

### MY RECOMMENDATION: Use Resource Groups ⭐

**For Evermail side-hustle**:
1. ✅ Use existing "Triviai" subscription
2. ✅ Create dedicated resource groups for Evermail
3. ✅ Track costs via resource group
4. ✅ Complete logical and cost isolation

**You get**:
- ✅ Same isolation as separate subscription
- ✅ Immediate start (no billing setup)
- ✅ Easier management (one subscription)
- ✅ Lower overhead (no additional subscription fees)

**When to create separate subscription**:
- Only if you need completely separate billing entities
- Only if Evermail becomes a separate company
- Only if you need separate Azure AD tenant

For a side-hustle SaaS, **resource groups are perfect and standard**.

---

## ✅ Implementation

I'll proceed with **Option 1 (Resource Groups)** unless you specifically want a new subscription:

```bash
# Set default subscription (if needed)
az account set --subscription "Triviai"

# Verify
az account show --query name -o tsv
```

When you run `azd up` for Evermail, it will automatically:
- Create `rg-evermail-{env}-{location}`
- Put all Evermail resources there
- Isolate costs and management
- Work perfectly for your side-hustle

**This is the Azure-recommended and industry-standard approach.** ✅

---

## 🔗 References

- [Azure Resource Groups Best Practices](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/overview)
- [Cost Management by Resource Group](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/quick-acm-cost-analysis)
- [Azure Subscription Limits](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits)

---

**Last Updated**: 2025-11-11  
**Azure CLI**: 2.79.0 ✅  
**azd**: 1.21.1 ✅  
**Recommendation**: Use resource groups within existing subscription

