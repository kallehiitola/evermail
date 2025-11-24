namespace Evermail.Infrastructure.Services;

public record SasUploadInfo(
    string SasUrl,
    string BlobPath,
    DateTime ExpiresAt
);

public interface IBlobStorageService
{
    /// <summary>
    /// Generates a SAS token URL for uploading a file directly to Azure Blob Storage.
    /// </summary>
    Task<SasUploadInfo> GenerateUploadSasTokenAsync(
        Guid tenantId,
        Guid mailboxId,
        string fileName,
        TimeSpan validity);
    
    /// <summary>
    /// Generates a SAS token URL for downloading a file from Azure Blob Storage.
    /// </summary>
    Task<string> GenerateDownloadSasTokenAsync(
        string blobPath,
        TimeSpan validity);
    
    /// <summary>
    /// Checks if a blob exists at the specified path.
    /// </summary>
    Task<bool> BlobExistsAsync(string blobPath);
    
    /// <summary>
    /// Deletes a blob at the specified path.
    /// </summary>
    Task DeleteBlobAsync(string blobPath);

    /// <summary>
    /// Uploads a GDPR export bundle and returns the fully-qualified blob path (container/prefix + blob name).
    /// </summary>
    Task<string> UploadExportAsync(Guid tenantId, Guid exportId, Stream content, CancellationToken cancellationToken = default);
}

