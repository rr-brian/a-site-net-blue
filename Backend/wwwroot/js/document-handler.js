/**
 * Document handling functionality for the chat application
 */

import { addMessage } from './ui.js';
import { clearDocumentContext, updateUIDocumentState } from './api.js';

// Initialize document context state
let documentContextActive = false;
let currentFile = null;
let currentFileName = null;

// Function to handle file selection
function handleFileSelection(file, uploadButton, fileInput, chatMessages, userInput) {
    if (!file) return;
    
    // Store the file for the next message
    currentFile = file;
    currentFileName = file.name;
    
    // Store file name in localStorage for persistence
    localStorage.setItem('lastDocumentName', file.name);
    
    // Create file upload indicator
    createFileUploadIndicator(file, chatMessages, uploadButton, fileInput, userInput);
    
    // Update UI to show a file is attached
    uploadButton.classList.add('active');
    uploadButton.setAttribute('title', `File attached: ${file.name}`);
    
    // Add file indicator badge to the upload button
    if (!uploadButton.querySelector('.file-indicator-badge')) {
        const badge = document.createElement('div');
        badge.classList.add('file-indicator-badge');
        badge.textContent = '1';
        uploadButton.appendChild(badge);
    }
    
    // Add 'with-file' class to the chat input container
    const chatInputContainer = userInput.closest('.chat-input-container');
    if (chatInputContainer) {
        chatInputContainer.classList.add('with-file');
    }
    
    // Update document status indicator
    updateUIDocumentState();
    
    // Focus on the message input
    userInput.focus();
}

// Function to create a visual file upload indicator
function createFileUploadIndicator(file, chatMessages, uploadButton, fileInput, userInput) {
    // Create the file upload indicator container
    const fileIndicator = document.createElement('div');
    fileIndicator.classList.add('file-upload-indicator');
    fileIndicator.id = 'fileUploadIndicator';
    
    // Create file icon based on file type
    const fileIcon = document.createElement('div');
    fileIcon.classList.add('file-icon');
    
    // Determine file type and set appropriate icon
    let iconClass = 'fa-file';
    let fileTypeClass = '';
    
    if (file.name.endsWith('.pdf')) {
        iconClass = 'fa-file-pdf';
        fileTypeClass = 'pdf';
    } else if (file.name.endsWith('.docx') || file.name.endsWith('.doc')) {
        iconClass = 'fa-file-word';
        fileTypeClass = 'docx';
    } else if (file.name.endsWith('.xlsx') || file.name.endsWith('.xls')) {
        iconClass = 'fa-file-excel';
        fileTypeClass = 'xlsx';
    }
    
    // Only add the fileTypeClass if it's not empty
    if (fileTypeClass) {
        fileIcon.classList.add(fileTypeClass);
    }
    
    fileIcon.innerHTML = `<i class="fas ${iconClass}"></i>`;
    
    // Create file info section
    const fileInfo = document.createElement('div');
    fileInfo.classList.add('file-info');
    
    // Add file name
    const fileName = document.createElement('div');
    fileName.classList.add('file-name');
    fileName.textContent = file.name;
    
    // Add file metadata
    const fileMeta = document.createElement('div');
    fileMeta.classList.add('file-meta');
    
    // Add file size
    const fileSize = document.createElement('span');
    fileSize.classList.add('file-size');
    fileSize.textContent = formatFileSize(file.size);
    
    // Add file type
    const fileType = document.createElement('span');
    fileType.classList.add('file-type');
    fileType.textContent = file.name.split('.').pop();
    
    // Add status indicator
    const fileStatus = document.createElement('span');
    fileStatus.classList.add('file-status');
    
    // Assemble file metadata
    fileMeta.appendChild(fileStatus);
    fileMeta.appendChild(fileSize);
    fileMeta.appendChild(fileType);
    
    // Assemble file info
    fileInfo.appendChild(fileName);
    fileInfo.appendChild(fileMeta);
    
    // Create remove button
    const removeButton = document.createElement('button');
    removeButton.classList.add('remove-file');
    removeButton.innerHTML = '<i class="fas fa-times"></i>';
    removeButton.setAttribute('title', 'Remove file');
    removeButton.addEventListener('click', () => {
        resetFileAttachment(uploadButton, fileInput);
        fileIndicator.remove();
        
        // Remove with-file class from chat input container
        const chatInputContainer = userInput.closest('.chat-input-container');
        if (chatInputContainer) {
            chatInputContainer.classList.remove('with-file');
        }
        
        // Remove badge from upload button
        const badge = uploadButton.querySelector('.file-indicator-badge');
        if (badge) {
            badge.remove();
        }
    });
    
    // Assemble the file indicator
    fileIndicator.appendChild(fileIcon);
    fileIndicator.appendChild(fileInfo);
    fileIndicator.appendChild(removeButton);
    
    // Add to chat messages container at the end
    chatMessages.appendChild(fileIndicator);
    
    // Scroll to make the indicator visible
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

// Helper function to format file size
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// Function to reset file attachment
function resetFileAttachment(uploadButton, fileInput) {
    currentFile = null;
    currentFileName = null;
    
    // Clear file name from localStorage but don't clear documentInContext
    // as that should be managed by server interactions
    localStorage.removeItem('lastDocumentName');
    
    uploadButton.classList.remove('active');
    uploadButton.setAttribute('title', 'Upload Document');
    fileInput.value = '';
    
    // Remove file indicator if it exists
    const fileIndicator = document.getElementById('fileUploadIndicator');
    if (fileIndicator) {
        fileIndicator.remove();
    }
    
    // Remove badge from upload button
    const badge = uploadButton.querySelector('.file-indicator-badge');
    if (badge) {
        badge.remove();
    }
    
    // Remove with-file class from chat input container
    const chatInputContainer = document.querySelector('.chat-input-container');
    if (chatInputContainer) {
        chatInputContainer.classList.remove('with-file');
    }
    
    // Update document status indicator UI
    updateUIDocumentState();
}

// Function to handle clearing document context
async function handleClearDocumentContext(clearDocumentButton, chatMessages, userInput) {
    try {
        // Call the clear document context API
        await clearDocumentContext();
        
        // Update UI to show document context is cleared
        documentContextActive = false;
        
        // Update document status UI
        // Note: This is now handled by clearDocumentContext() API function
        
        // Add system message to chat
        addMessage("Document context has been cleared. I'm no longer using any document for context.", 'system', chatMessages, userInput);
        
    } catch (error) {
        console.error('Error:', error);
        addMessage("Sorry, there was an error clearing the document context.", 'system', chatMessages, userInput);
    }
}

// Function to update document context UI
// Note: This functionality has been moved to api.js updateUIDocumentState

// Function to set document context active
function setDocumentContextActive(isActive) {
    documentContextActive = isActive;
    
    // Update localStorage
    localStorage.setItem('documentInContext', isActive ? 'true' : 'false');
    
    // Update the UI
    updateUIDocumentState();
}

// Export functions and state
export { 
    handleFileSelection, 
    resetFileAttachment, 
    handleClearDocumentContext, 
    setDocumentContextActive,
    createFileUploadIndicator,
    formatFileSize,
    currentFile,
    currentFileName
};
