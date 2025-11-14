var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server with database (runs locally in container, deploys to Azure SQL)
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)  // Persist data between restarts
    .AddDatabase("evermaildb");

// Add Azure Storage (runs Azurite locally, deploys to Azure Storage)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(c => c.WithLifetime(ContainerLifetime.Persistent));

// Add blob and queue resources
var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");

// Add WebApp (Blazor Web App - hybrid SSR + WASM)
// Pin to static ports for OAuth redirect URIs (7136 HTTPS, 5264 HTTP)
var webapp = builder.AddProject<Projects.Evermail_WebApp>("webapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues)
    .WithHttpsEndpoint(port: 7136, name: "https")
    .WithHttpEndpoint(port: 5264, name: "http")
    .WithExternalHttpEndpoints();

// Add AdminApp (Blazor Server)
// Pin to static ports for consistent access (7137 HTTPS, 5265 HTTP)
var adminapp = builder.AddProject<Projects.Evermail_AdminApp>("adminapp")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues)
    .WithHttpsEndpoint(port: 7137, name: "https")
    .WithHttpEndpoint(port: 5265, name: "http")
    .WithExternalHttpEndpoints();

// Add IngestionWorker (Background Service)
builder.AddProject<Projects.Evermail_IngestionWorker>("worker")
    .WithReference(sql)
    .WithReference(blobs)
    .WithReference(queues);

builder.Build().Run();
