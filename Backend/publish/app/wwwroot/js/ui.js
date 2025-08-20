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
            
            // Convert code blocks with backticks to proper code elements
            if (paragraph.includes('`')) {
                // Replace inline code
                let formattedText = paragraph.replace(/`([^`]+)`/g, '<code>$1</code>');
                p.innerHTML = formattedText;
            } else {
                p.textContent = paragraph;
            }
            
            messageContent.appendChild(p);
        });
    } else {
        // For user messages, just use text content
        messageContent.textContent = content;
    }
    
    // Add timestamp to message
    const timestamp = document.createElement('div');
    timestamp.classList.add('message-time');
    const now = new Date();
    timestamp.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    messageContent.appendChild(timestamp);
    
    messageDiv.appendChild(messageContent);
    chatMessages.appendChild(messageDiv);
    
    // Initial scroll to bottom to ensure message is visible
    chatMessages.scrollTop = chatMessages.scrollHeight;
    
    // Scroll behavior based on message type and length
    if (sender === 'bot') {
        // For bot messages, use a small delay to ensure the message has been rendered
        // before calculating its height and adjusting scroll position
        setTimeout(() => {
            const messageHeight = messageDiv.offsetHeight;
            const chatHeight = chatMessages.clientHeight;
            
            // If the message is more than 60% of the chat window height, scroll to the top of the message
            if (messageHeight > chatHeight * 0.6) {
                // Get the position of the message relative to the chat container
                const messagePosition = messageDiv.offsetTop - chatMessages.offsetTop;
                // Scroll to the top of the message with smooth behavior
                chatMessages.scrollTo({
                    top: messagePosition,
                    behavior: 'smooth'
                });
            }
        }, 50); // Small delay to ensure the message has rendered
    }
    
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

// Function to update document status indicator
function updateDocumentStatusIndicator(documentName) {
    const statusIndicator = document.getElementById('document-status');
    
    if (!statusIndicator) return;
    
    if (documentName) {
        statusIndicator.textContent = `Document in context: ${documentName}`;
        statusIndicator.style.display = 'inline-block';
    } else {
        statusIndicator.style.display = 'none';
        statusIndicator.textContent = '';
    }
}

// Export functions
export { 
    addMessage, 
    showTypingIndicator, 
    removeTypingIndicator, 
    updateDocumentContextUI,
    updateDocumentStatusIndicator 
};
