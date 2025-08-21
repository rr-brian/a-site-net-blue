let uploadedFiles = [];
let currentDocuments = [];

document.addEventListener('DOMContentLoaded', function() {
    const uploadButton = document.getElementById('uploadButton');
    const uploadModal = document.getElementById('uploadModal');
    const closeModal = document.getElementById('closeModal');
    const cancelUpload = document.getElementById('cancelUpload');
    const uploadArea = document.getElementById('uploadArea');
    const fileInput = document.getElementById('fileInput');
    const processFiles = document.getElementById('processFiles');
    const uploadedFilesContainer = document.getElementById('uploadedFiles');
    const uploadIndicator = document.getElementById('uploadIndicator');
    const documentsIndicator = document.getElementById('documentsIndicator');
    const documentsList = document.getElementById('documentsList');
    const clearDocuments = document.getElementById('clearDocuments');

    // Show upload modal
    uploadButton.addEventListener('click', function() {
        uploadModal.style.display = 'flex';
        loadUserDocuments();
    });

    // Clear all documents
    if (clearDocuments) {
        clearDocuments.addEventListener('click', clearAllDocuments);
    }

    // Load documents on page load
    loadUserDocuments();

    // Close modal
    closeModal.addEventListener('click', closeUploadModal);
    cancelUpload.addEventListener('click', closeUploadModal);

    // Close modal when clicking outside
    uploadModal.addEventListener('click', function(e) {
        if (e.target === uploadModal) {
            closeUploadModal();
        }
    });

    // Upload area click to trigger file input
    uploadArea.addEventListener('click', function() {
        fileInput.click();
    });

    // Drag and drop functionality
    uploadArea.addEventListener('dragover', function(e) {
        e.preventDefault();
        uploadArea.classList.add('dragover');
    });

    uploadArea.addEventListener('dragleave', function(e) {
        e.preventDefault();
        uploadArea.classList.remove('dragover');
    });

    uploadArea.addEventListener('drop', function(e) {
        e.preventDefault();
        uploadArea.classList.remove('dragover');
        handleFiles(e.dataTransfer.files);
    });

    // File input change
    fileInput.addEventListener('change', function(e) {
        handleFiles(e.target.files);
    });

    // Process files button
    processFiles.addEventListener('click', uploadFiles);

    function closeUploadModal() {
        uploadModal.style.display = 'none';
        uploadedFiles = [];
        updateUploadedFilesList();
        fileInput.value = '';
        processFiles.disabled = true;
    }

    function handleFiles(files) {
        const allowedTypes = ['.pdf', '.txt', '.doc', '.docx'];
        
        Array.from(files).forEach(file => {
            const fileExtension = '.' + file.name.split('.').pop().toLowerCase();
            
            if (allowedTypes.includes(fileExtension)) {
                if (!uploadedFiles.find(f => f.name === file.name)) {
                    uploadedFiles.push(file);
                }
            } else {
                showError(`File type not supported: ${file.name}`);
            }
        });

        updateUploadedFilesList();
        processFiles.disabled = uploadedFiles.length === 0;
    }

    function updateUploadedFilesList() {
        uploadedFilesContainer.innerHTML = '';

        if (uploadedFiles.length > 0) {
            const title = document.createElement('h4');
            title.textContent = 'Files to upload:';
            title.style.marginBottom = '1rem';
            title.style.color = '#165540';
            uploadedFilesContainer.appendChild(title);

            uploadedFiles.forEach((file, index) => {
                const fileItem = document.createElement('div');
                fileItem.className = 'file-item';
                
                fileItem.innerHTML = `
                    <div>
                        <div class="file-name">${file.name}</div>
                        <div class="file-size">${formatFileSize(file.size)}</div>
                    </div>
                    <button class="remove-file" onclick="removeFile(${index})">Remove</button>
                `;
                
                uploadedFilesContainer.appendChild(fileItem);
            });
        }

        // Show existing documents
        if (currentDocuments.length > 0) {
            const existingTitle = document.createElement('h4');
            existingTitle.textContent = 'Your Documents:';
            existingTitle.style.marginTop = '2rem';
            existingTitle.style.marginBottom = '1rem';
            existingTitle.style.color = '#165540';
            uploadedFilesContainer.appendChild(existingTitle);

            currentDocuments.forEach(doc => {
                const docItem = document.createElement('div');
                docItem.className = 'file-item';
                docItem.style.background = '#e3f2fd';
                
                docItem.innerHTML = `
                    <div>
                        <div class="file-name">${doc.fileName}</div>
                        <div class="file-size">${formatFileSize(doc.fileSizeBytes)} • Uploaded ${new Date(doc.uploadedAt).toLocaleDateString()}</div>
                    </div>
                    <button class="remove-file" onclick="deleteDocument('${doc.id}')">Delete</button>
                `;
                
                uploadedFilesContainer.appendChild(docItem);
            });
        }
    }

    window.removeFile = function(index) {
        uploadedFiles.splice(index, 1);
        updateUploadedFilesList();
        processFiles.disabled = uploadedFiles.length === 0;
    };

    // Define deleteDocument function for modal usage
    window.deleteDocument = async function(documentId) {
        try {
            const response = await fetch(`/api/document/${documentId}`, {
                method: 'DELETE'
            });

            if (response.ok) {
                await loadUserDocuments();
                showSuccess('Document deleted successfully');
            } else {
                showError('Failed to delete document');
            }
        } catch (error) {
            console.error('Error deleting document:', error);
            showError('Error deleting document');
        }
    };

    async function uploadFiles() {
        if (uploadedFiles.length === 0) return;

        processFiles.disabled = true;
        processFiles.textContent = 'Processing...';

        const formData = new FormData();
        uploadedFiles.forEach(file => {
            formData.append('files', file);
        });

        try {
            const response = await fetch('/api/document/upload-multiple', {
                method: 'POST',
                body: formData
            });

            if (response.ok) {
                const result = await response.json();
                const successCount = result.results.filter(r => r.success).length;
                const totalCount = result.results.length;

                // Close modal immediately on success
                closeUploadModal();
                
                // Refresh document display in header
                await loadUserDocuments();
                
                // Refresh chat document context
                if (window.refreshChatDocuments) {
                    window.refreshChatDocuments();
                }
                
                // Show success message after modal is closed
                setTimeout(() => {
                    showSuccess(`${successCount}/${totalCount} files processed successfully`);
                }, 100);
            } else {
                showError('Upload failed');
            }
        } catch (error) {
            console.error('Upload error:', error);
            showError('Upload failed');
        } finally {
            processFiles.disabled = false;
            processFiles.textContent = 'Process Files';
        }
    }

    async function loadUserDocuments() {
        try {
            const response = await fetch('/api/document/list');
            if (response.ok) {
                currentDocuments = await response.json();
                updateUploadedFilesList();
                updateDocumentsIndicator();
            }
        } catch (error) {
            console.error('Error loading documents:', error);
        }
    }

    function updateDocumentsIndicator() {
        // Check if elements exist before trying to use them
        if (!uploadIndicator || !documentsIndicator || !documentsList) {
            console.warn('Document indicator elements not found');
            return;
        }

        if (currentDocuments.length > 0) {
            uploadIndicator.style.display = 'flex';
            uploadIndicator.textContent = currentDocuments.length;
            documentsIndicator.style.display = 'block';
            
            documentsList.innerHTML = '';
            currentDocuments.forEach(doc => {
                const docItem = document.createElement('div');
                docItem.className = 'document-item';
                
                docItem.innerHTML = `
                    <div>
                        <div class="document-name">${doc.fileName}</div>
                        <div class="document-size">${formatFileSize(doc.fileSizeBytes)}</div>
                    </div>
                    <button class="remove-doc-btn" onclick="removeDocumentFromIndicator('${doc.id}')">×</button>
                `;
                
                documentsList.appendChild(docItem);
            });
        } else {
            uploadIndicator.style.display = 'none';
            documentsIndicator.style.display = 'none';
        }
    }

    async function clearAllDocuments() {
        if (confirm('Are you sure you want to delete all documents?')) {
            try {
                const deletePromises = currentDocuments.map(doc => 
                    fetch(`/api/document/${doc.id}`, { method: 'DELETE' })
                );
                
                await Promise.all(deletePromises);
                await loadUserDocuments();
                showSuccess('All documents deleted successfully');
            } catch (error) {
                console.error('Error clearing documents:', error);
                showError('Failed to delete documents');
            }
        }
    }

    window.removeDocumentFromIndicator = async function(documentId) {
        try {
            const response = await fetch(`/api/document/${documentId}`, {
                method: 'DELETE'
            });

            if (response.ok) {
                await loadUserDocuments();
                showSuccess('Document removed');
            } else {
                showError('Failed to remove document');
            }
        } catch (error) {
            console.error('Error removing document:', error);
            showError('Error removing document');
        }
    };

    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    function showSuccess(message) {
        console.log('Success:', message);
        // Create a temporary notification element
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #28a745;
            color: white;
            padding: 1rem 1.5rem;
            border-radius: 4px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.2);
            z-index: 10001;
            font-family: inherit;
        `;
        notification.textContent = message;
        document.body.appendChild(notification);
        
        // Remove after 3 seconds
        setTimeout(() => {
            document.body.removeChild(notification);
        }, 3000);
    }

    function showError(message) {
        console.error('Error:', message);
        // Create a temporary notification element
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #dc3545;
            color: white;
            padding: 1rem 1.5rem;
            border-radius: 4px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.2);
            z-index: 10001;
            font-family: inherit;
        `;
        notification.textContent = message;
        document.body.appendChild(notification);
        
        // Remove after 5 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                document.body.removeChild(notification);
            }
        }, 5000);
    }

    // Clear All Documents button functionality
    const clearDocumentsBtn = document.getElementById('clearDocuments');
    if (clearDocumentsBtn) {
        clearDocumentsBtn.addEventListener('click', async function() {
            if (!confirm('Are you sure you want to clear all documents? This action cannot be undone.')) {
                return;
            }

            try {
                // Get current documents first
                const response = await fetch('/api/document/list');
                if (response.ok) {
                    const documents = await response.json();
                    
                    // Delete each document
                    const deletePromises = documents.map(doc => 
                        fetch(`/api/document/${doc.id}`, { method: 'DELETE' })
                    );
                    
                    await Promise.all(deletePromises);
                    
                    // Update UI
                    currentDocuments = [];
                    updateDocumentsIndicator();
                    showNotification('All documents cleared successfully', 'success');
                    
                    // Refresh chat context
                    if (typeof window.refreshChatDocuments === 'function') {
                        window.refreshChatDocuments();
                    }
                }
            } catch (error) {
                console.error('Error clearing documents:', error);
                showNotification('Failed to clear documents', 'error');
            }
        });
    }

    // Clear documents on page load (refresh)
    window.addEventListener('load', function() {
        clearAllDocumentsOnRefresh();
    });

    async function clearAllDocumentsOnRefresh() {
        try {
            // Get current documents
            const response = await fetch('/api/document/list');
            if (response.ok) {
                const documents = await response.json();
                
                if (documents.length > 0) {
                    // Delete each document silently on refresh
                    const deletePromises = documents.map(doc => 
                        fetch(`/api/document/${doc.id}`, { method: 'DELETE' })
                    );
                    
                    await Promise.all(deletePromises);
                    console.log(`Cleared ${documents.length} documents on page refresh`);
                    
                    // Update UI
                    currentDocuments = [];
                    updateDocumentsIndicator();
                }
            }
        } catch (error) {
            console.error('Error clearing documents on refresh:', error);
        }
    }
});