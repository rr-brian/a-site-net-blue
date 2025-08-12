/**
 * Core chat functionality for the chat application
 */

import { addMessage, showTypingIndicator, removeTypingIndicator } from './ui.js';
import { callChatAPI, callChatWithFileAPI } from './api.js';
import { resetFileAttachment } from './document-handler.js';

// Function to send a message
async function sendMessage(
    message, 
    chatMessages, 
    userInput, 
    currentFile = null, 
    currentFileName = null,
    uploadButton = null,
    fileInput = null
) {
    if (message === '') return;

    // Add user message to chat
    if (currentFile) {
        // For messages with files, add a special note
        addMessage(`${message}`, 'user', chatMessages, userInput);
        
        // Remove the file indicator UI since we're sending it now
        const fileIndicator = document.getElementById('fileUploadIndicator');
        if (fileIndicator) {
            fileIndicator.remove();
        }
    } else {
        addMessage(message, 'user', chatMessages, userInput);
    }

    // Clear input and reset height
    userInput.value = '';
    userInput.style.height = 'auto';

    // Show typing indicator
    showTypingIndicator(chatMessages);

    try {
        let response;
        
        // Check if we have a file to send with the message
        if (currentFile) {
            // Call the chat-with-file API
            response = await callChatWithFileAPI(message, currentFile);
            
            // Reset file attachment
            if (uploadButton && fileInput) {
                resetFileAttachment(uploadButton, fileInput);
            }
        } else {
            // Regular chat without file
            response = await callChatAPI(message);
        }
        
        // Remove typing indicator
        removeTypingIndicator();
        
        // Add bot response to chat
        addMessage(response, 'bot', chatMessages, userInput);
    } catch (error) {
        // Remove typing indicator
        removeTypingIndicator();
        
        // Show error message
        addMessage("Sorry, there was an error processing your request. Please try again later.", 'bot', chatMessages, userInput);
        console.error('Error:', error);
        
        // Reset file attachment on error
        if (currentFile && uploadButton && fileInput) {
            resetFileAttachment(uploadButton, fileInput);
        }
    }
}

// Function to download chat history
function downloadChatHistory(chatMessages, downloadChatButton) {
    try {
        // Show a loading indicator
        const originalTitle = downloadChatButton.getAttribute('title') || 'Download Chat';
        downloadChatButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        downloadChatButton.setAttribute('title', 'Preparing download...');
        
        // Create text content from chat messages
        let textContent = "RAI Chat Transcript\r\n";
        textContent += `Generated: ${new Date().toLocaleString()}\r\n\r\n`;
        
        const messages = chatMessages.querySelectorAll('.message');
        messages.forEach(message => {
            // Skip typing indicators
            if (message.classList.contains('typing-indicator')) return;
            
            // Determine sender
            const isBot = message.classList.contains('bot-message');
            const isSystem = message.classList.contains('system-message');
            const sender = isBot ? 'RAI' : (isSystem ? 'System' : 'You');
            
            // Get message content
            let content = message.textContent.trim();
            
            // Add to chat content
            textContent += `${sender}: ${content}\r\n\r\n`;
        });
        
        // Create a download link for the text file
        const blob = new Blob([textContent], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `rai-chat-${new Date().toISOString().slice(0, 10)}.txt`;
        
        // Append link, trigger click, and clean up
        document.body.appendChild(a);
        a.click();
        
        // Clean up after a short delay
        setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            
            // Restore the button
            downloadChatButton.innerHTML = '<i class="fas fa-download"></i>';
            downloadChatButton.setAttribute('title', originalTitle);
            
            console.log('Download initiated');
        }, 100);
    } catch (error) {
        console.error('Error downloading chat:', error);
        
        // Show error message
        addMessage('Sorry, there was an error downloading the chat history.', 'system', chatMessages);
        
        // Restore the button
        downloadChatButton.innerHTML = '<i class="fas fa-download"></i>';
        downloadChatButton.setAttribute('title', 'Download Chat');
    }
}

// Function to clear chat
function clearChat(chatMessages) {
    // Keep only the first welcome message
    const welcomeMessage = chatMessages.querySelector('.bot-message');
    chatMessages.innerHTML = '';
    if (welcomeMessage) {
        chatMessages.appendChild(welcomeMessage);
    }
}

// Export functions
export { sendMessage, downloadChatHistory, clearChat };
