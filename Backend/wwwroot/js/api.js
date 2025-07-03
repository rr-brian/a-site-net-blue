/**
 * API communication functions for the chat application
 */

// Function to call the chat API
async function callChatAPI(message) {
    const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ message })
    });
    
    if (!response.ok) {
        throw new Error('Network response was not ok');
    }
    
    const data = await response.json();
    return data.response;
}

// Function to call the chat with file API
async function callChatWithFileAPI(message, file) {
    // Create form data with both file and message
    const formData = new FormData();
    formData.append('file', file);
    formData.append('message', message);
    
    // Call the chat-with-file API
    const response = await fetch('/api/chat/with-file', {
        method: 'POST',
        body: formData
    });
    
    if (!response.ok) {
        throw new Error('Error processing file with message');
    }
    
    const data = await response.json();
    return data.response;
}

// Function to clear document context
async function clearDocumentContext() {
    const response = await fetch('/api/chat/clear-document', {
        method: 'POST'
    });
    
    if (!response.ok) {
        throw new Error('Error clearing document context');
    }
    
    return await response.json();
}

// Export functions
export { callChatAPI, callChatWithFileAPI, clearDocumentContext };
