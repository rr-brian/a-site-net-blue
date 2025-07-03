/**
 * Main script file for the RAI Chat application
 * This file coordinates the modules and sets up event listeners
 */

// Import modules
import { addMessage, showTypingIndicator, removeTypingIndicator, updateDocumentContextUI } from './ui.js';
import { sendMessage, downloadChatHistory, clearChat } from './chat.js';
import { handleFileSelection, handleClearDocumentContext, setDocumentContextActive } from './document-handler.js';

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
    let currentFile = null;
    let currentFileName = null;

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
    
    // Initialize UI
    updateDocumentContextUI(documentContextActive, clearDocumentButton);
    
    // File upload functionality
    fileInput.addEventListener('change', (e) => {
        const file = e.target.files[0];
        handleFileSelection(file, uploadButton, fileInput, chatMessages, userInput);
        currentFile = file;
        currentFileName = file ? file.name : null;
    });

    // Send message when the send button is clicked
    sendButton.addEventListener('click', () => {
        const message = userInput.value.trim();
        sendMessage(
            message, 
            chatMessages, 
            userInput, 
            currentFile, 
            currentFileName,
            uploadButton,
            fileInput
        );
        // Reset file state after sending
        if (currentFile) {
            currentFile = null;
            currentFileName = null;
        }
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
                currentFile, 
                currentFileName,
                uploadButton,
                fileInput
            );
            // Reset file state after sending
            if (currentFile) {
                currentFile = null;
                currentFileName = null;
            }
        }
    });
});
