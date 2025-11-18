using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Evermail.IngestionWorker;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults (Aspire telemetry, service discovery)
builder.AddServiceDefaults();

// Load secrets from Azure Key Vault when in production/cloud environments
try
{
    builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");
    Console.WriteLine("✅ Azure Key Vault secrets loaded");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Key Vault not accessible: {ex.Message}");
    Console.WriteLine("ℹ️  Falling back to local configuration");
}

// Database connection
var connectionString = builder.Configuration.GetConnectionString("evermaildb")
    ?? throw new InvalidOperationException("Connection string 'evermaildb' is not configured");

builder.Services.AddDbContext<EvermailDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Note: TenantContext is not needed for worker (no user context)
}, ServiceLifetime.Scoped);

// Azure Blob Storage
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("blobs")
        ?? throw new InvalidOperationException("Connection string 'blobs' is not configured");
    return new BlobServiceClient(connectionString);
});

// Azure Queue Storage
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("queues")
        ?? throw new InvalidOperationException("Connection string 'queues' is not configured");
    return new QueueServiceClient(connectionString);
});

// Services
builder.Services.AddScoped<MailboxProcessingService>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
