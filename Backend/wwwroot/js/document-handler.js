/**
 * Document handling functionality for the chat application
 */

import { addMessage } from './ui.js';
import { clearDocumentContext } from './api.js';

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
    
    // Show notification that file is ready to be sent
    addMessage(`File "${file.name}" is ready to be sent with your next message.`, 'system', chatMessages, userInput);
    
    // Update UI to show a file is attached
    uploadButton.classList.add('active');
    uploadButton.setAttribute('title', `File attached: ${file.name}`);
    
    // Focus on the message input
    userInput.focus();
}

// Function to reset file attachment
function resetFileAttachment(uploadButton, fileInput) {
    currentFile = null;
    currentFileName = null;
    uploadButton.classList.remove('active');
    uploadButton.setAttribute('title', 'Upload Document');
    fileInput.value = '';
}

// Function to handle clearing document context
async function handleClearDocumentContext(clearDocumentButton, chatMessages, userInput) {
    try {
        // Call the clear document context API
        await clearDocumentContext();
        
        // Update UI to show document context is cleared
        documentContextActive = false;
        updateDocumentContextUI(clearDocumentButton);
        
        // Add system message to chat
        addMessage("Document context has been cleared. I'm no longer using any document for context.", 'system', chatMessages, userInput);
        
    } catch (error) {
        console.error('Error:', error);
        addMessage("Sorry, there was an error clearing the document context.", 'system', chatMessages, userInput);
    }
}

// Function to update document context UI
function updateDocumentContextUI(clearDocumentButton) {
    if (documentContextActive) {
        clearDocumentButton.classList.add('active');
        clearDocumentButton.setAttribute('title', 'Clear Document Context (Active)');
    } else {
        clearDocumentButton.classList.remove('active');
        clearDocumentButton.setAttribute('title', 'Clear Document Context (None)');
    }
}

// Function to set document context active
function setDocumentContextActive(isActive) {
    documentContextActive = isActive;
}

// Export functions and state
export { 
    handleFileSelection, 
    resetFileAttachment, 
    handleClearDocumentContext, 
    updateDocumentContextUI, 
    setDocumentContextActive,
    currentFile,
    currentFileName
};
