# Azure Resource Management for Evermail

## ✅ Current Azure Access Status

### Verified Azure Tools

1. **Azure CLI** ✅
   - **Status**: Installed, authenticated, and **UPGRADED**
   - **Version**: **2.79.0** (latest)
   - **Subscription**: "Triviai" (ea32cc6b-3647-4f47-b218-aabb0aeef3b6)
   - **Tenant**: 96ebf722-1be3-4aba-96e3-b423f43dcd7d
   - **Updated**: 2025-11-11

2. **Azure Developer CLI (azd)** ✅
   - **Status**: Installed and **UPGRADED**
   - **Version**: **1.21.1** (latest)
   - **Updated**: 2025-11-11

3. **Terminal Access** ✅
   - **Status**: AI agent can run Azure CLI commands
   - **Permissions**: Can create/manage Azure resources via `run_terminal_cmd`
   - **No additional MCP needed**: Standard terminal access is sufficient

---

## 🎯 How AI Agent Will Create Azure Resources

### Method 1: Azure Developer CLI (azd) - RECOMMENDED ⭐

**Best for**: Aspire deployments (automatic)

```bash
# Initialize Aspire project for Azure
azd init

# Provision all Azure resources
azd provision

# Deploy application
azd deploy

# Or do both at once
azd up
```

**What `azd` creates automatically**:
- ✅ Resource Group
- ✅ Azure SQL Database
- ✅ Azure Storage Account (blobs + queues)
- ✅ Azure Container Apps Environment
- ✅ Container Apps (webapp, worker, admin)
- ✅ Application Insights
- ✅ Azure Key Vault
- ✅ All networking and identities

**Benefits**:
- ✅ One command deployment
- ✅ Infrastructure-as-Code (Bicep)
- ✅ Aspire integration
- ✅ Automatic service discovery
- ✅ Managed identities configured

### Method 2: Azure CLI (az) - MANUAL

**Best for**: Individual resource creation, debugging

```bash
# Create resource group
az group create --name evermail-prod-rg --location westeurope

# Create SQL Server
az sql server create \
  --name evermail-sql-server \
  --resource-group evermail-prod-rg \
  --location westeurope \
  --admin-user sqladmin \
  --admin-password "SecurePassword123!"

# Create SQL Database (Serverless)
az sql db create \
  --name Evermail \
  --server evermail-sql-server \
  --resource-group evermail-prod-rg \
  --edition GeneralPurpose \
  --compute-model Serverless \
  --family Gen5 \
  --capacity 2

# Create Storage Account
az storage account create \
  --name evermailstorage \
  --resource-group evermail-prod-rg \
  --location westeurope \
  --sku Standard_LRS

# Create blob containers
az storage container create --name mbox-archives --account-name evermailstorage
az storage container create --name attachments --account-name evermailstorage

# Create queue
az storage queue create --name mailbox-ingestion --account-name evermailstorage

# Create Container Apps Environment
az containerapp env create \
  --name evermail-env \
  --resource-group evermail-prod-rg \
  --location westeurope
```

### Method 3: ARM/Bicep Templates - INFRASTRUCTURE AS CODE

**Best for**: Repeatable deployments, version control

Aspire generates Bicep templates automatically when you use `azd`.

---

## 🤖 AI Agent Azure Capabilities

### What I Can Do ✅

1. **Create Azure Resources**
   ```
   "Create an Azure resource group for Evermail in West Europe"
   ```

2. **Deploy with azd**
   ```
   "Initialize azd for the Evermail project and provision Azure resources"
   ```

3. **Query Azure Resources**
   ```
   "Show me all resource groups in my subscription"
   "Get the connection string for evermail SQL database"
   ```

4. **Manage Resources**
   ```
   "Scale the Container App to 2 instances"
   "Update the SQL database to 4 vCores"
   ```

5. **Check Pricing (with Azure Pricing MCP)**
   ```
   "What's the cost of Azure SQL Serverless for a 10GB database in West Europe?"
   "Compare Container Apps pricing across regions"
   ```

6. **Get Official Docs (with Microsoft Learn MCP)**
   ```
   "Show me the Azure CLI commands to create Container Apps. search Microsoft Learn"
   "Fetch full documentation on Azure Aspire deployment. fetch full doc"
   ```

### What I Cannot Do ❌

- ❌ Access Azure Portal GUI directly
- ❌ See visual dashboards
- ❌ Take screenshots of Azure Portal
- ❌ Interactively browse portal

**Workaround**: Use Azure CLI commands for everything (which I can do)

---

## 📋 Recommended Azure Tools Upgrade

### Update Azure Developer CLI (Important!)

Your `azd` is outdated (1.6.1, latest is 1.21.0):

```bash
# macOS
brew upgrade azd

# Or download installer
curl -fsSL https://aka.ms/install-azd.sh | bash

# Verify
azd version
```

**Why upgrade?**:
- ✅ Bug fixes
- ✅ Better Aspire integration
- ✅ New features for .NET 9
- ✅ Performance improvements

### Optional: Azure CLI Update

Your `az` is slightly old (2.56.0):

```bash
az upgrade
```

---

## 🔍 Do We Need an Azure MCP?

### Answer: **NO - We're Already Good** ✅

**Reasons**:

1. **I have terminal access** ✅
   - Can run `az` commands
   - Can run `azd` commands
   - Can create/manage all Azure resources

2. **We have Microsoft Learn MCP** ✅
   - Provides official Azure documentation
   - Shows correct CLI commands
   - Gives examples and best practices

3. **Azure Aspire handles deployment** ✅
   - `azd` automatically provisions resources
   - Infrastructure-as-Code (Bicep)
   - One-command deployment

4. **Azure Pricing MCP (optional)** ⚙️
   - Helps with cost decisions
   - Compares services and regions
   - Not required but useful

### What We Don't Need

- ❌ **Azure Portal MCP** - Not necessary (CLI is sufficient)
- ❌ **Azure ARM MCP** - azd handles this
- ❌ **Azure SDK MCP** - We'll use Azure SDKs in code
- ❌ **Terraform MCP** - We're using Aspire + Bicep

### What Would Be Nice to Have (Optional)

**Shell/Terminal MCP**: Some MCP servers provide enhanced terminal access, but Cursor already has built-in terminal access via `run_terminal_cmd` tool (which I'm using right now).

---

## 🚀 Azure Deployment Workflow for Evermail

### Step 1: Upgrade Tools

```bash
# Upgrade azd (important!)
brew upgrade azd

# Optionally upgrade az
az upgrade
```

### Step 2: Create Aspire Solution

```bash
cd /Users/kallehiitola/Work/evermail
dotnet new aspire -n Evermail --framework net9.0
```

### Step 3: Initialize Azure Deployment

```bash
cd Evermail.AppHost

# Initialize azd
azd init

# Prompts will ask:
# - Environment name: evermail-prod
# - Azure location: westeurope
```

### Step 4: Provision Resources

```bash
# This creates ALL Azure resources
azd provision
```

**What gets created**:
- Resource Group: `rg-evermail-prod`
- Azure SQL Server + Database (Serverless)
- Storage Account (blobs + queues)
- Container Apps Environment
- Container Apps (webapp, worker, admin)
- Application Insights
- Key Vault
- All networking and managed identities

**Cost**: ~€80-120/month (depending on usage)

### Step 5: Deploy Application

```bash
# Build and deploy all services
azd deploy
```

### Step 6: Configure Secrets

```bash
# Get Key Vault name
KV_NAME=$(azd env get-values | grep AZURE_KEY_VAULT_NAME | cut -d'=' -f2)

# Set Stripe secrets
az keyvault secret set --vault-name $KV_NAME --name "Stripe--SecretKey" --value "sk_live_..."
az keyvault secret set --vault-name $KV_NAME --name "Stripe--WebhookSecret" --value "whsec_..."
```

---

## 🎯 AI Agent Azure Workflow

### How I'll Help You Deploy

**When you ask me to create Azure resources**, I will:

1. **Check Documentation/Deployment.md** (document-driven development)
2. **Use Azure CLI commands** via terminal
3. **Reference Microsoft Learn** for correct syntax
4. **Provide step-by-step commands** you can review
5. **Use `azd` for Aspire** (automatic infrastructure)

### Example Interaction

**You**: "Create the Azure resources for Evermail"

**I will**:
1. Verify azd is updated
2. Run `azd init` to configure
3. Run `azd provision` to create resources
4. Configure secrets in Key Vault
5. Update Documentation/Deployment.md with actual resource names

**You**:
- Review commands before they run
- Approve or modify as needed
- I'll execute and handle errors

---

## 🔐 Azure Authentication Status

### Current Status

- ✅ Azure CLI authenticated
- ✅ Azure subscription active
- ✅ Tenant ID: 96ebf722-1be3-4aba-96e3-b423f43dcd7d
- ✅ Subscription: "Triviai" (ea32cc6b-3647-4f47-b218-aabb0aeef3b6)

### Required for Evermail Deployment

- ✅ Owner or Contributor role on subscription
- ✅ Ability to create resource groups
- ✅ Ability to create SQL databases
- ✅ Ability to create storage accounts
- ✅ Ability to create container apps

**Verify permissions**:
```bash
az role assignment list --assignee $(az account show --query user.name -o tsv) --all
```

---

## 📊 Comparison: MCP vs Direct CLI Access

| Capability | Azure MCP | Direct CLI (Current) | Winner |
|------------|-----------|---------------------|--------|
| **Create resources** | Hypothetical | ✅ Available | ✅ **CLI** |
| **Query resources** | Hypothetical | ✅ Available | ✅ **CLI** |
| **Get documentation** | ❌ No | ✅ Microsoft Learn MCP | ✅ **MS Learn** |
| **Cost estimation** | ❌ No | ✅ Azure Pricing MCP | ✅ **Pricing MCP** |
| **Aspire deployment** | ❌ No | ✅ azd commands | ✅ **azd** |
| **Ease of use** | Unknown | ✅ Proven | ✅ **CLI** |

### Conclusion: **We don't need an Azure resource management MCP** ✅

**We have everything we need**:
1. ✅ Azure CLI (authenticated, working)
2. ✅ Azure Developer CLI (for Aspire)
3. ✅ Microsoft Learn MCP (for documentation)
4. ✅ Azure Pricing MCP (for cost decisions)
5. ✅ Terminal access (AI can run commands)

---

## 🎯 Next Steps for Azure Deployment

### When You're Ready to Deploy

1. **Upgrade tools first**:
   ```bash
   brew upgrade azd
   az upgrade
   ```

2. **Build Aspire solution**:
   ```bash
   dotnet new aspire -n Evermail --framework net9.0
   ```

3. **Deploy to Azure**:
   ```bash
   cd Evermail.AppHost
   azd up  # Provisions + deploys in one command
   ```

4. **I'll assist with**:
   - Running commands
   - Troubleshooting errors
   - Configuring secrets
   - Verifying deployment
   - Updating documentation

---

## ✅ Summary

### Azure Resource Management: READY ✅

- ✅ **Azure CLI**: Installed, authenticated
- ✅ **azd CLI**: Installed (needs upgrade to 1.21.0)
- ✅ **Subscription**: Active and accessible
- ✅ **AI Agent**: Can run Azure CLI commands
- ✅ **MCPs**: Microsoft Learn (docs) + Azure Pricing (costs)
- ✅ **No gaps**: Everything needed is available

### Do We Need Additional MCPs? **NO** ✅

**Current setup is sufficient**:
- Terminal access for Azure CLI commands
- Microsoft Learn MCP for documentation
- Azure Pricing MCP for cost decisions
- azd for automated Aspire deployment

### Action Items

**Before deploying**:
```bash
# Upgrade azd (recommended)
brew upgrade azd

# Verify
azd version  # Should show 1.21.0
```

**When ready to deploy**:
```
"Initialize azd for the Evermail Aspire project and provision Azure resources in West Europe"
```

I'll handle the deployment using Azure CLI commands with your review and approval!

---

**Status**: 🟢 **READY FOR AZURE DEPLOYMENT**  
**Tools**: ✅ All present and working  
**MCPs**: ✅ Sufficient (no additional needed)  
**Next**: Upgrade azd, then deploy when MVP is built

