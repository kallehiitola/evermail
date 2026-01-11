using Amazon.SecurityToken;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Evermail.IngestionWorker;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.Infrastructure.Services.Archives;
using Evermail.Infrastructure.Services.Encryption;
using Evermail.Infrastructure.Configuration;
using Evermail.Common.Runtime;
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
var runtimeMode = EvermailRuntimeResolver.ResolveMode(builder.Configuration, builder.Environment);
var connectionString = EvermailRuntimeResolver.ResolveConnectionString(builder.Configuration, runtimeMode, "evermaildb");
if (string.IsNullOrEmpty(connectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true";
        Console.WriteLine("ℹ️  Using localdb for ingestion worker (evermaildb connection string missing)");
    }
    else
    {
        throw new InvalidOperationException("Connection string 'evermaildb' is not configured");
    }
}

builder.Services.Configure<OfflineByokOptions>(
    builder.Configuration.GetSection("OfflineByok"));

builder.Services.AddDbContext<EvermailDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Note: TenantContext is not needed for worker (no user context)
}, ServiceLifetime.Scoped);

// Azure Blob Storage
builder.Services.AddSingleton(sp =>
{
    var blobsConnection = EvermailRuntimeResolver.ResolveConnectionString(builder.Configuration, runtimeMode, "blobs");
    if (string.IsNullOrEmpty(blobsConnection))
    {
        if (builder.Environment.IsDevelopment())
        {
            blobsConnection = "UseDevelopmentStorage=true";
            Console.WriteLine("ℹ️  Using Azurite for blob storage (blobs connection string missing)");
        }
        else
        {
            throw new InvalidOperationException("Connection string 'blobs' is not configured");
        }
    }
    return new BlobServiceClient(blobsConnection);
});

// Azure Queue Storage
builder.Services.AddSingleton(sp =>
{
    var queuesConnection = EvermailRuntimeResolver.ResolveConnectionString(builder.Configuration, runtimeMode, "queues");
    if (string.IsNullOrEmpty(queuesConnection))
    {
        if (builder.Environment.IsDevelopment())
        {
            queuesConnection = "UseDevelopmentStorage=true";
            Console.WriteLine("ℹ️  Using Azurite for queue storage (queues connection string missing)");
        }
        else
        {
            throw new InvalidOperationException("Connection string 'queues' is not configured");
        }
    }
    return new QueueServiceClient(queuesConnection);
});

// Services
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddSingleton<IAmazonSecurityTokenService>(_ => new AmazonSecurityTokenServiceClient());
builder.Services.AddSingleton<IAwsKmsConnector, AwsKmsConnector>();
builder.Services.AddSingleton<IKeyWrappingProvider, EvermailManagedWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingProvider, AzureKeyVaultWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingProvider, AwsKmsWrappingProvider>();
builder.Services.AddSingleton<IOfflineByokKeyProtector, OfflineByokKeyProtector>();
builder.Services.AddSingleton<IKeyWrappingProvider, OfflineByokWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingService, KeyWrappingService>();
builder.Services.AddSingleton<PstToMboxWriter>();
builder.Services.AddScoped<IArchivePreparationService, ArchivePreparationService>();
builder.Services.AddScoped<MailboxProcessingService>();
builder.Services.AddScoped<MailboxDeletionService>();
builder.Services.AddScoped<IMailboxEncryptionStateService, MailboxEncryptionStateService>();

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
