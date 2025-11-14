// Azure Blob Storage Upload with Progress Tracking
// Uses Azure Storage Blob SDK for chunked uploads

window.azureBlobUpload = {
    currentUpload: null,

    upload: async function (sasUrl, dotNetRef) {
        try {
            const fileInput = document.getElementById('fileInput');
            if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                throw new Error('No file selected');
            }

            const file = fileInput.files[0];
            console.log(`Starting upload: ${file.name} (${file.size} bytes)`);

            // Parse SAS URL to get blob info
            const url = new URL(sasUrl);
            const blobUrl = `${url.origin}${url.pathname}`;
            const sasToken = url.search;

            // Create BlockBlobClient using Azure Storage SDK
            const { BlockBlobClient } = window.AzureStorageBlob;
            const blockBlobClient = new BlockBlobClient(sasUrl);

            // Configuration
            const chunkSize = 4 * 1024 * 1024; // 4MB chunks
            const totalBlocks = Math.ceil(file.size / chunkSize);
            const blockIds = [];

            let uploadedBytes = 0;
            const startTime = Date.now();

            console.log(`File will be uploaded in ${totalBlocks} chunks`);

            // Upload each chunk
            for (let i = 0; i < totalBlocks; i++) {
                const start = i * chunkSize;
                const end = Math.min(start + chunkSize, file.size);
                const chunk = file.slice(start, end);

                // Generate unique block ID (base64 encoded, must be same length for all blocks)
                const blockId = btoa(`block-${String(i).padStart(10, '0')}`);
                blockIds.push(blockId);

                console.log(`Uploading block ${i + 1}/${totalBlocks} (${chunk.size} bytes)`);

                // Upload block
                await blockBlobClient.stageBlock(blockId, chunk, chunk.size);

                uploadedBytes += chunk.size;
                const progress = Math.round((uploadedBytes / file.size) * 100);

                // Calculate speed
                const elapsedSeconds = (Date.now() - startTime) / 1000;
                const speedMBps = (uploadedBytes / (1024 * 1024)) / elapsedSeconds;

                // Report progress to Blazor
                dotNetRef.invokeMethodAsync('UpdateProgress', progress, uploadedBytes, speedMBps);

                console.log(`Progress: ${progress}%, Speed: ${speedMBps.toFixed(2)} MB/s`);
            }

            // Commit all blocks to finalize the blob
            console.log('Committing blocks...');
            await blockBlobClient.commitBlockList(blockIds);

            console.log('Upload complete!');

            // Final progress update
            dotNetRef.invokeMethodAsync('UpdateProgress', 100, file.size, 0);

        } catch (error) {
            console.error('Upload error:', error);
            throw error;
        }
    },

    cancel: function () {
        // TODO: Implement cancellation
        console.log('Upload cancellation requested');
    }
};

// Log when script is loaded
console.log('Azure Blob Upload script loaded');

