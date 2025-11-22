// Azure Blob Storage Upload using native Fetch API (no SDK needed)
// Uploads files in 4MB chunks directly to Azure Blob Storage

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

            // Configuration
            const chunkSize = 4 * 1024 * 1024; // 4MB chunks
            const totalBlocks = Math.ceil(file.size / chunkSize);
            const blockIds = [];

            let uploadedBytes = 0;
            const startTime = Date.now();

            console.log(`File will be uploaded in ${totalBlocks} chunks`);

            // Upload each chunk using PUT block API
            for (let i = 0; i < totalBlocks; i++) {
                const start = i * chunkSize;
                const end = Math.min(start + chunkSize, file.size);
                const chunk = file.slice(start, end);

                // Generate unique block ID (base64 encoded)
                const blockId = btoa(`block-${String(i).padStart(10, '0')}`);
                blockIds.push(blockId);

                console.log(`Uploading block ${i + 1}/${totalBlocks} (${chunk.size} bytes)`);

                // Upload block using Azure Blob REST API
                const blockUrl = `${sasUrl}&comp=block&blockid=${encodeURIComponent(blockId)}`;
                const uploadResponse = await fetch(blockUrl, {
                    method: 'PUT',
                    body: chunk,
                    headers: {
                        'x-ms-blob-type': 'BlockBlob',
                        'Content-Length': chunk.size.toString()
                    }
                });

                if (!uploadResponse.ok) {
                    throw new Error(`Block upload failed: ${uploadResponse.status} ${uploadResponse.statusText}`);
                }

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
            
            const commitUrl = `${sasUrl}&comp=blocklist`;
            const blockListXml = `<?xml version="1.0" encoding="utf-8"?><BlockList>${blockIds.map(id => `<Latest>${id}</Latest>`).join('')}</BlockList>`;
            
            const commitResponse = await fetch(commitUrl, {
                method: 'PUT',
                body: blockListXml,
                headers: {
                    'Content-Type': 'application/xml',
                    'Content-Length': blockListXml.length.toString()
                }
            });

            if (!commitResponse.ok) {
                throw new Error(`Commit failed: ${commitResponse.status} ${commitResponse.statusText}`);
            }

            console.log('Upload complete!');

            // Final progress update
            dotNetRef.invokeMethodAsync('UpdateProgress', 100, file.size, 0);

        } catch (error) {
            console.error('Upload error:', error);
            if (dotNetRef && typeof dotNetRef.invokeMethodAsync === 'function') {
                try {
                    const friendly = window.azureBlobUpload.describeError(error);
                    await dotNetRef.invokeMethodAsync(
                        'HandleUploadError',
                        friendly.title,
                        friendly.message,
                        error?.message || '');
                } catch (callbackError) {
                    console.warn('Failed to notify Blazor about the upload error', callbackError);
                }
            }
            throw error;
        }
    },

    cancel: function () {
        // TODO: Implement cancellation
        console.log('Upload cancellation requested');
    },

    describeError: function (error) {
        const baseMessage = 'The browser could not upload the file.';
        if (error instanceof TypeError && error.message && error.message.toLowerCase().includes('failed to fetch')) {
            return {
                title: 'We could not read the file',
                message: 'Your operating system reports that the file is still open in another application (for example Outlook, OneDrive sync, or antivirus scanning). Please close any apps using the file and try again.'
            };
        }

        return {
            title: 'Upload failed',
            message: baseMessage + ' Please check your internet connection and try again.'
        };
    }
};

// Log when script is loaded
console.log('Azure Blob Upload script loaded (using native Fetch API)');
