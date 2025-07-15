/**
 * API communication functions for the chat application
 */

// Track if we have an active document in context
// Use localStorage to persist between page refreshes
let documentInContext = localStorage.getItem('documentInContext') === 'true' || false;
let lastDocumentName = localStorage.getItem('lastDocumentName') || null;

// Generate a persistent client ID that won't change between page refreshes
let clientSessionId = localStorage.getItem('clientSessionId');
if (!clientSessionId) {
    // Create a unique identifier for this browser session
    clientSessionId = 'client-' + Date.now() + '-' + Math.random().toString(36).substring(2, 15);
    localStorage.setItem('clientSessionId', clientSessionId);
}

// Log the initial document context state
console.log(`Initial document context state: ${documentInContext ? 'ACTIVE' : 'INACTIVE'} (from localStorage)`);
console.log(`Last document name: ${lastDocumentName || 'None'}`);
console.log(`Client session ID: ${clientSessionId}`);

// Import UI functions
import { updateDocumentStatusIndicator } from './ui.js';

// Ensure UI is updated to match the current document context state
function updateUIDocumentState() {
    // Update clear document button if it exists
    const clearDocumentButton = document.getElementById('clearDocumentButton');
    if (clearDocumentButton) {
        if (documentInContext) {
            clearDocumentButton.classList.add('active');
            clearDocumentButton.setAttribute('title', 'Clear Document Context (Active)');
        } else {
            clearDocumentButton.classList.remove('active');
            clearDocumentButton.setAttribute('title', 'Clear Document Context (None)');
        }
    }
    
    // Update document status indicator
    updateDocumentStatusIndicator(documentInContext ? lastDocumentName : null);
}


// Function to call the chat API
async function callChatAPI(message) {
    // Double-check localStorage before making the request
    documentInContext = localStorage.getItem('documentInContext') === 'true' || false;
    
    console.log(`Sending chat request with documentInContext: ${documentInContext}`);
    
    // Ensure we always set the session cookie
    const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        credentials: 'same-origin', // Important for session cookies
        body: JSON.stringify({ 
            message,
            // Always maintain document context if we have a document
            MaintainDocumentContext: documentInContext,  // FIXED: Capitalized to match C# property name
            ClientSessionId: clientSessionId  // Add our client-side session ID to help with persistence
        })
    });
    
    if (!response.ok) {
        console.error('Error in chat API call:', response.status, response.statusText);
        throw new Error('Network response was not ok');
    }
    
    const data = await response.json();
    
    // Debug the entire response data
    console.log('Chat API response data:', data);
    
    // If the response includes information about document context, update our state
    if (data.documentInContext !== undefined) {
        documentInContext = data.documentInContext;
        localStorage.setItem('documentInContext', documentInContext ? 'true' : 'false');
        
        // If we have document info, save the filename
        if (data.documentInfo && data.documentInfo.fileName) {
            lastDocumentName = data.documentInfo.fileName;
            localStorage.setItem('lastDocumentName', lastDocumentName);
            console.log(`Updated document name in context: ${lastDocumentName}`);
        }
    }
    else {
        console.warn('Response missing documentInContext flag - keeping current state', documentInContext);
    }
    
    // ALWAYS update the UI regardless of whether we got document context info back
    // This ensures the document status remains visible across multiple questions
    updateUIDocumentState();
    
    // Debug current state after processing
    console.log(`Current document state after API response: documentInContext=${documentInContext}, lastDocumentName=${lastDocumentName}`);

    
    return data.response;
}

// Function to call the chat with file API
async function callChatWithFileAPI(message, file) {
    // Create form data with file, message, and client session ID
    const formData = new FormData();
    formData.append('file', file);
    formData.append('message', message);
    formData.append('clientSessionId', clientSessionId);
    
    console.log(`Sending file upload with clientSessionId: ${clientSessionId}`);
    
    // Try the new endpoint first (preferred)
    try {
        console.log('Attempting to use new endpoint: /api/document-chat/with-file');
        const response = await fetch('/api/document-chat/with-file', {
            method: 'POST',
            body: formData
        });
        
        if (response.ok) {
            console.log('Successfully used new endpoint');
            const data = await response.json();
            return handleDocumentResponse(data, file);
        } else {
            console.warn('New endpoint failed with status:', response.status);
            // Let it fall through to the fallback
        }
    } catch (error) {
        console.warn('Error using new endpoint:', error);
        // Continue to fallback
    }
    
    // Fallback to old endpoint if new one failed
    try {
        console.log('Falling back to legacy endpoint: /api/chat/with-file');
        const legacyResponse = await fetch('/api/chat/with-file', {
            method: 'POST',
            body: formData
        });
        
        if (!legacyResponse.ok) {
            console.error('Both endpoints failed. Legacy status:', legacyResponse.status);
            throw new Error('Error processing file with message');
        }
        
        console.log('Successfully used legacy endpoint');
        const data = await legacyResponse.json();
        return handleDocumentResponse(data, file);
    } catch (error) {
        console.error('All endpoints failed:', error);
        throw new Error('Error processing file with message');
    }
}

// Helper function to handle document response processing
function handleDocumentResponse(data, file) {
    // Set flag indicating we now have a document in context
    documentInContext = true;
    // Persist this state in localStorage
    localStorage.setItem('documentInContext', 'true');
    
    // Store the document filename if available
    if (data.documentStored && file) {
        lastDocumentName = file.name;
        localStorage.setItem('lastDocumentName', lastDocumentName);
        console.log(`Saved document name to context: ${lastDocumentName}`);
    }
    
    // Update the UI
    updateUIDocumentState();
    
    return data.response;
}

// Function to clear document context
async function clearDocumentContext() {
    const response = await fetch('/api/document-chat/clear-context', {
        method: 'POST',
        credentials: 'same-origin' // Important for session cookies
    });
    
    if (!response.ok) {
        console.error('Error clearing document context:', response.status, response.statusText);
        throw new Error('Error clearing document context');
    }
    
    // Reset document context flag and document name
    documentInContext = false;
    lastDocumentName = null;
    localStorage.setItem('documentInContext', 'false');
    localStorage.removeItem('lastDocumentName');
    
    // Update the UI to reflect no document in context
    updateUIDocumentState();
    console.log('Document context cleared');
    
    const data = await response.json();
    return data;
}

// Function to set document context state from outside this module
function setDocumentContext(active) {
    documentInContext = active;
}

// Export chat-related API functions
export { 
    callChatAPI, 
    callChatWithFileAPI, 
    clearDocumentContext,
    documentInContext, 
    setDocumentContext,
    updateUIDocumentState
};
