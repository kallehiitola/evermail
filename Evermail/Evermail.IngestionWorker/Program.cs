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
