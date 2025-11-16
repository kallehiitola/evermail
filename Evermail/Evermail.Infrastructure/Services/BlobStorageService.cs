using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Evermail.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string ContainerName = "mailbox-archives";

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<SasUploadInfo> GenerateUploadSasTokenAsync(
        Guid tenantId,
        Guid mailboxId,
        string fileName,
        TimeSpan validity)
    {
        // Blob path: mailbox-archives/{tenantId}/{mailboxId}/{guid}_{filename}
        // This ensures multi-tenant isolation and unique file names
        var blobPath = $"{tenantId}/{mailboxId}/{Guid.NewGuid()}_{fileName}";
        
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync();
        
        var blobClient = containerClient.GetBlobClient(blobPath);
        
        // Generate SAS token with write permission
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b", // blob
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Clock skew tolerance
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
        };
        
        // Grant create and write permissions for upload
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);
        
        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        
        // Fix for local development: Azurite HTTP/HTTPS port mapping
        // Azurite: HTTP port 10000, HTTPS port 10001 (when using SSL)
        // For now, use HTTP but accessed from same-origin HTTP endpoint
        var sasUrl = sasUri.ToString();
        if (sasUrl.StartsWith("http://127.0.0.1:") || sasUrl.StartsWith("http://localhost:"))
        {
            // Convert Azurite HTTP port (10000) to HTTPS port (10001)
            sasUrl = sasUrl.Replace("http://127.0.0.1:", "https://127.0.0.1:")
                          .Replace("http://localhost:", "https://localhost:");
            // Note: Azurite must be started with --cert and --key for HTTPS
            // Or we just accept HTTP for local dev and use real Azure for prod
        }
        
        return new SasUploadInfo(
            sasUrl,
            blobPath,
            sasBuilder.ExpiresOn.UtcDateTime
        );
    }
    
    public async Task<string> GenerateDownloadSasTokenAsync(
        string blobPath,
        TimeSpan validity)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        
        // Verify blob exists before generating SAS token
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Blob not found: {blobPath}");
        }
        
        // Generate SAS token with read permission
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
        };
        
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        
        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }
    
    public async Task<bool> BlobExistsAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        return await blobClient.ExistsAsync();
    }
    
    public async Task DeleteBlobAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
    }
}

