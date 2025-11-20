<#
.SYNOPSIS
    Helper script for Evermail tenants to provision a Key Vault + TMK with the required permissions.

.DESCRIPTION
    - Creates/updates the resource group and Key Vault
    - Generates an RSA-HSM key suitable for envelope encryption
    - Grants the Evermail managed identity the minimal key permissions (get, wrapKey, unwrapKey, release, decrypt, encrypt)
    - Outputs the values that must be pasted into the Evermail admin portal

.EXAMPLE
    pwsh scripts/tenant-keyvault-onboarding.ps1 `
        -ResourceGroup evermail-secure-rg `
        -Region westeurope `
        -KeyVaultName contoso-evermail-kv `
        -EvermailManagedIdentityObjectId 11111111-2222-3333-4444-555555555555 `
        -KeyName evermail-tmk
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Region,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9-]{3,24}$')]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$EvermailManagedIdentityObjectId,

    [string]$KeyName = "evermail-tmk"
)

Write-Host "🔐 Evermail Tenant Key Vault Onboarding" -ForegroundColor Cyan
Write-Host ""

Write-Host "1) Ensuring resource group '$ResourceGroup' exists..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Region | Out-Null

Write-Host "2) Ensuring Key Vault '$KeyVaultName' exists..." -ForegroundColor Yellow
az keyvault create `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --location $Region `
    --enable-purge-protection true `
    --enable-rbac-authorization false `
    --sku Premium | Out-Null

Write-Host "3) Creating (or updating) key '$KeyName'..." -ForegroundColor Yellow
az keyvault key create `
    --name $KeyName `
    --vault-name $KeyVaultName `
    --kty RSA-HSM `
    --size 4096 `
    --ops wrapKey unwrapKey encrypt decrypt release `
    --exportable true | Out-Null

Write-Host "4) Granting Evermail managed identity least-privileged access..." -ForegroundColor Yellow
az keyvault set-policy `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --object-id $EvermailManagedIdentityObjectId `
    --key-permissions get wrapKey unwrapKey release encrypt decrypt | Out-Null

$key = az keyvault key show --name $KeyName --vault-name $KeyVaultName --query "{kid:id,version:attributes.version}" -o json | ConvertFrom-Json
$kv = az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --query "{uri:properties.vaultUri}" -o json | ConvertFrom-Json
$tenantId = az account show --query tenantId -o tsv

Write-Host ""
Write-Host "✅ All set! Copy the following values into the Evermail admin portal:" -ForegroundColor Green
Write-Host " ------------------------------------------------------------------------"
Write-Host (" Key Vault URI            : {0}" -f $kv.uri)
Write-Host (" Key Name                 : {0}" -f $KeyName)
Write-Host (" Key Version              : {0}" -f $key.version)
Write-Host (" Azure AD Tenant ID       : {0}" -f $tenantId)
Write-Host (" Managed Identity ObjectID: {0}" -f $EvermailManagedIdentityObjectId)
Write-Host " ------------------------------------------------------------------------"
Write-Host ""
Write-Host "⚠️  Reminder: enable soft-delete and purge protection to protect your TMK." -ForegroundColor Yellow
Write-Host "📄  Save this output somewhere safe; you'll need it if you rotate the key."


