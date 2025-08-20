/**
 * Main script file for the RAI Chat application
 * This file coordinates the modules and sets up event listeners
 */

// Import modules
import { 
    addMessage, 
    showTypingIndicator, 
    removeTypingIndicator, 
    updateDocumentStatusIndicator 
} from './ui.js?v=1.0.2';
import { sendMessage, downloadChatHistory, clearChat } from './chat.js?v=1.0.2';
import { 
    handleFileSelection, 
    handleClearDocumentContext, 
    setDocumentContextActive, 
    resetFileAttachment,
    currentFile as documentFile,
    currentFileName as documentFileName
} from './document-handler.js?v=1.0.2';
import { updateUIDocumentState, clearDocumentContext } from './api.js?v=1.0.2';

// Initialize the application when the DOM is fully loaded
document.addEventListener('DOMContentLoaded', async () => {
    // Initialize authentication first
    console.log('Initializing authentication...');
    const authInitialized = await window.authModule.initializeAuth();
    
    if (!authInitialized) {
        console.log('Authentication required - waiting for user login');
        setupAuthEventListeners();
        return; // Don't initialize the rest of the app until authenticated
    }
    
    console.log('Authentication successful - initializing application');
    initializeApplication();
});

// Set up authentication event listeners
function setupAuthEventListeners() {
    const loginButton = document.getElementById('loginButton');
    const logoutButton = document.getElementById('logoutButton');
    
    if (loginButton) {
        loginButton.addEventListener('click', async () => {
            await window.authModule.login();
        });
    }
    
    if (logoutButton) {
        logoutButton.addEventListener('click', async () => {
            await window.authModule.logout();
        });
    }
}

// Initialize the main application after authentication
function initializeApplication() {
    // Get DOM elements
    const chatMessages = document.getElementById('chatMessages');
    const userInput = document.getElementById('userInput');
    const sendButton = document.getElementById('sendButton');
    const clearChatButton = document.getElementById('clearChatButton');
    const downloadChatButton = document.getElementById('downloadChatButton');
    const clearDocumentButton = document.getElementById('clearDocumentButton');
    const fileInput = document.getElementById('fileInput');
    const uploadButton = document.getElementById('uploadButton');
    
    // Initialize document context state
    let documentContextActive = false;

    // Auto-resize the textarea as the user types
    userInput.addEventListener('input', () => {
        userInput.style.height = 'auto';
        userInput.style.height = (userInput.scrollHeight) + 'px';
    });
    
    // Clear chat functionality
    clearChatButton.addEventListener('click', () => {
        clearChat(chatMessages);
    });
    
    // Download chat functionality
    downloadChatButton.addEventListener('click', () => {
        downloadChatHistory(chatMessages, downloadChatButton);
    });
    
    // Clear document context functionality
    clearDocumentButton.addEventListener('click', async () => {
        await handleClearDocumentContext(clearDocumentButton, chatMessages, userInput);
    });
    
    // Initialize UI with document context state from localStorage
    // Check localStorage for saved document context state
    documentContextActive = localStorage.getItem('documentInContext') === 'true';
    const savedDocumentName = localStorage.getItem('lastDocumentName');
    
    // Initialize the UI with the saved state
    updateUIDocumentState();
    
    // Make sure the document status indicator is updated with saved document name
    if (documentContextActive && savedDocumentName) {
        console.log('Initializing document status indicator with:', savedDocumentName);
        updateDocumentStatusIndicator(savedDocumentName);
    } else {
        updateDocumentStatusIndicator(null);
    }
    
    // Set up event listener to clear document context when user closes the application
    window.addEventListener('beforeunload', async (event) => {
        // Check if there's an active document context to clear
        if (localStorage.getItem('documentInContext') === 'true') {
            console.log('Application closing: Clearing document context');
            try {
                // Attempt to call the server to clear the context
                // We use the fetch API with keepalive to ensure the request completes
                // even after the page is unloading
                fetch('/api/document-chat/clear-context', {
                    method: 'POST',
                    credentials: 'same-origin',
                    keepalive: true // This is important to ensure the request completes during page unload
                });
                
                // Clear local storage immediately (don't wait for response)
                localStorage.setItem('documentInContext', 'false');
                localStorage.removeItem('lastDocumentName');
                console.log('Document context cleared on application close');
            } catch (error) {
                console.error('Error clearing document context on close:', error);
            }
        }
    });
    
    // File upload functionality
    fileInput.addEventListener('change', (e) => {
        const file = e.target.files[0];
        handleFileSelection(file, uploadButton, fileInput, chatMessages, userInput);
    });

    // Send message when the send button is clicked
    sendButton.addEventListener('click', () => {
        const message = userInput.value.trim();
        sendMessage(
            message, 
            chatMessages, 
            userInput, 
            documentFile, 
            documentFileName,
            uploadButton,
            fileInput
        );
    });

    // Send message when Enter key is pressed (but allow Shift+Enter for new lines)
    userInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            const message = userInput.value.trim();
            sendMessage(
                message, 
                chatMessages, 
                userInput, 
                documentFile, 
                documentFileName,
                uploadButton,
                fileInput
            );
        }
    });
    
    // Set up authentication event listeners for the main app
    setupAuthEventListeners();
}
