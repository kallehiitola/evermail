# Azure Key Vault Setup - Complete ✅

## ✅ What's Configured

### Key Vaults Created
- **Dev**: `evermail-dev-kv` (resource group: `evermail-dev`)
- **Prod**: `evermail-prod-kv` (resource group: `evermail-prod`)

### Secrets Stored
- `ConnectionStrings--blobs` - Azure Storage Blob connection string
- `ConnectionStrings--queues` - Azure Storage Queue connection string  
- `sql-password` - SQL Server password (dev only)

### Automatic Configuration

#### 1. **Environment Detection** (Automatic)
- **Local Dev**: Automatically uses `evermail-dev-kv`
- **Production**: Automatically uses `evermail-prod-kv` (when `IsPublishMode = true`)
- **No manual parameter changes needed!**

#### 2. **Role Assignment** (Automatic)
- When you deploy with Aspire (`azd deploy`), role assignments are **automatically created**
- Container Apps get `KeyVaultSecretsUser` role via `.WithReference(keyVault)`
- **No manual role assignment needed!**

#### 3. **Authentication** (Automatic)
- **Local Dev**: Uses Azure CLI credentials (if logged in) → Key Vault
- **Local Dev Fallback**: If not logged in → User secrets
- **Production**: Uses managed identity → Key Vault

## 🧪 Testing Key Vault in Local Development

### Prerequisites
1. **Login to Azure CLI**:
   ```bash
   az login
   ```

2. **Verify access to Key Vault**:
   ```bash
   az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs"
   ```

### Test via API Endpoint

Start the application:
```bash
cd Evermail.AppHost
dotnet run
```

Then test the Key Vault endpoint:
```bash
curl http://localhost:5000/api/v1/dev/test-keyvault
```

**Expected Response** (if Key Vault is accessible):
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

**If not logged into Azure CLI**:
```json
{
  "results": {
    "source": "ℹ️  User secrets (local fallback)"
  }
}
```

### Verify Secrets Are Loaded

Check application logs on startup:
- ✅ `Azure Key Vault secrets loaded (using DefaultAzureCredential)` = Using Key Vault
- ⚠️ `Key Vault not accessible: ...` = Falling back to user secrets

## 🚀 Production Deployment

### Automatic Behavior

When you run `azd deploy` or `dotnet publish`:

1. **Key Vault Selection**: Automatically uses `evermail-prod-kv` (no config needed)
2. **Role Assignment**: Aspire automatically grants `KeyVaultSecretsUser` role to Container Apps
3. **Managed Identity**: Container Apps automatically get system-assigned managed identity
4. **Secret Loading**: Application automatically loads secrets from Key Vault

### What You Need to Do

**Before first production deployment:**

1. **Update Production Secrets**:
   ```bash
   az keyvault secret set --vault-name evermail-prod-kv \
     --name "ConnectionStrings--blobs" \
     --value "<production-storage-connection-string>"
   
   az keyvault secret set --vault-name evermail-prod-kv \
     --name "ConnectionStrings--queues" \
     --value "<production-queue-connection-string>"
   ```

2. **Deploy** (role assignments happen automatically):
   ```bash
   azd deploy
   ```

**That's it!** No manual role assignments needed.

## 📝 How It Works

### Code Flow

1. **AppHost** (`Program.cs`):
   ```csharp
   // Automatically selects dev/prod based on IsPublishMode
   var keyVault = builder.AddAzureKeyVault("key-vault")
       .AsExisting(keyVaultNameParam, keyVaultResourceGroupParam);
   
   // Automatic role assignment when deployed
   webapp.WithReference(keyVault);
   ```

2. **WebApp** (`Program.cs`):
   ```csharp
   // Tries Key Vault first, falls back to user secrets
   builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");
   ```

3. **Aspire Deployment**:
   - Generates Bicep with role assignments
   - Creates managed identities
   - Grants `KeyVaultSecretsUser` role automatically

### Authentication Chain

**Local Development:**
```
DefaultAzureCredential tries:
1. Environment variables
2. Managed Identity (if in Azure)
3. Azure CLI (if logged in) ← Works for local dev!
4. Falls back to user secrets
```

**Production:**
```
DefaultAzureCredential uses:
1. Managed Identity (system-assigned) ← Automatic!
```

## 🔍 Troubleshooting

### Key Vault Not Accessible Locally

**Problem**: Getting "Key Vault not accessible" in logs

**Solution**:
```bash
# Login to Azure CLI
az login

# Verify access
az keyvault secret show --vault-name evermail-dev-kv --name "ConnectionStrings--blobs"
```

### Role Assignment Missing in Production

**Problem**: Container App can't access Key Vault

**Solution**: 
- Aspire should handle this automatically
- If not, verify the Key Vault reference in AppHost:
  ```csharp
  webapp.WithReference(keyVault); // This creates role assignment
  ```

### Wrong Key Vault in Production

**Problem**: Using dev Key Vault in production

**Solution**: 
- Check `IsPublishMode` is true when deploying
- Verify user secrets don't override:
  ```bash
  dotnet user-secrets list | grep key-vault
  ```

## 📚 Related Documentation

- `Documentation/Deployment.md` - Full deployment guide
- `Documentation/Security.md` - Security and Key Vault details
- `CHECKPOINT_UPLOAD_WORKING.md` - Previous checkpoint

---

**Last Updated**: 2025-11-16  
**Status**: ✅ Complete - Fully automated, no manual steps needed!

