/**
 * API communication functions for the chat application
 */

// Track if we have an active document in context
// Use localStorage to persist between page refreshes
let documentInContext = localStorage.getItem('documentInContext') === 'true' || false;
let lastDocumentName = localStorage.getItem('lastDocumentName') || null;

// Track conversation history to maintain context
let conversationHistory = [];

// Load conversation history from localStorage if available
try {
    const savedHistory = localStorage.getItem('conversationHistory');
    if (savedHistory) {
        conversationHistory = JSON.parse(savedHistory);
        console.log(`Loaded ${conversationHistory.length} messages from conversation history`);
    }
} catch (error) {
    console.warn('Failed to load conversation history from localStorage:', error);
    conversationHistory = [];
}

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

/**
 * Helper function to get authentication headers for API calls
 */
async function getAuthHeaders() {
    const headers = {
        'Content-Type': 'application/json'
    };

    // Check if authentication is available and get access token
    if (window.authModule && window.authModule.isAuthenticated()) {
        try {
            const accessToken = await window.authModule.getAccessToken();
            if (accessToken) {
                headers['Authorization'] = `Bearer ${accessToken}`;
            } else {
                console.log('No access token available - user may need to sign in');
                // If no token and user appears authenticated, show error
                if (window.authModule.showAuthError) {
                    window.authModule.showAuthError('Failed to get access token. Please sign in again.');
                    window.authModule.showLoginUI();
                }
                throw new Error('Authentication required');
            }
        } catch (error) {
            console.error('Failed to get access token:', error);
            // Show user-friendly error message
            if (window.authModule.showAuthError) {
                window.authModule.showAuthError('Authentication session expired. Please sign in again.');
                window.authModule.showLoginUI();
            }
            throw new Error('Authentication required');
        }
    }

    return headers;
}

/**
 * Helper function to get authentication headers for file upload (multipart/form-data)
 */
async function getAuthHeadersForFileUpload() {
    const headers = {};

    // Check if authentication is available and get access token
    if (window.authModule && window.authModule.isAuthenticated()) {
        try {
            const accessToken = await window.authModule.getAccessToken();
            if (accessToken) {
                headers['Authorization'] = `Bearer ${accessToken}`;
            }
        } catch (error) {
            console.error('Failed to get access token for file upload:', error);
            // Don't throw here - let the API call proceed and handle auth errors
        }
    }

    return headers;
}

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
    
    // Get authentication headers
    const headers = await getAuthHeaders();
    
    // Ensure we always set the session cookie
    const response = await fetch('/api/chat', {
        method: 'POST',
        headers: headers,
        credentials: 'same-origin', // Important for session cookies
        body: JSON.stringify({ 
            message,
            // Always maintain document context if we have a document
            MaintainDocumentContext: documentInContext,  // FIXED: Capitalized to match C# property name
            ClientSessionId: clientSessionId,  // Add our client-side session ID to help with persistence
            ConversationHistory: conversationHistory  // Include conversation history for context
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

    // Save conversation history
    if (data.response) {
        // Add user message to history
        conversationHistory.push({
            role: 'user',
            content: message
        });
        
        // Add assistant response to history
        conversationHistory.push({
            role: 'assistant',
            content: data.response
        });
        
        // Keep only the last 20 messages (10 exchanges) to prevent excessive memory usage
        if (conversationHistory.length > 20) {
            conversationHistory = conversationHistory.slice(-20);
        }
        
        // Save to localStorage
        try {
            localStorage.setItem('conversationHistory', JSON.stringify(conversationHistory));
            console.log(`Saved conversation history: ${conversationHistory.length} messages`);
        } catch (error) {
            console.warn('Failed to save conversation history to localStorage:', error);
        }
    }
    
    return data.response;
}

// Function to clear conversation history
function clearConversationHistory() {
    conversationHistory = [];
    localStorage.removeItem('conversationHistory');
    console.log('Conversation history cleared');
}

// Function to call the chat with file API
async function callChatWithFileAPI(message, file) {
    // Create form data with file, message, and client session ID
    const formData = new FormData();
    formData.append('file', file);
    formData.append('message', message);
    formData.append('clientSessionId', clientSessionId);
    
    console.log(`Sending file upload with clientSessionId: ${clientSessionId}`);
    
    // Log detailed information about the request we're about to make
    console.log(`Uploading file: Name=${file.name}, Type=${file.type}, Size=${file.size} bytes`);
    console.log(`Message length: ${message.length} characters`);
    console.log(`Client session ID: ${clientSessionId}`);
    
    // Try the new endpoint first (preferred)
    try {
        console.log('Attempting to use new endpoint: /api/document-chat/with-file');
        
        // Get authentication headers for file upload
        const authHeaders = await getAuthHeadersForFileUpload();
        
        // Don't set Content-Type header explicitly - browser will set it correctly with boundary parameter for multipart/form-data
        const response = await fetch('/api/document-chat/with-file', {
            method: 'POST',
            headers: authHeaders,
            body: formData,
            credentials: 'same-origin', // Ensure cookies are sent
            cache: 'no-cache' // Prevent caching issues
        });
        
        if (response.ok) {
            console.log('Successfully used new endpoint');
            const data = await response.json();
            return handleDocumentResponse(data, file);
        } else {
            console.warn('New endpoint failed with status:', response.status);
            try {
                // Try to get detailed error information
                const errorText = await response.text();
                console.error('Error details from new endpoint:', errorText);
            } catch (parseError) {
                console.error('Could not parse error response from new endpoint');
            }
            // Let it fall through to the fallback
        }
    } catch (error) {
        console.warn('Error using new endpoint:', error);
        // Continue to fallback
    }
    
    // Fallback to old endpoint if new one failed
    try {
        console.log('Falling back to legacy endpoint: /api/chat/with-file');
        
        // Create a fresh FormData object for the legacy endpoint
        const legacyFormData = new FormData();
        legacyFormData.append('file', file);
        legacyFormData.append('message', message);
        legacyFormData.append('clientSessionId', clientSessionId);
        
        const legacyResponse = await fetch('/api/chat/with-file', {
            method: 'POST',
            body: legacyFormData,
            credentials: 'same-origin', // Ensure cookies are sent
            cache: 'no-cache' // Prevent caching issues
        });
        
        console.log('Legacy endpoint response status:', legacyResponse.status);
        
        if (!legacyResponse.ok) {
            console.error('Both endpoints failed. Legacy status:', legacyResponse.status);
            
            try {
                // Try to get detailed error information
                const errorText = await legacyResponse.text();
                console.error('Error details from legacy endpoint:', errorText);
                throw new Error(`Error processing file: ${errorText}`);
            } catch (parseError) {
                throw new Error('Error processing file - could not get detailed error message');
            }
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
    
    // Clear conversation history when document context is cleared
    clearConversationHistory();
    
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
    clearConversationHistory,
    documentInContext, 
    setDocumentContext,
    updateUIDocumentState
};
