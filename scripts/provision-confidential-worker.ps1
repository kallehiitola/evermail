param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$AcrName,

    [Parameter(Mandatory = $true)]
    [string]$SubnetResourceId,

    [string]$EnvironmentName = "evermail-confidential-env",
    [string]$ContainerAppName = "evermail-confidential-worker",
    [string]$WorkloadProfileName = "confidential-profile",
    [string]$ImageName = "evermail-ingestion-worker",
    [string]$ImageTag = "confidential",
    [string]$UserAssignedIdentityName = "evermail-conf-worker-mi",
    [string]$KeyVaultName = "evermail-prod-kv",
    [string]$KeyVaultResourceGroup = "evermail-prod",
    [string]$SqlSecretName = "ConnectionStrings--evermaildb",
    [string]$BlobSecretName = "ConnectionStrings--blobs",
    [string]$QueueSecretName = "ConnectionStrings--queues"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-AzCli {
    try {
        az account show --output none | Out-Null
    }
    catch {
        throw "Azure CLI is not logged in. Run `az login` before executing this script."
    }
}

Ensure-AzCli

$subscriptionId = az account show --query id -o tsv
$fullImageName = "$AcrName.azurecr.io/$ImageName:$ImageTag"

Write-Step "Building ingestion worker container image ($fullImageName)"
az acr login --name $AcrName | Out-Null
docker build `
    -f "Evermail/Evermail.IngestionWorker/Dockerfile" `
    -t $fullImageName `
    .

Write-Step "Pushing image to Azure Container Registry"
docker push $fullImageName | Out-Null

Write-Step "Creating (or updating) user-assigned managed identity $UserAssignedIdentityName"
$identity = az identity create `
    --name $UserAssignedIdentityName `
    --resource-group $ResourceGroup `
    --location $Location `
    --query "{clientId:clientId,principalId:principalId,id:id}" `
    -o json | ConvertFrom-Json

Write-Step "Granting Key Vault permissions to the managed identity"
$keyVaultScope = "/subscriptions/$subscriptionId/resourceGroups/$KeyVaultResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"

az role assignment create `
    --assignee-object-id $identity.principalId `
    --assignee-principal-type ServicePrincipal `
    --role "Key Vault Secrets User" `
    --scope $keyVaultScope `
    --only-show-errors `
    --output none

az role assignment create `
    --assignee-object-id $identity.principalId `
    --assignee-principal-type ServicePrincipal `
    --role "Key Vault Crypto Service Release" `
    --scope $keyVaultScope `
    --only-show-errors `
    --output none

Write-Step "Creating Confidential Container Apps environment $EnvironmentName"
az containerapp env create `
    --name $EnvironmentName `
    --resource-group $ResourceGroup `
    --location $Location `
    --infrastructure-subnet-resource-id $SubnetResourceId `
    --workload-profiles "[{'name':'$WorkloadProfileName','workloadProfileType':'Confidential','minimumCount':1,'maximumCount':3}]" `
    --output none

Write-Step "Fetching connection strings from Key Vault"
$sqlConn = az keyvault secret show --vault-name $KeyVaultName --name $SqlSecretName --query value -o tsv
$blobConn = az keyvault secret show --vault-name $KeyVaultName --name $BlobSecretName --query value -o tsv
$queueConn = az keyvault secret show --vault-name $KeyVaultName --name $QueueSecretName --query value -o tsv

Write-Step "Creating or updating container app $ContainerAppName"
az containerapp create `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --environment $EnvironmentName `
    --image $fullImageName `
    --workload-profile-name $WorkloadProfileName `
    --ingress internal `
    --target-port 8080 `
    --min-replicas 1 `
    --max-replicas 3 `
    --registry-server "$AcrName.azurecr.io" `
    --registry-identity $identity.id `
    --secrets sql-conn="$sqlConn" blob-conn="$blobConn" queue-conn="$queueConn" `
    --env-vars `
        "ConnectionStrings__evermaildb=secretref:sql-conn" `
        "ConnectionStrings__blobs=secretref:blob-conn" `
        "ConnectionStrings__queues=secretref:queue-conn" `
        "Parameters__key-vault-name=$KeyVaultName" `
        "Parameters__key-vault-resource-group=$KeyVaultResourceGroup" `
    --user-assigned $identity.id `
    --revision-mode Single `
    --only-show-errors `
    --output none

Write-Step "Confidential worker provisioning complete."

