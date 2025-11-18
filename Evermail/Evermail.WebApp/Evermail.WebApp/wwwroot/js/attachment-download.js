// Attachment download helper - downloads attachments with authentication token
// Files are proxied through our API (SAS URLs never exposed to client)
window.attachmentDownload = {
    download: async function (attachmentUrl, token) {
        try {
            // Fetch the file directly from our API endpoint
            // The API proxies the file from Azure Storage, so SAS URLs are never exposed
            const response = await fetch(attachmentUrl, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                if (response.status === 401) {
                    throw new Error('Authentication required. Please log in again.');
                }
                const errorText = await response.text();
                throw new Error(`Failed to download attachment: ${response.status} ${response.statusText}. ${errorText}`);
            }

            // Get filename from Content-Disposition header or use default
            const contentDisposition = response.headers.get('Content-Disposition');
            let fileName = 'attachment';
            if (contentDisposition) {
                const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                if (filenameMatch && filenameMatch[1]) {
                    fileName = filenameMatch[1].replace(/['"]/g, '');
                }
            }
            
            // Create a blob from the response
            const blob = await response.blob();
            
            // Create a blob URL
            const blobUrl = window.URL.createObjectURL(blob);
            
            // Create a temporary anchor element and trigger download
            // This avoids popup blockers and prevents navigation
            const link = document.createElement('a');
            link.href = blobUrl;
            link.download = fileName;
            link.style.display = 'none';
            document.body.appendChild(link);
            
            // Trigger the download
            link.click();
            
            // Clean up after a short delay
            setTimeout(() => {
                document.body.removeChild(link);
                window.URL.revokeObjectURL(blobUrl);
            }, 100);
        } catch (error) {
            console.error('Download error:', error);
            alert(`Failed to download attachment: ${error.message}`);
        }
    },
    
    shouldPreventNavigation: function(event) {
        // Check if the click target is an attachment button
        const target = event.target || event.srcElement;
        const clickedButton = target.closest('.attachment-download-btn');
        return clickedButton !== null;
    },
    
    preventAnchorNavigation: function(buttonElement) {
        // Find the parent anchor and prevent its navigation
        const parentAnchor = buttonElement.closest('a.list-group-item');
        if (parentAnchor) {
            // Add a one-time click listener to prevent navigation
            const preventNav = function(e) {
                e.preventDefault();
                e.stopPropagation();
                parentAnchor.removeEventListener('click', preventNav, true);
            };
            parentAnchor.addEventListener('click', preventNav, true);
        }
    },
    
    setupAttachmentButtonListeners: function() {
        // Remove any existing listeners first to avoid duplicates
        const buttons = document.querySelectorAll('.attachment-download-btn');
        buttons.forEach(button => {
            const parentAnchor = button.closest('a.list-group-item');
            if (parentAnchor) {
                // Remove any existing listener
                if (button._preventNavHandler) {
                    parentAnchor.removeEventListener('click', button._preventNavHandler, true);
                }
                
                // Add a click listener to prevent navigation when button is clicked
                button._preventNavHandler = function(e) {
                    // Check if the click originated from the button or its children
                    if (button.contains(e.target)) {
                        e.preventDefault();
                        e.stopPropagation();
                    }
                };
                // Use capture phase to catch before Blazor handles it
                parentAnchor.addEventListener('click', button._preventNavHandler, true);
            }
        });
    }
};

console.log('Attachment download helper loaded');

