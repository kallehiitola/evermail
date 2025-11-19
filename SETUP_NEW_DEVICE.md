# Setup Instructions for New Device

## Required Parameters

When running Aspire AppHost on a new device, you need to set these parameters:

**⚠️ IMPORTANT: You MUST be in the `Evermail/Evermail.AppHost` directory!**

### 1. Key Vault Parameters (for local dev)

```bash
# Navigate to AppHost directory FIRST
cd Evermail/Evermail.AppHost

# Set Key Vault name and resource group for dev environment
dotnet user-secrets set "Parameters:key-vault-name" "evermail-dev-kv"
dotnet user-secrets set "Parameters:key-vault-resource-group" "evermail-dev"
```

**Note**: These are for accessing the dev Key Vault. Make sure you're logged into Azure CLI:
```bash
az login
```

### 2. SQL Password (for local SQL container)

```bash
# Make sure you're in the AppHost directory
cd Evermail/Evermail.AppHost

# Set SQL password (use a strong password)
dotnet user-secrets set "Parameters:sql-password" "YourStrong@Passw0rd123"
```

**Note**: This password is used for the local SQL Server container. It can be any strong password you prefer.

### 2b. OAuth Credentials (Optional but Recommended)

**Google OAuth** (if you want Google login to work):

```powershell
# PowerShell (Windows)
cd Evermail\Evermail.WebApp\Evermail.WebApp

dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"
```

**Microsoft OAuth** (if you want Microsoft login to work):

```powershell
# PowerShell (Windows)
cd Evermail\Evermail.WebApp\Evermail.WebApp

dotnet user-secrets set "Authentication:Microsoft:ClientId" "YOUR_MICROSOFT_CLIENT_ID"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "YOUR_MICROSOFT_CLIENT_SECRET"
```

**Note**: 
- OAuth is optional - the app will work without it (users can still register/login with email/password)
- If OAuth credentials are missing, you'll see a warning in logs: `⚠️ Google OAuth not configured (missing credentials)`
- If you try to use Google login without credentials, you'll get an error: `No authentication handler is registered for the scheme 'Google'`
- To get Google OAuth credentials: https://console.cloud.google.com/apis/credentials
- To get Microsoft OAuth credentials: https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade

### 3. Storage Connection Strings (for local dev)

**Use Azure Storage from Key Vault (recommended)**

**For PowerShell (Windows):**
```powershell
cd Evermail\Evermail.AppHost

# Login to Azure first
az login

# Get connection strings from Key Vault
$BLOBS_CS = az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs" --query value -o tsv
$QUEUES_CS = az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--queues" --query value -o tsv

# Verify connection strings were retrieved
Write-Host "Blobs CS length: $($BLOBS_CS.Length)"
Write-Host "Queues CS length: $($QUEUES_CS.Length)"

# Set them
dotnet user-secrets set "ConnectionStrings:blobs" $BLOBS_CS
dotnet user-secrets set "ConnectionStrings:queues" $QUEUES_CS
```

**For Bash (Linux/Mac/Git Bash):**
```bash
cd Evermail/Evermail.AppHost

# Get connection strings from Key Vault (requires az login)
BLOBS_CS=$(az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs" --query value -o tsv)
QUEUES_CS=$(az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--queues" --query value -o tsv)

dotnet user-secrets set "ConnectionStrings:blobs" "$BLOBS_CS"
dotnet user-secrets set "ConnectionStrings:queues" "$QUEUES_CS"
```

**Alternative: Use Azurite (local storage emulator) - Only if you don't have Azure access**

```bash
cd Evermail/Evermail.AppHost

# Use Azurite for local blob storage
dotnet user-secrets set "ConnectionStrings:blobs" "UseDevelopmentStorage=true"

# Use Azurite for local queue storage
dotnet user-secrets set "ConnectionStrings:queues" "UseDevelopmentStorage=true"
```

## Quick Setup Script

**⚠️ Make sure you're in the correct directory!**

Run this complete setup:

```bash
# Navigate to AppHost directory (this is REQUIRED!)
cd Evermail/Evermail.AppHost

# Verify you're in the right place (should show Evermail.AppHost.csproj)
ls *.csproj

# Key Vault parameters
dotnet user-secrets set "Parameters:key-vault-name" "evermail-dev-kv"
dotnet user-secrets set "Parameters:key-vault-resource-group" "evermail-dev"

# SQL password (change to your preferred password)
dotnet user-secrets set "Parameters:sql-password" "YourStrong@Passw0rd123"

# Storage (get from Azure Key Vault)
az login
$BLOBS_CS = az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs" --query value -o tsv
$QUEUES_CS = az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--queues" --query value -o tsv
dotnet user-secrets set "ConnectionStrings:blobs" $BLOBS_CS
dotnet user-secrets set "ConnectionStrings:queues" $QUEUES_CS

# Verify
echo "✅ Parameters set. Verify with:"
dotnet user-secrets list
```

## Verify Setup

```bash
cd Evermail/Evermail.AppHost

# List all secrets
dotnet user-secrets list

# Should show:
# Parameters:key-vault-name = evermail-dev-kv
# Parameters:key-vault-resource-group = evermail-dev
# Parameters:sql-password = YourStrong@Passw0rd123
# ConnectionStrings:blobs = UseDevelopmentStorage=true
# ConnectionStrings:queues = UseDevelopmentStorage=true
```

## Start the Application

```bash
cd Evermail/Evermail.AppHost
dotnet run
```

## Troubleshooting

### If Key Vault access fails:
- Make sure you're logged in: `az login`
- Verify access: `az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs"`

### If Azurite is not running:
- Azurite should start automatically with Aspire
- If not, install: `npm install -g azurite`
- Run manually: `azurite --silent --location ~/.azurite --debug ~/.azurite/debug.log`

### If SQL container fails:
- Check Docker is running
- Verify SQL password matches what you set
- Check logs: `docker logs <container-id>`

