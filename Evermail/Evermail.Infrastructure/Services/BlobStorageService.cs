using System.Collections.Generic;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Evermail.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string ContainerName = "mailbox-archives";
    private const string ExportsContainerName = "gdpr-exports";

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
        var (containerName, blobName) = ResolveContainer(blobPath);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        
        // Verify blob exists before generating SAS token
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"Blob not found: {blobPath}");
        }
        
        // Generate SAS token with read permission
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
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
        var (containerName, blobName) = ResolveContainer(blobPath);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return await blobClient.ExistsAsync();
    }
    
    public async Task DeleteBlobAsync(string blobPath)
    {
        var (containerName, blobName) = ResolveContainer(blobPath);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<string> UploadExportAsync(Guid tenantId, Guid exportId, Stream content, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ExportsContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{tenantId}/{exportId}.zip";
        var blobClient = containerClient.GetBlobClient(blobName);

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/zip" },
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = tenantId.ToString(),
                    ["exportId"] = exportId.ToString()
                }
            },
            cancellationToken);

        return $"{ExportsContainerName}/{blobName}";
    }

    private static (string Container, string BlobName) ResolveContainer(string blobPath)
    {
        if (blobPath.StartsWith($"{ExportsContainerName}/", StringComparison.OrdinalIgnoreCase))
        {
            return (ExportsContainerName, blobPath.Substring(ExportsContainerName.Length + 1));
        }

        return (ContainerName, blobPath);
    }
}

