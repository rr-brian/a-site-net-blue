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
} from './ui.js';
import { sendMessage, downloadChatHistory, clearChat } from './chat.js';
import { 
    handleFileSelection, 
    handleClearDocumentContext, 
    setDocumentContextActive, 
    resetFileAttachment,
    currentFile as documentFile,
    currentFileName as documentFileName
} from './document-handler.js';
import { updateUIDocumentState } from './api.js';

// Initialize the application when the DOM is fully loaded
document.addEventListener('DOMContentLoaded', () => {
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
});
