# Phase 1: Large File Upload & Email Parsing - Implementation Plan (Historical)

> ⚠️ **Status note (2025-12-16)**: This document is **historical** and kept for reference.  
> The Phase 1 goals have been implemented and then significantly expanded (multi-format uploads, lifecycle jobs, audit logging, rate limiting, GDPR self-service, zero-access encrypted uploads, SKR scaffolding, and Azure production runway work).
>
> **Canonical “where we are now”**:
> - `Documentation/ProgressReports/ProgressReport.md` (timeline + current status)
> - `Documentation/Architecture.md` (current ingestion/onboarding architecture)
> - `Documentation/API.md` (current upload + encrypted-upload contracts)
> - `Documentation/Deployment.md` (Azure deployment guidance + production runway)

> **Original Date**: 2025-11-14  
> **Original Status**: Ready to Start (at the time)

---

## 🎯 Goal

Build a **production-ready large file upload system** that:
- Handles files up to **100GB** (Enterprise tier)
- Supports **.mbox files, Google Takeout zip, Microsoft export zip**
- Uploads **directly to Azure Blob Storage** (no server bandwidth)
- Shows **real-time progress** (critical for huge files)
- Queues for **background processing** (scalable)
- **Parses emails** with MimeKit
- **Stores in database** for search and display

---

## ✅ Phase 1 Outcome (Implemented)

This Phase 1 scope is now delivered (and extended). At a high level, Evermail currently supports:

- **Direct-to-Blob uploads** with an initiate/complete handshake (plus re-import history).
- **Automatic archive format detection** and **normalization** (mbox, zip bundles, pst/ost, eml bundles), including **plan-aware inflation guardrails**.
- **Queue-driven ingestion + deletion** lifecycle with progress tracking and audit logging.
- **Zero-access encrypted upload mode** (`/api/v1/mailboxes/encrypted-upload/*`) with deterministic token indexing for mailbox tags and header-derived tokens (from/to/cc/subject).

For the exact current behaviors and endpoint contracts, follow the canonical docs listed at the top of this file.

---

## ✅ What's Already Done

### Step 1: Subscription Tier Limits ✅
**Commit**: `992dde6` - "feat: add MaxFileSizeGB limits to subscription tiers"

**File Size Limits:**
- **Free**: 1GB max file (1GB total storage)
- **Pro**: 5GB max file (5GB total storage)
- **Team**: 10GB max file (500GB total storage)
- **Enterprise**: 100GB max file (2TB total storage) ← **Supports your 40GB test file!**

**Files Modified:**
- `SubscriptionPlan.cs` - Added `MaxFileSizeGB` property
- `DataSeeder.cs` - Updated all 4 tiers with limits

**Next**: Create database migration for this change

---

## 📋 Implementation Steps (11 Remaining)

### Step 2: Database Migration for MaxFileSizeGB (5 minutes)

**Commands:**
```bash
cd /Users/kallehiitola/Work/evermail/Evermail/Evermail.Infrastructure
dotnet ef migrations add AddMaxFileSizeToSubscriptionPlans --startup-project ../Evermail.WebApp/Evermail.WebApp/Evermail.WebApp.csproj
```

**Expected:**
- Migration file created
- Will add `MaxFileSizeGB` column to `SubscriptionPlans` table
- Applied automatically on next Aspire run

---

### Step 3: Install Azure.Storage.Blobs Package (2 minutes)

**Add to** `Evermail.Infrastructure/Evermail.Infrastructure.csproj`:
```xml
<PackageReference Include="Azure.Storage.Blobs" Version="12.22.2" />
<PackageReference Include="Azure.Storage.Queues" Version="12.20.1" />
```

**Commands:**
```bash
cd /Users/kallehiitola/Work/evermail/Evermail
dotnet add Evermail.Infrastructure/Evermail.Infrastructure.csproj package Azure.Storage.Blobs
dotnet add Evermail.Infrastructure/Evermail.Infrastructure.csproj package Azure.Storage.Queues
dotnet restore
```

---

### Step 4: Create BlobStorageService (30 minutes)

**Create:** `Evermail.Infrastructure/Services/IBlobStorageService.cs`

```csharp
namespace Evermail.Infrastructure.Services;

public record SasUploadInfo(
    string SasUrl,
    string BlobPath,
    DateTime ExpiresAt
);

public interface IBlobStorageService
{
    Task<SasUploadInfo> GenerateUploadSasTokenAsync(
        Guid tenantId,
        Guid mailboxId,
        string fileName,
        TimeSpan validity);
    
    Task<string> GenerateDownloadSasTokenAsync(
        string blobPath,
        TimeSpan validity);
    
    Task<bool> BlobExistsAsync(string blobPath);
    
    Task DeleteBlobAsync(string blobPath);
}
```

**Create:** `Evermail.Infrastructure/Services/BlobStorageService.cs`

```csharp
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
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
        };
        
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);
        
        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        
        return new SasUploadInfo(
            sasUri.ToString(),
            blobPath,
            sasBuilder.ExpiresOn.UtcDateTime
        );
    }
    
    // ... other methods
}
```

**Register in** `Program.cs`:
```csharp
// Configure Azure Blob Storage
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("blobs");
    return new BlobServiceClient(connectionString);
});
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
```

---

### Step 5: Create Upload API Endpoint (20 minutes)

**Create:** `Evermail.Common/DTOs/Upload/UploadRequest.cs`

```csharp
namespace Evermail.Common.DTOs.Upload;

public record InitiateUploadRequest(
    string FileName,
    long FileSizeBytes,
    string FileType  // "mbox", "google-takeout-zip", "microsoft-export-zip"
);

public record InitiateUploadResponse(
    string UploadUrl,  // Azure Blob SAS URL
    string BlobPath,
    Guid MailboxId,
    DateTime ExpiresAt
);
```

**Create:** `Evermail.WebApp/Endpoints/UploadEndpoints.cs`

```csharp
public static class UploadEndpoints
{
    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/initiate", InitiateUploadAsync);
        group.MapPost("/complete", CompleteUploadAsync);
        
        return group;
    }

    private static async Task<IResult> InitiateUploadAsync(
        InitiateUploadRequest request,
        IBlobStorageService blobService,
        EvermailDbContext context,
        TenantContext tenantContext)
    {
        // 1. Get tenant's subscription plan
        var tenant = await context.Tenants.FindAsync(tenantContext.TenantId);
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name == tenant.SubscriptionTier);
        
        // 2. Validate file size against plan
        var fileSizeGB = request.FileSizeBytes / (1024.0 * 1024.0 * 1024.0);
        if (fileSizeGB > plan.MaxFileSizeGB)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"File size ({fileSizeGB:F2} GB) exceeds your plan limit ({plan.MaxFileSizeGB} GB). Please upgrade."
            ));
        }
        
        // 3. Create Mailbox record (Pending status)
        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = tenantContext.UserId,
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            BlobPath = string.Empty, // Will be set after upload
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        
        context.Mailboxes.Add(mailbox);
        await context.SaveChangesAsync();
        
        // 4. Generate SAS token (2 hours validity for large uploads)
        var sasInfo = await blobService.GenerateUploadSasTokenAsync(
            tenantContext.TenantId,
            mailbox.Id,
            request.FileName,
            TimeSpan.FromHours(2)
        );
        
        // 5. Update mailbox with blob path
        mailbox.BlobPath = sasInfo.BlobPath;
        await context.SaveChangesAsync();
        
        return Results.Ok(new ApiResponse<InitiateUploadResponse>(
            Success: true,
            Data: new InitiateUploadResponse(
                sasInfo.SasUrl,
                sasInfo.BlobPath,
                mailbox.Id,
                sasInfo.ExpiresAt
            )
        ));
    }

    private static async Task<IResult> CompleteUploadAsync(
        CompleteUploadRequest request,
        EvermailDbContext context,
        IQueueService queueService)
    {
        var mailbox = await context.Mailboxes.FindAsync(request.MailboxId);
        
        if (mailbox == null)
        {
            return Results.NotFound();
        }
        
        // Update status to Queued
        mailbox.Status = "Queued";
        mailbox.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        // Send message to queue for processing
        await queueService.EnqueueMailboxProcessingAsync(mailbox.Id);
        
        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { MailboxId = mailbox.Id, Status = "Queued" }
        ));
    }
}
```

**Add to** `Program.cs`:
```csharp
var api = app.MapGroup("/api/v1");
api.MapGroup("/upload").MapUploadEndpoints();
```

---

### Step 6: Create Upload UI with Progress (45 minutes)

**Create:** `Evermail.WebApp/Components/Pages/Upload.razor`

```razor
@page "/upload"
@rendermode InteractiveServer
@using Evermail.Common.DTOs.Upload
@inject HttpClient Http
@inject IJSRuntime JS
@inject NavigationManager Navigation

<PageTitle>Upload Mailbox - Evermail</PageTitle>

<div class="container mt-4">
    <h1>Upload Email Archive</h1>
    
    @if (_isUploading)
    {
        <div class="card">
            <div class="card-body">
                <h5>Uploading @_fileName...</h5>
                <div class="progress mb-3" style="height: 30px;">
                    <div class="progress-bar progress-bar-striped progress-bar-animated" 
                         role="progressbar" 
                         style="width: @(_uploadProgress)%">
                        @(_uploadProgress)%
                    </div>
                </div>
                <p class="text-muted">
                    @_uploadedMB MB / @_totalMB MB
                    @if (_estimatedTimeRemaining > TimeSpan.Zero)
                    {
                        <text> - @_estimatedTimeRemaining.ToString(@"hh\:mm\:ss") remaining</text>
                    }
                </p>
                <p class="small text-muted">Upload speed: @_uploadSpeedMBps MB/s</p>
                <button class="btn btn-danger" @onclick="CancelUpload">Cancel Upload</button>
            </div>
        </div>
    }
    else
    {
        <div class="card">
            <div class="card-body">
                <h5>Select File Type</h5>
                
                <div class="mb-4">
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="fileType" id="mbox" value="mbox" @onchange="() => _fileType = 'mbox'" checked="@(_fileType == "mbox")">
                        <label class="form-check-label" for="mbox">
                            <strong>.mbox File</strong> - Single mailbox export (Gmail, Thunderbird, Apple Mail)
                        </label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="fileType" id="google" value="google-takeout-zip" @onchange="() => _fileType = "google-takeout-zip"">
                        <label class="form-check-label" for="google">
                            <strong>Google Takeout ZIP</strong> - Full Google account export
                        </label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="fileType" id="microsoft" value="microsoft-export-zip" @onchange="() => _fileType = "microsoft-export-zip"">
                        <label class="form-check-label" for="microsoft">
                            <strong>Microsoft Export ZIP</strong> - Outlook/Office 365 export
                        </label>
                    </div>
                </div>

                <div class="mb-3">
                    <label class="form-label">Choose File</label>
                    <InputFile OnChange="HandleFileSelected" class="form-control" accept=".mbox,.zip" />
                    <small class="text-muted">
                        Your plan allows up to <strong>@_maxFileSizeGB GB</strong> per file
                    </small>
                </div>
                
                @if (_selectedFile != null)
                {
                    <div class="alert alert-info">
                        <strong>Selected:</strong> @_fileName<br />
                        <strong>Size:</strong> @_totalMB MB (@_totalGB GB)
                    </div>
                }
                
                <button class="btn btn-primary btn-lg" @onclick="StartUpload" disabled="@(_selectedFile == null)">
                    <i class="bi bi-cloud-upload"></i> Start Upload
                </button>
            </div>
        </div>
    }
</div>

@code {
    private IBrowserFile? _selectedFile;
    private string _fileName = "";
    private string _fileType = "mbox";
    private long _totalBytes;
    private double _totalMB;
    private double _totalGB;
    private int _maxFileSizeGB = 1;
    
    private bool _isUploading;
    private int _uploadProgress;
    private long _uploadedBytes;
    private double _uploadedMB;
    private double _uploadSpeedMBps;
    private TimeSpan _estimatedTimeRemaining;

    protected override async Task OnInitializedAsync()
    {
        // TODO: Get tenant's plan and max file size from API
        _maxFileSizeGB = 100; // For testing - will be from tenant's plan
    }

    private void HandleFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        _fileName = e.File.Name;
        _totalBytes = e.File.Size;
        _totalMB = _totalBytes / (1024.0 * 1024.0);
        _totalGB = _totalBytes / (1024.0 * 1024.0 * 1024.0);
    }

    private async Task StartUpload()
    {
        if (_selectedFile == null) return;
        
        // 1. Validate file size
        if (_totalGB > _maxFileSizeGB)
        {
            // Show error
            return;
        }
        
        _isUploading = true;
        
        try
        {
            // 2. Get SAS URL from backend
            var initiateResponse = await Http.PostAsJsonAsync("/api/v1/upload/initiate", new
            {
                FileName = _fileName,
                FileSizeBytes = _totalBytes,
                FileType = _fileType
            });
            
            var result = await initiateResponse.Content.ReadFromJsonAsync<ApiResponse<InitiateUploadResponse>>();
            
            if (result?.Success != true || result.Data == null)
            {
                // Handle error
                return;
            }
            
            // 3. Upload directly to Azure Blob with progress tracking
            await UploadToAzureBlobAsync(result.Data.UploadUrl);
            
            // 4. Notify backend upload is complete (triggers queue processing)
            await Http.PostAsJsonAsync("/api/v1/upload/complete", new
            {
                MailboxId = result.Data.MailboxId
            });
            
            // 5. Navigate to processing status page
            Navigation.NavigateTo($"/mailboxes/{result.Data.MailboxId}");
        }
        catch (Exception ex)
        {
            // Handle error
            _isUploading = false;
        }
    }

    private async Task UploadToAzureBlobAsync(string sasUrl)
    {
        // This will be implemented with JavaScript Interop
        // Using Azure Blob Storage JavaScript SDK for chunked uploads
        await JS.InvokeVoidAsync("azureBlobUpload.upload", sasUrl, DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void UpdateProgress(int progress, long uploadedBytes, double speedMBps)
    {
        _uploadProgress = progress;
        _uploadedBytes = uploadedBytes;
        _uploadedMB = uploadedBytes / (1024.0 * 1024.0);
        _uploadSpeedMBps = speedMBps;
        
        if (speedMBps > 0)
        {
            var remainingBytes = _totalBytes - uploadedBytes;
            var remainingSeconds = remainingBytes / (speedMBps * 1024 * 1024);
            _estimatedTimeRemaining = TimeSpan.FromSeconds(remainingSeconds);
        }
        
        StateHasChanged();
    }

    private void CancelUpload()
    {
        // TODO: Implement cancellation
        _isUploading = false;
    }
}
```

---

### Step 7: JavaScript Azure Blob Upload (45 minutes)

**Create:** `Evermail.WebApp/wwwroot/js/azure-blob-upload.js`

**Use Azure Blob Storage JavaScript SDK:**
- Install via CDN or npm
- Implements chunked uploads (4MB blocks)
- Reports progress via callback
- Handles resume on network errors
- Supports files up to 190TB (Azure limit)

**Key Features:**
```javascript
window.azureBlobUpload = {
    upload: async function(sasUrl, dotNetRef) {
        const blockBlobClient = new BlockBlobClient(sasUrl);
        const fileInput = document.querySelector('input[type="file"]');
        const file = fileInput.files[0];
        
        const chunkSize = 4 * 1024 * 1024; // 4MB chunks
        const totalBlocks = Math.ceil(file.size / chunkSize);
        const blockIds = [];
        
        let uploadedBytes = 0;
        const startTime = Date.now();
        
        for (let i = 0; i < totalBlocks; i++) {
            const start = i * chunkSize;
            const end = Math.min(start + chunkSize, file.size);
            const chunk = file.slice(start, end);
            
            const blockId = btoa(`block-${i.toString().padStart(6, '0')}`);
            blockIds.push(blockId);
            
            await blockBlobClient.stageBlock(blockId, chunk, chunk.size);
            
            uploadedBytes += chunk.size;
            const progress = Math.round((uploadedBytes / file.size) * 100);
            
            // Calculate speed
            const elapsedSeconds = (Date.now() - startTime) / 1000;
            const speedMBps = (uploadedBytes / (1024 * 1024)) / elapsedSeconds;
            
            // Report progress to Blazor
            dotNetRef.invokeMethodAsync('UpdateProgress', progress, uploadedBytes, speedMBps);
        }
        
        // Commit all blocks
        await blockBlobClient.commitBlockList(blockIds);
    }
};
```

**Add script to** `App.razor`:
```html
<script src="https://cdn.jsdelivr.net/npm/@azure/storage-blob@12/dist/azure-storage-blob.min.js"></script>
<script src="js/azure-blob-upload.js"></script>
```

---

### Step 8: Queue Integration (20 minutes)

**Create:** `Evermail.Infrastructure/Services/IQueueService.cs`

```csharp
public interface IQueueService
{
    Task EnqueueMailboxProcessingAsync(Guid mailboxId);
}
```

**Create:** `Evermail.Infrastructure/Services/QueueService.cs`

```csharp
using Azure.Storage.Queues;

public class QueueService : IQueueService
{
    private readonly QueueClient _queueClient;
    private const string QueueName = "mailbox-processing";

    public QueueService(QueueServiceClient queueServiceClient)
    {
        _queueClient = queueServiceClient.GetQueueClient(QueueName);
    }

    public async Task EnqueueMailboxProcessingAsync(Guid mailboxId)
    {
        await _queueClient.CreateIfNotExistsAsync();
        
        var message = JsonSerializer.Serialize(new
        {
            MailboxId = mailboxId,
            EnqueuedAt = DateTime.UtcNow
        });
        
        await _queueClient.SendMessageAsync(message);
    }
}
```

---

### Step 9: Install MimeKit for Email Parsing (5 minutes)

```bash
cd /Users/kallehiitola/Work/evermail/Evermail
dotnet add Evermail.Infrastructure/Evermail.Infrastructure.csproj package MimeKit
```

**MimeKit**: Industry-standard email parsing library
- Handles all email formats
- Streaming support (doesn't load entire file in memory)
- RFC-compliant
- Used by Xamarin, K-9 Mail, countless apps

---

### Step 10: Update IngestionWorker (60 minutes)

**Update:** `Evermail.IngestionWorker/Worker.cs`

**New Architecture:**
```csharp
public class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Poll queue for messages
            var messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 10);
            
            foreach (var message in messages.Value)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<MailboxQueueMessage>(message.MessageText);
                    
                    // 2. Download blob from Azure Storage
                    var blobClient = _blobServiceClient.GetBlobContainerClient("mailbox-archives")
                        .GetBlobClient(mailbox.BlobPath);
                    
                    // 3. Stream parse with MimeKit
                    await using var stream = await blobClient.OpenReadAsync();
                    await ParseMboxStreamAsync(stream, mailbox, stoppingToken);
                    
                    // 4. Update mailbox status
                    mailbox.Status = "Completed";
                    await _context.SaveChangesAsync();
                    
                    // 5. Delete queue message
                    await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }
                catch (Exception ex)
                {
                    // Log error, update mailbox status to Failed
                    // Optionally retry
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ParseMboxStreamAsync(
        Stream stream,
        Mailbox mailbox,
        CancellationToken cancellationToken)
    {
        // Use MimeKit.MboxReader for streaming
        var parser = new MimeParser(stream);
        int processedCount = 0;
        
        while (!parser.IsEndOfStream)
        {
            var message = await parser.ParseMessageAsync(cancellationToken);
            
            // Extract email data
            var email = new EmailMessage
            {
                Id = Guid.NewGuid(),
                TenantId = mailbox.TenantId,
                UserId = mailbox.UserId,
                MailboxId = mailbox.Id,
                Subject = message.Subject,
                FromAddress = message.From.ToString(),
                // ... extract all fields
                CreatedAt = DateTime.UtcNow
            };
            
            _context.EmailMessages.Add(email);
            processedCount++;
            
            // Batch save every 500 emails
            if (processedCount % 500 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                mailbox.ProcessedEmails = processedCount;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        
        await _context.SaveChangesAsync(cancellationToken);
        mailbox.TotalEmails = processedCount;
        mailbox.ProcessedEmails = processedCount;
    }
}
```

---

### Step 11: File Validation (15 minutes)

**Validation Checks:**
1. ✅ File extension (`.mbox`, `.zip`)
2. ✅ File size vs plan limit
3. ✅ MIME type validation
4. ✅ Virus scanning (future - can use Azure Defender)
5. ✅ Zip structure validation (for Google/Microsoft zips)

---

### Step 12: Testing Strategy for 40GB File

**Test Plan:**

**1. Setup Enterprise Tier** (manually in database)
```sql
UPDATE Tenants SET SubscriptionTier = 'Enterprise' WHERE Id = 'YOUR_TENANT_ID';
```

**2. Prepare Test File**
- Your 40GB mbox file
- Located at: (you provide path)

**3. Upload Test**
- Navigate to `/upload`
- Select .mbox type
- Choose your 40GB file
- Click "Start Upload"

**Expected:**
- ✅ Progress bar shows 0% → 100%
- ✅ Upload speed displayed
- ✅ Time remaining calculated
- ✅ 4MB chunks uploaded (10,000 chunks for 40GB)
- ✅ Takes ~20 minutes on 300 Mbps connection
- ✅ Resumes if connection drops

**4. Processing Test**
- IngestionWorker picks up from queue
- Streams through 40GB file
- Parses emails in batches (500 at a time)
- Updates progress in database
- Takes 1-3 hours depending on email count

**5. Verification**
```sql
-- Check mailbox status
SELECT FileName, Status, TotalEmails, ProcessedEmails, FileSizeBytes
FROM Mailboxes;

-- Check emails
SELECT COUNT(*) FROM EmailMessages WHERE MailboxId = 'YOUR_MAILBOX_ID';

-- Check processing progress
SELECT 
    FileName,
    Status,
    ProcessedEmails,
    TotalEmails,
    (ProcessedEmails * 100.0 / NULLIF(TotalEmails, 0)) as ProgressPercent
FROM Mailboxes
WHERE Status = 'Processing';
```

---

## 🎯 Implementation Order (Recommended)

**Session 1** (1-2 hours):
1. ✅ Database migration
2. ✅ Install packages (Azure.Storage.Blobs, MimeKit)
3. ✅ Create BlobStorageService
4. ✅ Create QueueService
5. ✅ Create upload endpoints
6. **Commit & Test APIs** with Postman/curl

**Session 2** (1-2 hours):
7. ✅ Create upload UI
8. ✅ Add JavaScript upload
9. ✅ Test with small file (1MB)
10. ✅ Test with medium file (100MB)
11. **Commit & Test UI**

**Session 3** (1 hour):
12. ✅ Update IngestionWorker
13. ✅ Test end-to-end with 1GB file
14. **Commit & Test Processing**

**Session 4** (Trial by Fire!):
15. ✅ **Test with your 40GB mbox file!**
16. Monitor, optimize, fix issues
17. **Celebrate!** 🎉

---

## 📦 Required NuGet Packages

```xml
<!-- Evermail.Infrastructure.csproj -->
<PackageReference Include="Azure.Storage.Blobs" Version="12.22.2" />
<PackageReference Include="Azure.Storage.Queues" Version="12.20.1" />
<PackageReference Include="MimeKit" Version="4.8.0" />
```

---

## 🔧 Aspire Configuration

**Update** `Evermail.AppHost/Program.cs`:

```csharp
// Blob and Queue already configured!
var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");

// These connections are automatically injected into services
```

**Local Development:**
- Uses Azurite emulator (already running in Docker)
- Connection strings automatically configured by Aspire

---

## 🧪 Testing Progression

### Test 1: 1MB File (Proves Concept)
- ✅ Upload works
- ✅ Queue message sent
- ✅ Worker processes
- ✅ Emails in database

### Test 2: 100MB File (Proves Chunks)
- ✅ Chunked upload (25 chunks)
- ✅ Progress tracking works
- ✅ Speed calculation accurate

### Test 3: 1GB File (Proves Scale)
- ✅ 250 chunks
- ✅ Memory stays low
- ✅ Processing handles large file

### Test 4: 40GB File (TRIAL BY FIRE!)
- ✅ 10,000 chunks
- ✅ 20+ minute upload
- ✅ Hours of processing
- ✅ Thousands of emails
- ✅ **Production-ready proof!**

---

## 📚 Key Documentation to Read

**Before implementing, scan these:**

1. **Azure Blob Storage:**
   - https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-upload
   - https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-tune-upload-download

2. **ASP.NET Core File Uploads:**
   - https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads

3. **MimeKit:**
   - http://www.mimekit.net/docs/html/Introduction.htm
   - Streaming mbox parsing examples

4. **Azure Storage Queues:**
   - https://learn.microsoft.com/en-us/azure/storage/queues/storage-dotnet-how-to-use-queues

---

## 🎯 Success Criteria

**Phase 1 Complete When:**
- [x] User can select subscription tier
- [x] User can upload .mbox file
- [x] User can upload Google Takeout zip
- [x] User can upload Microsoft export zip
- [x] Upload shows real-time progress
- [x] Large files (40GB+) upload successfully
- [x] Files queued for background processing
- [x] IngestionWorker parses emails
- [x] Emails stored in database
- [x] Emails displayable in /emails page
- [x] **40GB test file works end-to-end!**

---

## 💡 Pro Tips

### For Large File Upload Testing

**Use `dd` to create test files:**
```bash
# Create 1GB test file
dd if=/dev/zero of=test-1gb.mbox bs=1m count=1024

# Create 10GB test file
dd if=/dev/zero of=test-10gb.mbox bs=1m count=10240
```

### Monitor Upload in Browser

**F12 → Network tab:**
- See individual chunk uploads
- Verify 4MB chunk sizes
- Monitor upload speed
- Check for failures/retries

### Monitor Processing in Aspire

**Dashboard → Structured Logs → worker:**
```
🔍 Search for: "Processing mailbox"
🔍 Search for: "Parsed"
🔍 Search for: "emails"
```

---

## ⚠️ Potential Challenges

### Challenge 1: Memory Usage
**Problem**: Parsing 40GB in memory  
**Solution**: Stream with MimeKit (never load full file)

### Challenge 2: Upload Timeouts
**Problem**: Long uploads timing out  
**Solution**: Direct to Azure (no ASP.NET timeout), chunked

### Challenge 3: Processing Time
**Problem**: Hours to process 40GB  
**Solution**: Background worker, show progress, user can close browser

### Challenge 4: Network Failures
**Problem**: Upload fails midway  
**Solution**: Azure SDK retries automatically, can resume

---

## 📊 Estimated Timings (100 Mbps Connection)

| File Size | Upload Time | Chunks | Processing Time |
|-----------|-------------|--------|-----------------|
| 1 GB | ~2 minutes | 250 | ~5-10 minutes |
| 5 GB | ~10 minutes | 1,250 | ~30-60 minutes |
| 10 GB | ~20 minutes | 2,500 | ~1-2 hours |
| 40 GB | ~80 minutes | 10,000 | ~4-8 hours |

**Your 40GB file:**
- Upload: ~1.5 hours (100 Mbps)
- Processing: ~6 hours (depends on email count)
- **Total**: ~7.5 hours first run
- **But**: User doesn't have to wait! Background processing!

---

## 🎊 Next Session Checklist

**Before you start coding:**
- [ ] Read Azure Blob Storage upload docs (15 min)
- [ ] Read MimeKit documentation (15 min)
- [ ] Have 40GB test file ready
- [ ] Have good internet connection
- [ ] Set aside 3-4 hours
- [ ] Coffee ready ☕

**During implementation:**
- [ ] Test each component individually
- [ ] Commit after each major step
- [ ] Use Aspire logs for debugging
- [ ] Monitor Azure Storage in dashboard

**After implementation:**
- [ ] Test with small files first
- [ ] Gradually increase to 40GB
- [ ] Monitor memory usage
- [ ] Monitor processing time
- [ ] **CELEBRATE!** 🎉

---

## 💝 Final Notes

**This is the CORE feature** - the reason Evermail exists!

Once this works with your 40GB file:
- ✅ MVP is basically done
- ✅ Users can actually use the product
- ✅ You have proof of concept
- ✅ Ready for beta users

**The authentication template you built?** That was the foundation.  
**This upload system?** That's the product!

---

**You're about to build something awesome!** 🚀

Take this file to your next chat session and let's make Evermail handle those massive email archives! 📧✨

