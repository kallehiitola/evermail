# Evermail - Deployment Guide

## Overview

Evermail is deployed using **Azure Aspire** to Azure Container Apps. This guide covers local development, CI/CD setup, and production deployment.

## Prerequisites

### Local Development
- .NET 8 SDK or later
- Docker Desktop (for local Aspire orchestration)
- Azure CLI (`az`) version 2.50+
- Visual Studio 2022 17.9+ or VS Code with C# Dev Kit

### Azure Resources
- Azure subscription
- Resource group created
- Azure CLI authenticated: `az login`

## Local Development Setup

### 1. Install Azure Aspire Workload
```bash
dotnet workload update
dotnet workload install aspire
```

### 2. Clone Repository
```bash
git clone https://github.com/yourusername/evermail.git
cd evermail
```

### 3. Set Up User Secrets
```bash
cd Evermail.AppHost

# Azure Storage (for local development, use Azurite)
dotnet user-secrets set "ConnectionStrings:storage" "UseDevelopmentStorage=true"

# SQL Server (local container)
dotnet user-secrets set "ConnectionStrings:evermaildb" "Server=localhost,1433;Database=Evermail;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true"

# Stripe (test keys)
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."

# Optional: Azure OpenAI (for AI features)
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "..."
```

### 4. Start Aspire AppHost
```bash
cd Evermail.AppHost
dotnet run
```

This will:
- Start SQL Server container
- Start Azurite (local blob/queue storage)
- **Run database migrations automatically** (via MigrationService)
- Start WebApp API (after migrations complete)
- Start Admin Dashboard (after migrations complete)
- Start Ingestion Worker (after migrations complete)
- Open Aspire Dashboard at `http://localhost:15000`

**Note**: Migrations run automatically via `Evermail.MigrationService` before other services start. You no longer need to manually run `dotnet ef database update`.

### 5. Access Applications
- **User Web App**: `https://localhost:7136` or `http://localhost:5264`
- **Admin Dashboard**: `http://localhost:5001`
- **API**: `https://localhost:7136/api/v1` or `http://localhost:5264/api/v1`
- **Aspire Dashboard**: `http://localhost:15000`

### 7. Test Key Vault Access (Local Development)

**Prerequisites**: Login to Azure CLI to access Key Vault
```bash
az login
```

**Test Key Vault endpoint** (app must be running in Development mode):
```bash
# Test HTTPS endpoint (use -k to ignore self-signed cert)
curl -k https://localhost:7136/api/v1/dev/test-keyvault

# Pretty print JSON response
curl -k -s https://localhost:7136/api/v1/dev/test-keyvault | jq '.'

# Test HTTP endpoint (follow redirects)
curl -L http://localhost:5264/api/v1/dev/test-keyvault | jq '.'
```

**Test Key Vault access directly via Azure CLI** (verify secrets are accessible):
```bash
# Test dev Key Vault (should return connection strings)
az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs" --query value -o tsv
az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--queues" --query value -o tsv
az keyvault secret show --vault-name evermail-dev-kv --name "sql-password" --query value -o tsv

# Test prod Key Vault (will show placeholder until production resources created)
az keyvault secret show --vault-name evermail-prod-kv --name "ConnectionStrings--blobs" --query value -o tsv
az keyvault secret show --vault-name evermail-prod-kv --name "ConnectionStrings--queues" --query value -o tsv
az keyvault secret show --vault-name evermail-prod-kv --name "sql-password" --query value -o tsv
```

**Verification**: The endpoint confirms Key Vault is being used when:
- `source` shows "✅ Key Vault (connection string matches Key Vault pattern)"
- Connection strings contain storage account names from Key Vault (e.g., "evermaildevstorage")
- All secrets are found with correct lengths

**Note**: The `/api/v1/dev/test-keyvault` endpoint returns 404 if:
- App is not running (start with `cd Evermail.AppHost && dotnet run`)
- Environment is not `Development` (check `ASPNETCORE_ENVIRONMENT=Development`)
- Check application startup logs for "✅ Azure Key Vault secrets loaded" message

**Expected response** (if Key Vault is accessible):
```json
{
  "success": true,
  "data": {
    "message": "Key Vault access test results",
    "results": {
      "blobs-connection": "✅ Found (length: 123)",
      "queues-connection": "✅ Found (length: 123)",
      "sql-password": "✅ Found (length: 25)",
      "source": "✅ Key Vault (dev)"
    }
  }
}
```

**If not logged into Azure CLI**, you'll see:
```json
{
  "results": {
    "source": "ℹ️  User secrets (local fallback)"
  }
}
```

**Verify in application logs**:
- ✅ `Azure Key Vault secrets loaded (using DefaultAzureCredential)` = Using Key Vault
- ⚠️ `Key Vault not accessible: ...` = Falling back to user secrets

## Azure Deployment

### Option 1: Azure Developer CLI (azd) - Recommended

#### 1. Initialize azd
```bash
# From project root
azd init

# Follow prompts:
# - Environment name: evermail-prod
# - Azure region: westeurope
```

#### 2. Provision Infrastructure
```bash
azd provision
```

This creates:
- Resource group
- Azure SQL Database (Serverless)
- Azure Storage Account (blobs + queues)
- Azure Container Apps Environment
- Container Apps for each service
- Application Insights
- Azure Key Vault

#### Full-Text Search Requirement (SQL)

- The SQL Server/Database **must include Full-Text Search** (FTS). For local dev we use the `mcr.microsoft.com/mssql/server:2022-latest` image with `mssql-server-fts`; for Azure SQL use service tiers that support FTS (S3+ or Hyperscale).
- The migration `20251120_EnsureEmailFullTextCatalog` will:
  - Verify `FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1` (and fail if not).
  - Enable FTS at the database level if needed (`sp_fulltext_database 'enable'`).
  - Create the `EmailSearchCatalog` (default) and rebuild the `EmailMessages` full-text index.
- The migration `20251120_AddEmailSearchVector` introduces the persisted `SearchVector` column (subject + sender + recipients + text/html bodies) and recreates the catalog so boolean queries like `bob AND order` match even when tokens live in different original columns.
- **Validation commands** (run in the SQL container or Azure SQL):
  ```sql
  SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled');           -- expect 1
  SELECT is_fulltext_enabled FROM sys.databases WHERE name = DB_NAME();
  SELECT name FROM sys.fulltext_catalogs;                          -- expect EmailSearchCatalog
  SELECT o.name FROM sys.fulltext_indexes fi JOIN sys.objects o ON fi.object_id = o.object_id;
  ```
- If any command returns 0/empty, install the FTS feature or move to an SKU that supports it before running migrations.

#### 3. Set Secrets in Key Vault

**Key Vaults Created:**
- **Dev**: `evermail-dev-kv` (resource group: `evermail-dev`)
- **Prod**: `evermail-prod-kv` (resource group: `evermail-prod`)

**Secrets stored in Key Vault:**
- `ConnectionStrings--blobs` - Azure Storage Blob connection string
- `ConnectionStrings--queues` - Azure Storage Queue connection string
- `sql-password` - SQL Server password (dev only; prod uses managed identity)

**Set secrets manually:**
```bash
# Dev Key Vault
az keyvault secret set --vault-name evermail-dev-kv --name "ConnectionStrings--blobs" --value "..."
az keyvault secret set --vault-name evermail-dev-kv --name "ConnectionStrings--queues" --value "..."
az keyvault secret set --vault-name evermail-dev-kv --name "sql-password" --value "..."

# Production Key Vault (update when production resources are created)
az keyvault secret set --vault-name evermail-prod-kv --name "ConnectionStrings--blobs" --value "..."
az keyvault secret set --vault-name evermail-prod-kv --name "ConnectionStrings--queues" --value "..."
```

**Grant access to Container Apps (managed identity):**
```bash
# Get Container App managed identity principal ID
APP_IDENTITY_ID=$(az containerapp show --name evermail-webapp --resource-group evermail-prod-rg --query identity.principalId -o tsv)

# Grant Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/<sub-id>/resourceGroups/evermail-prod/providers/Microsoft.KeyVault/vaults/evermail-prod-kv \
  --assignee $APP_IDENTITY_ID
```

**Note:** The application automatically loads secrets from Key Vault when deployed to Azure (using `DefaultAzureCredential` with managed identity). Local development continues to use user secrets.

#### 4. Create Azure Storage Queues

Two queues are required: one for ingestion, another for deletion jobs.

```bash
STORAGE_ACCOUNT=evermailstorage

az storage queue create --account-name $STORAGE_ACCOUNT --name mailbox-ingestion
az storage queue create --account-name $STORAGE_ACCOUNT --name mailbox-deletion
```

> The same storage account/connection string is reused for both queues. The worker listens to both queues concurrently.

#### 5. Deploy Applications
```bash
azd deploy
```

#### 6. Database Migrations

**Migrations run automatically** via `Evermail.MigrationService` when deployed via Aspire. The MigrationService runs before other services start, ensuring the database schema is up-to-date.

**Verify the migration step**:
- AppHost logs will show `Waiting for resource 'migrations'` followed by `Finished waiting for resource 'migrations'`.
- `Evermail.MigrationService` exits immediately after `dotnet ef database update` succeeds; any failure keeps the other services from starting so you can fix the schema before traffic hits.
- To rerun locally without the full AppHost, execute `dotnet run --project Evermail.MigrationService`.

**If you need to apply migrations manually** (e.g., for troubleshooting or when not using Aspire deployment):
```bash
# Connect to Azure SQL and apply migrations
# Option A: From local machine with Azure SQL firewall rule
az sql server firewall-rule create \
  --resource-group evermail-prod-rg \
  --server evermail-sql-server \
  --name AllowLocalClient \
  --start-ip-address <your-ip> \
  --end-ip-address <your-ip>

# Get connection string from Key Vault
CONN_STRING=$(az keyvault secret show --vault-name $KV_NAME --name "ConnectionStrings--evermaildb" --query value -o tsv)

# Apply migrations
dotnet ef database update --project Evermail.Infrastructure --startup-project Evermail.WebApp/Evermail.WebApp --connection "$CONN_STRING"

# Option B: Use Azure SQL migration bundle (recommended for prod)
dotnet ef migrations bundle --project Evermail.Infrastructure --startup-project Evermail.WebApp/Evermail.WebApp -o migrate

# Run bundle in Azure Container Instance or from local
./migrate --connection "$CONN_STRING"
```

#### 6. Verify Deployment
```bash
# Get app URLs
azd env get-values

# Test API health endpoint
curl https://evermail-webapp-<hash>.azurecontainerapps.io/health
```

### Confidential worker provisioning (Azure Confidential Container Apps)

Phase 1 locks in Secure Key Release and BYOK metadata; this step brings the ingestion worker into an attested compute plane so Key Vault can enforce SKR policies. We deploy the existing `Evermail.IngestionWorker` image into a dedicated Azure Container Apps environment configured with the *Confidential* workload profile.

> **Current Azure inventory (queried via `az resource list` on 2025‑11‑24)**
> - Production resource group: `evermail-prod` (West Europe)
> - Development resource group: `evermail-dev`
> - Key Vaults: `evermail-prod-kv`, `evermail-dev-kv`
> - Storage: `evermaildevstorage`

#### Prerequisites

- Register the Container Apps resource provider once per subscription (already done for `8e14c1ce-c216-4ac4-b274-2df2da25aa6f`, but safe to repeat):

  ```powershell
  az provider register -n Microsoft.App --wait
  ```

- Azure Container Registry (ACR) seeded via `azd provision` (typically `evermailacr`).
- If your subscription does **not** yet contain an ACR, create one in the prod resource group:

  ```powershell
  az acr create `
    --name evermailacr `
    --resource-group evermail-prod `
    --location westeurope `
    --sku Premium
  ```

- Virtual network + subnet prepared for confidential workloads. Microsoft requires a delegated subnet for Container Apps (`Microsoft.App/environments`). Record the subnet resource ID, e.g. `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/evermail-secure-vnet/subnets/container-apps-conf`.
  - If missing, create a dedicated VNet + subnet (adjust address space to match your landing zone):

    ```powershell
    az network vnet create `
      --name evermail-secure-vnet `
      --resource-group evermail-prod `
      --location westeurope `
      --address-prefixes 10.20.0.0/22 `
      --subnet-name container-apps-conf `
      --subnet-prefix 10.20.0.0/24

    az network vnet subnet update `
      --name container-apps-conf `
      --resource-group evermail-prod `
      --vnet-name evermail-secure-vnet `
      --delegations Microsoft.App/environments
    ```

- Key Vault already contains the connection strings (`ConnectionStrings--evermaildb`, `ConnectionStrings--blobs`, `ConnectionStrings--queues`) produced by `azd provision`.
- Azure CLI 2.51+ and Docker installed locally.

#### Build + provision via script

We ship `scripts/provision-confidential-worker.ps1` to automate image publishing, identity creation, RBAC assignment, and Container App deployment:

```powershell
pwsh ./scripts/provision-confidential-worker.ps1 `
  -ResourceGroup evermail-prod `
  -Location westeurope `
  -AcrName evermailacr `
  -SubnetResourceId "/subscriptions/<sub>/resourceGroups/evermail-net/providers/Microsoft.Network/virtualNetworks/evermail-secure-vnet/subnets/confidential-apps" `
  -EnvironmentName evermail-conf-env `
  -ContainerAppName evermail-conf-worker `
  -KeyVaultName evermail-prod-kv `
  -KeyVaultResourceGroup evermail-prod
```

What the script does:

1. Builds `Evermail.IngestionWorker/Dockerfile` and pushes `evermail-ingestion-worker:confidential` to the specified ACR.
2. Creates (or updates) a user-assigned managed identity and grants it **Key Vault Secrets User** + **Key Vault Crypto Service Release** over `evermail-prod-kv`.
3. Creates a Container Apps environment with the *Confidential* workload profile bound to the provided subnet.
4. Creates a confidential Container App named `evermail-conf-worker`, disables ingress, attaches the managed identity, injects the SQL/blob/queue connection strings as secrets, and sets the Key Vault parameters needed by `AddAzureKeyVaultSecrets`.

After the script completes:

- Add the new managed identity to your Secure Key Release policy (the `Configure Secure Key Release` UI already exposes the hash for auditors).
- Update observability: tag the container app logs in Application Insights or Log Analytics so “DekUnwrapped” events can be correlated with revisions.
- (Optional) Scale out by adjusting `--min-replicas`/`--max-replicas` or KEDA rules once we switch queue processing to event-driven mode.

#### Quick execution checklist

1. `az login` and `az account set -s 8e14c1ce-c216-4ac4-b274-2df2da25aa6f` (the “Evermail” subscription, if needed).
2. Ensure Microsoft.App is registered (command above).
3. `az acr login --name evermailacr` so Docker can push the worker image.
4. Run the script with the concrete values:  
   `pwsh ./scripts/provision-confidential-worker.ps1 -ResourceGroup evermail-prod -Location westeurope -AcrName evermailacr -SubnetResourceId "/subscriptions/8e14c1ce-c216-4ac4-b274-2df2da25aa6f/resourceGroups/evermail-prod/providers/Microsoft.Network/virtualNetworks/evermail-secure-vnet/subnets/container-apps-conf" -EnvironmentName evermail-conf-env -ContainerAppName evermail-conf-worker -KeyVaultName evermail-prod-kv -KeyVaultResourceGroup evermail-prod`
5. Verify the container app exists: `az containerapp show --name evermail-conf-worker --resource-group evermail-prod`.
6. Update the tenant SKR policy to include the new managed identity if not already covered.
7. Tail logs to ensure the worker starts cleanly:  
   `az containerapp logs show --name evermail-conf-worker --resource-group evermail-prod --follow`.
8. Run a test mailbox ingestion to confirm the confidential worker drains queue messages successfully.

> **When the confidential workload profile becomes available**  
> Update `scripts/provision-confidential-worker.ps1` with `-WorkloadProfileName confidential-profile -WorkloadProfileType Confidential` (or the official SKU name) before step 4, uncomment the workload-profile block inside the script, and re-run the provisioning flow. Afterwards swap the SKR policy from the placeholder `allowEvermailOps` claim to the Microsoft Azure Attestation claims for the new workload.

### Azure SQL Database (serverless, production)

To eliminate the local SQL dependency, we provision a cost-conscious serverless database in the same `evermail-prod` resource group. This keeps idle costs ~€30/month and can jump to more vCores later.

1. **Register the provider (one-time)**

   ```powershell
   az provider register -n Microsoft.Sql --wait
   ```

2. **Create the logical server**

   ```powershell
   az sql server create `
     --name evermail-sql-weu `
     --resource-group evermail-prod `
     --location westeurope `
     --admin-user evermailadmin `
     --admin-password "<generated-strong-password>"

   az sql server firewall-rule create `
     --resource-group evermail-prod `
     --server evermail-sql-weu `
     --name AllowAzure `
     --start-ip-address 0.0.0.0 `
     --end-ip-address 0.0.0.0
   ```

3. **Provision a serverless database (GP, Gen5, 1 vCore)**

   ```powershell
   az sql db create `
     --resource-group evermail-prod `
     --server evermail-sql-weu `
     --name evermail `
     --edition GeneralPurpose `
     --family Gen5 `
     --capacity 1 `
     --compute-model Serverless `
     --auto-pause-delay 120 `
     --backup-storage-redundancy Local
   ```

4. **Store the connection string in Key Vault**

   ```powershell
   az keyvault secret set `
     --vault-name evermail-prod-kv `
     --name sql-password `
     --value "<same-admin-password>"

   az keyvault secret set `
     --vault-name evermail-prod-kv `
     --name ConnectionStrings--evermaildb `
     --value "Server=tcp:evermail-sql-weu.database.windows.net,1433;Initial Catalog=evermail;Persist Security Info=False;User ID=evermailadmin;Password=<same-admin-password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
   ```

Any Aspire-hosted service can now read `ConnectionStrings--evermaildb` and connect securely via the managed identity that already has Key Vault access.

### Production storage account (blobs + queues)

We no longer rely on Azurite or the dev storage account for production uploads. Create the dedicated account once and store its connection strings in Key Vault so WebApp, AdminApp, and the worker can read them via Aspire.

1. **Create the StorageV2 account**

   ```powershell
   az storage account create `
     --name evermailprodstg `
     --resource-group evermail-prod `
     --location westeurope `
     --sku Standard_LRS `
     --kind StorageV2 `
     --https-only true
   ```

2. **Create required containers/queues**

   ```powershell
   az storage container create --name mailbox-archives --account-name evermailprodstg
   az storage container create --name gdpr-exports --account-name evermailprodstg
   az storage queue create --name mailbox-ingestion --account-name evermailprodstg
   az storage queue create --name mailbox-deletion --account-name evermailprodstg
   ```

3. **Store connection strings in Key Vault**

   ```powershell
   $conn = az storage account show-connection-string `
     --name evermailprodstg `
     --resource-group evermail-prod `
     --query connectionString -o tsv

    az keyvault secret set --vault-name evermail-prod-kv --name ConnectionStrings--blobs --value $conn
    az keyvault secret set --vault-name evermail-prod-kv --name ConnectionStrings--queues --value $conn
   ```

4. **Redeploy the worker** (`scripts/provision-confidential-worker.ps1`) so the Container App picks up the refreshed secrets. WebApp/AdminApp automatically consume the same secrets when published through Aspire.

### Option 2: Manual Bicep/ARM Deployment

#### 1. Create Resource Group
```bash
az group create --name evermail-prod-rg --location westeurope
```

#### 2. Deploy Bicep Templates
```bash
cd infra

# Deploy infrastructure
az deployment group create \
  --resource-group evermail-prod-rg \
  --template-file main.bicep \
  --parameters @main.parameters.json
```

#### 3. Build and Push Container Images
```bash
# Login to Azure Container Registry
ACR_NAME="evermailacr"
az acr login --name $ACR_NAME

# Build and push WebApp
cd Evermail.WebApp
docker build -t $ACR_NAME.azurecr.io/evermail-webapp:latest .
docker push $ACR_NAME.azurecr.io/evermail-webapp:latest

# Build and push Worker
cd ../Evermail.IngestionWorker
docker build -t $ACR_NAME.azurecr.io/evermail-worker:latest .
docker push $ACR_NAME.azurecr.io/evermail-worker:latest

# Build and push AdminApp
cd ../Evermail.AdminApp
docker build -t $ACR_NAME.azurecr.io/evermail-admin:latest .
docker push $ACR_NAME.azurecr.io/evermail-admin:latest
```

#### 4. Update Container Apps
```bash
# Update WebApp
az containerapp update \
  --name evermail-webapp \
  --resource-group evermail-prod-rg \
  --image $ACR_NAME.azurecr.io/evermail-webapp:latest

# Update Worker
az containerapp update \
  --name evermail-worker \
  --resource-group evermail-prod-rg \
  --image $ACR_NAME.azurecr.io/evermail-worker:latest

# Update Admin
az containerapp update \
  --name evermail-admin \
  --resource-group evermail-prod-rg \
  --image $ACR_NAME.azurecr.io/evermail-admin:latest
```

## CI/CD with GitHub Actions

### 1. Create GitHub Secrets
In your GitHub repository, go to Settings → Secrets and add:

- `AZURE_CREDENTIALS`: Service principal JSON
- `AZURE_SUBSCRIPTION_ID`: Your Azure subscription ID
- `AZURE_TENANT_ID`: Your Azure tenant ID
- `ACR_USERNAME`: Container registry username
- `ACR_PASSWORD`: Container registry password
- `STRIPE_SECRET_KEY`: Stripe secret key (for integration tests)

### 2. Service Principal Setup
```bash
# Create service principal for GitHub Actions
az ad sp create-for-rbac \
  --name "evermail-github-actions" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/evermail-prod-rg \
  --sdk-auth

# Copy output JSON to AZURE_CREDENTIALS secret
```

### 3. GitHub Actions Workflow
Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '8.0.x'
  ACR_NAME: 'evermailacr'
  RESOURCE_GROUP: 'evermail-prod-rg'

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Run tests
        run: dotnet test --no-build --configuration Release --verbosity normal
        env:
          STRIPE_SECRET_KEY: ${{ secrets.STRIPE_SECRET_KEY }}

  deploy:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - name: Login to ACR
        run: az acr login --name ${{ env.ACR_NAME }}
      
      - name: Build and push WebApp
        run: |
          docker build -t ${{ env.ACR_NAME }}.azurecr.io/evermail-webapp:${{ github.sha }} -t ${{ env.ACR_NAME }}.azurecr.io/evermail-webapp:latest -f Evermail.WebApp/Dockerfile .
          docker push ${{ env.ACR_NAME }}.azurecr.io/evermail-webapp:${{ github.sha }}
          docker push ${{ env.ACR_NAME }}.azurecr.io/evermail-webapp:latest
      
      - name: Build and push Worker
        run: |
          docker build -t ${{ env.ACR_NAME }}.azurecr.io/evermail-worker:${{ github.sha }} -t ${{ env.ACR_NAME }}.azurecr.io/evermail-worker:latest -f Evermail.IngestionWorker/Dockerfile .
          docker push ${{ env.ACR_NAME }}.azurecr.io/evermail-worker:${{ github.sha }}
          docker push ${{ env.ACR_NAME }}.azurecr.io/evermail-worker:latest
      
      - name: Deploy to Container Apps
        run: |
          az containerapp update --name evermail-webapp --resource-group ${{ env.RESOURCE_GROUP }} --image ${{ env.ACR_NAME }}.azurecr.io/evermail-webapp:${{ github.sha }}
          az containerapp update --name evermail-worker --resource-group ${{ env.RESOURCE_GROUP }} --image ${{ env.ACR_NAME }}.azurecr.io/evermail-worker:${{ github.sha }}
      
      - name: Database Migrations
        run: |
          # Note: Migrations run automatically via MigrationService when deployed via Aspire
          # This step is only needed if deploying without Aspire or for manual migration verification
          # Get connection string from Key Vault
          CONN_STRING=$(az keyvault secret show --vault-name evermail-kv --name "ConnectionStrings--evermaildb" --query value -o tsv)
          
          # Create migration bundle
          dotnet ef migrations bundle --project Evermail.Infrastructure --startup-project Evermail.WebApp/Evermail.WebApp -o migrate
          
          # Apply migrations (if not using Aspire automatic migrations)
          ./migrate --connection "$CONN_STRING"
```

## Monitoring & Health Checks

### Application Insights
Aspire automatically configures Application Insights for all services.

**View Telemetry**:
```bash
az monitor app-insights component show \
  --resource-group evermail-prod-rg \
  --app evermail-appinsights \
  --query "instrumentationKey"
```

**Key Metrics to Monitor**:
- Request latency (p50, p95, p99)
- Exception rate
- Queue depth (mailbox-ingestion + mailbox-deletion)
- Database DTU percentage
- Blob storage operations

### Health Endpoints
Each service exposes health endpoints:

```csharp
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

**Test Health**:
```bash
curl https://evermail-webapp.azurecontainerapps.io/health
curl https://evermail-worker.azurecontainerapps.io/health
```

### Alerts
Set up Azure Monitor alerts for:
- High error rate (>5% in 5 minutes)
- Queue depth >1000 for >10 minutes (evaluate both mailbox-ingestion and mailbox-deletion)
- Database DTU >80% for >15 minutes
- Container app crashed (restart count >3)

```bash
az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group evermail-prod-rg \
  --scopes /subscriptions/<sub-id>/resourceGroups/evermail-prod-rg/providers/Microsoft.App/containerApps/evermail-webapp \
  --condition "count requests failed > 50 in 5m" \
  --description "Alert when error rate exceeds 5%"
```

## Scaling Configuration

### WebApp Auto-Scaling
```bash
az containerapp update \
  --name evermail-webapp \
  --resource-group evermail-prod-rg \
  --min-replicas 1 \
  --max-replicas 10 \
  --scale-rule-name http-requests \
  --scale-rule-type http \
  --scale-rule-http-concurrency 100
```

### Worker Queue-Based Scaling
```bash
az containerapp update \
  --name evermail-worker \
  --resource-group evermail-prod-rg \
  --min-replicas 0 \
  --max-replicas 5 \
  --scale-rule-name ingestion-queue \
  --scale-rule-type azure-queue \
  --scale-rule-metadata queueName=mailbox-ingestion accountName=evermailstorage queueLength=10 \
  --scale-rule-auth secretRef=storage-connection-string

az containerapp update \
  --name evermail-worker \
  --resource-group evermail-prod-rg \
  --scale-rule-name deletion-queue \
  --scale-rule-type azure-queue \
  --scale-rule-metadata queueName=mailbox-deletion accountName=evermailstorage queueLength=10 \
  --scale-rule-auth secretRef=storage-connection-string
```

## Backup & Disaster Recovery

### Database Backups
Azure SQL Serverless provides automatic backups:
- **Point-in-time restore**: Last 7 days
- **Long-term retention**: Configure for 1-10 years

```bash
# Create manual backup
az sql db copy \
  --resource-group evermail-prod-rg \
  --server evermail-sql-server \
  --name Evermail \
  --dest-name Evermail-backup-$(date +%Y%m%d)
```

### Blob Storage Backups
Enable soft delete and versioning:
```bash
az storage account blob-service-properties update \
  --account-name evermailstorage \
  --enable-delete-retention true \
  --delete-retention-days 30 \
  --enable-versioning true
```

### Disaster Recovery Plan
1. **Database**: Restore from point-in-time backup
2. **Blobs**: Failover to geo-redundant secondary region
3. **Secrets**: Restore from Key Vault soft delete
4. **Configuration**: Redeploy via IaC (Bicep templates)

**RTO**: 4 hours  
**RPO**: 1 hour (Azure SQL automatic backups)

## Cost Optimization

### Azure SQL Serverless
- Auto-pauses after 1 hour of inactivity
- Auto-resumes on first connection
- Pay only for compute used

**Estimate**: €15-30/month for small workload

### Container Apps
- Scale to zero when idle (Worker)
- Consumption-based pricing
- Use minimum replicas = 0 for non-critical services

**Estimate**: €40-80/month

### Blob Storage Lifecycle
```bash
# Move old mbox files to cool tier after 90 days
az storage account management-policy create \
  --account-name evermailstorage \
  --policy @lifecycle-policy.json
```

**lifecycle-policy.json**:
```json
{
  "rules": [
    {
      "name": "moveToCool",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["mbox-archives/"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 90
            }
          }
        }
      }
    }
  ]
}
```

## Troubleshooting

### View Container Logs
```bash
az containerapp logs show \
  --name evermail-webapp \
  --resource-group evermail-prod-rg \
  --follow
```

### Connect to Container Shell
```bash
az containerapp exec \
  --name evermail-webapp \
  --resource-group evermail-prod-rg \
  --command /bin/bash
```

### Check Queue Messages
```bash
az storage message peek \
  --queue-name mailbox-ingestion \
  --account-name evermailstorage \
  --num-messages 10
```

### Database Connection Issues
```bash
# Test connection from local machine
sqlcmd -S evermail-sql-server.database.windows.net -d Evermail -U sqladmin -P <password> -Q "SELECT @@VERSION"

# Check firewall rules
az sql server firewall-rule list \
  --resource-group evermail-prod-rg \
  --server evermail-sql-server
```

---

**Last Updated**: 2025-11-11  
**Next Review**: Before major infrastructure changes

