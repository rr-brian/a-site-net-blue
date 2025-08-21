let conversationHistory = [];
let totalTokens = 0;

document.addEventListener('DOMContentLoaded', function() {
    const messageInput = document.getElementById('messageInput');
    const sendButton = document.getElementById('sendButton');
    const clearButton = document.getElementById('clearButton');
    const chatMessages = document.getElementById('chatMessages');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const tokenCount = document.getElementById('tokenCount');

    // Enable/disable send button based on input
    messageInput.addEventListener('input', function() {
        sendButton.disabled = !this.value.trim();
    });

    // Send message on Enter (but not Shift+Enter)
    messageInput.addEventListener('keydown', function(e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            if (!sendButton.disabled) {
                sendMessage();
            }
        }
    });

    sendButton.addEventListener('click', sendMessage);
    clearButton.addEventListener('click', clearChat);

    // Clear chat history on page load/refresh
    window.addEventListener('load', function() {
        clearChatOnRefresh();
    });

    function clearChatOnRefresh() {
        // Reset conversation history and token count
        conversationHistory = [];
        totalTokens = 0;
        tokenCount.textContent = 'Tokens: 0';
        
        // Reset chat messages to welcome state
        chatMessages.innerHTML = `
            <div class="welcome-message">
                <h2>Welcome to RAI - RTS AI Toolbox</h2>
                <p>Your AI-powered assistant is ready to help. Start a conversation below.</p>
            </div>
        `;
        
        console.log('Chat history cleared on page refresh');
    }

    async function sendMessage() {
        const message = messageInput.value.trim();
        if (!message) return;

        // Clear input and disable send button
        messageInput.value = '';
        sendButton.disabled = true;

        // Add user message to chat
        addMessageToChat('user', message);

        // Add to conversation history
        conversationHistory.push({ role: 'user', content: message });

        // Show loading
        loadingOverlay.style.display = 'flex';

        try {
            const response = await fetch('/api/chat/send', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    messages: conversationHistory,
                    maxTokens: 1000,
                    temperature: 0.7
                })
            });

            if (!response.ok) {
                throw new Error('Failed to get response');
            }

            const data = await response.json();

            if (data.success) {
                // Add assistant message to chat
                addMessageToChat('assistant', data.content);
                
                // Add to conversation history
                conversationHistory.push({ role: 'assistant', content: data.content });
                
                // Update token count
                totalTokens += data.totalTokens || 0;
                tokenCount.textContent = `Tokens: ${totalTokens}`;
            } else {
                showError(data.error || 'Failed to get response');
            }
        } catch (error) {
            console.error('Error:', error);
            showError('An error occurred. Please try again.');
        } finally {
            loadingOverlay.style.display = 'none';
            messageInput.focus();
        }
    }

    function addMessageToChat(role, content) {
        // Remove welcome message if it exists
        const welcomeMessage = chatMessages.querySelector('.welcome-message');
        if (welcomeMessage) {
            welcomeMessage.remove();
        }

        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${role}`;

        const contentDiv = document.createElement('div');
        contentDiv.className = 'message-content';
        contentDiv.innerHTML = formatMessage(content, role);

        const timeDiv = document.createElement('div');
        timeDiv.className = 'message-time';
        timeDiv.textContent = new Date().toLocaleTimeString();

        contentDiv.appendChild(timeDiv);
        messageDiv.appendChild(contentDiv);
        chatMessages.appendChild(messageDiv);

        // Scroll to bottom
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function clearChat() {
        if (confirm('Are you sure you want to clear the chat history?')) {
            conversationHistory = [];
            totalTokens = 0;
            tokenCount.textContent = 'Tokens: 0';
            
            chatMessages.innerHTML = `
                <div class="welcome-message">
                    <h2>Welcome to RAI - RTS AI Toolbox</h2>
                    <p>Your AI-powered assistant is ready to help. Start a conversation below.</p>
                </div>
            `;
        }
    }

    function showError(message) {
        const errorDiv = document.createElement('div');
        errorDiv.className = 'message assistant';
        errorDiv.innerHTML = `
            <div class="message-content" style="background: #f8d7da; color: #721c24;">
                Error: ${message}
            </div>
        `;
        chatMessages.appendChild(errorDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // Test connection and load documents on page load
    testConnection();
    
    // Load documents after a slight delay to ensure page is ready
    setTimeout(() => {
        loadDocumentsForChat();
    }, 500);

    async function testConnection() {
        try {
            const response = await fetch('/api/config');
            if (response.ok) {
                const config = await response.json();
                console.log('Connected as:', config.user.email);
            }
        } catch (error) {
            console.error('Connection test failed:', error);
        }
    }

    async function loadDocumentsForChat() {
        // This function is no longer needed since we don't show documents in chat
        // Documents are shown in the header indicator instead
        console.log('Documents display moved to header indicator');
    }

    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    function formatMessage(content, role) {
        if (role === 'user') {
            // User messages - simple escaping
            return escapeHtml(content).replace(/\n/g, '<br>');
        }
        
        // Assistant messages - apply formatting
        let formatted = escapeHtml(content);
        
        // Convert **bold** to <strong>
        formatted = formatted.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        
        // Convert *italic* to <em>
        formatted = formatted.replace(/\*(.*?)\*/g, '<em>$1</em>');
        
        // Convert numbered lists
        formatted = formatted.replace(/^(\d+\.\s)/gm, '<div class="list-item">$1');
        formatted = formatted.replace(/<div class="list-item">(\d+\.\s.*?)(?=\n|$)/g, '<div class="list-item">$1</div>');
        
        // Convert bullet points
        formatted = formatted.replace(/^[-•]\s/gm, '<div class="bullet-item">• ');
        formatted = formatted.replace(/<div class="bullet-item">(• .*?)(?=\n|$)/g, '<div class="bullet-item">$1</div>');
        
        // Convert line breaks to <br> but preserve paragraphs
        formatted = formatted.replace(/\n\n/g, '</p><p>');
        formatted = formatted.replace(/\n/g, '<br>');
        
        // Wrap in paragraphs if not already wrapped
        if (!formatted.startsWith('<')) {
            formatted = '<p>' + formatted + '</p>';
        }
        
        return formatted;
    }
    
    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function(m) { return map[m]; });
    }

    // Global function to refresh document context (called from upload.js)
    window.refreshChatDocuments = loadDocumentsForChat;
});