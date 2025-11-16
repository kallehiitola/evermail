var builder = DistributedApplication.CreateBuilder(args);

// Use a fixed password for SQL Server in development (required for data persistence)
// In production, Azure SQL will use managed identities
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Add SQL Server with database (runs locally in container, deploys to Azure SQL)
var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent)  // Persist container between restarts
    .WithDataVolume("evermail-sql-data")        // Persist data in named volume
    .AddDatabase("evermaildb");

// Add Azure Storage (runs Azurite locally, deploys to Azure Storage)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(c => c
        .WithLifetime(ContainerLifetime.Persistent)  // Persist container
        .WithDataVolume("evermail-azurite-data"));   // Persist blob/queue data

// Add blob and queue resources
var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");

// Add WebApp (Blazor Web App - hybrid SSR + WASM)
// Ports defined in Properties/launchSettings.json: 7136 HTTPS, 5264 HTTP
// These ports are fixed in launchSettings and won't change between restarts
var webapp = builder.AddProject<Projects.Evermail_WebApp>("webapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues);

// Add AdminApp (Blazor Server)
var adminapp = builder.AddProject<Projects.Evermail_AdminApp>("adminapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues);

// Add IngestionWorker (Background Service)
builder.AddProject<Projects.Evermail_IngestionWorker>("worker")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues);

builder.Build().Run();
