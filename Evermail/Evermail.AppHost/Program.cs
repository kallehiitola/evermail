using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Key Vault resources (existing Key Vaults created manually)
// Automatically selects dev or prod Key Vault based on publish mode
// In local dev: uses evermail-dev-kv
// In production: uses evermail-prod-kv
var keyVaultName = builder.ExecutionContext.IsPublishMode ? "evermail-prod-kv" : "evermail-dev-kv";
var keyVaultResourceGroup = builder.ExecutionContext.IsPublishMode ? "evermail-prod" : "evermail-dev";

// Use environment variables if set, otherwise use defaults above
var keyVaultNameParam = builder.AddParameter("key-vault-name", secret: false);
var keyVaultResourceGroupParam = builder.AddParameter("key-vault-resource-group", secret: false);

var keyVault = builder.AddAzureKeyVault("key-vault")
    .AsExisting(keyVaultNameParam, keyVaultResourceGroupParam);

// Use a fixed password for SQL Server in development (required for data persistence)
// In production, Azure SQL will use managed identities
// Password can come from Key Vault or user secrets (for local dev)
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Add SQL Server locally; in publish mode we rely on the Key Vault connection string
IResourceBuilder<IResourceWithConnectionString> sql;

if (builder.ExecutionContext.IsPublishMode)
{
    sql = builder.AddConnectionString("evermaildb");
}
else
{
    var sqlServer = builder.AddSqlServer("sql", password: sqlPassword)
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume("evermail-sql-data")
        .WithDockerfile("../../docker/sqlserver");

    sql = sqlServer.AddDatabase("evermaildb");
}

// Add Azure Storage using connection strings (existing storage account)
// This avoids auto-provisioning and RBAC role assignment issues
// Falls back to Azurite if no connection string is configured
var blobs = builder.AddConnectionString("blobs");
var queues = builder.AddConnectionString("queues");

// Add Migration Service - runs migrations before other services start
// This ensures the database schema is up-to-date before the app starts
var migrations = builder.AddProject<Projects.Evermail_MigrationService>("migrations")
    .WithReference(sql)
    .WithReference(keyVault)
    .WaitFor(sql);

// Add WebApp (Blazor Web App - hybrid SSR + WASM)
// Ports defined in Properties/launchSettings.json: 7136 HTTPS, 5264 HTTP
// These ports are fixed in launchSettings and won't change between restarts
// Note: WithReference(keyVault) automatically grants KeyVaultSecretsUser role when deployed
// WaitFor(migrations) ensures migrations complete before WebApp starts
var webapp = builder.AddProject<Projects.Evermail_WebApp>("webapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(keyVault)
    .WaitForCompletion(migrations);

// Add AdminApp (Blazor Server)
// WaitFor(migrations) ensures migrations complete before AdminApp starts
var adminapp = builder.AddProject<Projects.Evermail_AdminApp>("adminapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(keyVault)
    .WaitForCompletion(migrations);

// Dev-only: enable "Dev bypass" button for fast UI iteration without breaking OAuth.
// This becomes an env var for the AdminApp process when running locally (non-publish).
if (!builder.ExecutionContext.IsPublishMode)
{
    adminapp.WithEnvironment("AdminAuth__DevBypassEnabled", "true");
}

// Add IngestionWorker (Background Service)
// WaitFor(migrations) ensures migrations complete before Worker starts
builder.AddProject<Projects.Evermail_IngestionWorker>("worker")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(keyVault)
    .WaitForCompletion(migrations);

builder.Build().Run();
