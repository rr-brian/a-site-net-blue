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

// Function to create a visual file upload indicator in the header
function createFileUploadIndicator(file, chatMessages, uploadButton, fileInput, userInput) {
    // Update the document status indicator in the header instead of adding to chat
    const documentStatus = document.getElementById('document-status');
    if (documentStatus) {
        documentStatus.style.display = 'inline-block';
        documentStatus.innerHTML = `<i class="fas fa-file-alt"></i> ${file.name}`;
        documentStatus.setAttribute('title', `Document loaded: ${file.name} (${formatFileSize(file.size)})`);
    }
    
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
    
    // Hide document status indicator
    const documentStatus = document.getElementById('document-status');
    if (documentStatus) {
        documentStatus.style.display = 'none';
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
