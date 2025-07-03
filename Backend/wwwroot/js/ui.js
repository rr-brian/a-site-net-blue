/**
 * UI-related functionality for the chat application
 */

// Function to add a message to the chat UI
function addMessage(content, sender, chatMessages, userInput) {
    const messageDiv = document.createElement('div');
    messageDiv.classList.add('message', `${sender}-message`);
    
    const messageContent = document.createElement('div');
    messageContent.classList.add('message-content');
    
    // Format bot and system messages with better styling
    if (sender === 'bot' || sender === 'system') {
        // Convert line breaks to paragraphs
        const paragraphs = content.split('\n').filter(p => p.trim() !== '');
        
        // Add RAI branding to greetings (bot only)
        if (sender === 'bot' && (content.toLowerCase().includes('hello') || content.toLowerCase().includes('hi there'))) {
            if (!content.includes('RAI')) {
                content = content.replace(/^(Hello|Hi there)/i, '$1! I\'m RAI');
            }
        }
        
        paragraphs.forEach(paragraph => {
            const p = document.createElement('p');
            p.textContent = paragraph;
            messageContent.appendChild(p);
        });
    } else {
        // For user messages, just use text content
        messageContent.textContent = content;
    }
    
    messageDiv.appendChild(messageContent);
    chatMessages.appendChild(messageDiv);
    
    // Scroll to bottom
    chatMessages.scrollTop = chatMessages.scrollHeight;
    
    // Focus on the message input
    if (userInput) {
        userInput.focus();
    }
}

// Function to show typing indicator
function showTypingIndicator(chatMessages) {
    const typingDiv = document.createElement('div');
    typingDiv.classList.add('message', 'bot-message', 'typing-indicator');
    typingDiv.id = 'typingIndicator';
    
    for (let i = 0; i < 3; i++) {
        const dot = document.createElement('span');
        typingDiv.appendChild(dot);
    }
    
    chatMessages.appendChild(typingDiv);
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

// Function to remove typing indicator
function removeTypingIndicator() {
    const typingIndicator = document.getElementById('typingIndicator');
    if (typingIndicator) {
        typingIndicator.remove();
    }
}

// Function to update document context UI
function updateDocumentContextUI(documentContextActive, clearDocumentButton) {
    if (documentContextActive) {
        clearDocumentButton.classList.add('active');
        clearDocumentButton.setAttribute('title', 'Clear Document Context (Active)');
    } else {
        clearDocumentButton.classList.remove('active');
        clearDocumentButton.setAttribute('title', 'Clear Document Context (None)');
    }
}

// Export functions
export { addMessage, showTypingIndicator, removeTypingIndicator, updateDocumentContextUI };
