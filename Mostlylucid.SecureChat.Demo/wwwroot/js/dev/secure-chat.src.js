/*
 * ⚠️ TRIVIAL IMPLEMENTATION OF CONCEPT - NOT PRODUCTION CODE ⚠️
 *
 * This demonstrates the basic concept only. Real implementation would need:
 * - End-to-end encryption
 * - Secure key exchange
 * - Message padding to prevent length analysis
 * - Timing randomization
 * - Session token rotation
 * - Comprehensive input validation
 * - XSS protection
 * - And much more...
 */

window.SecureChat = (function() {
    'use strict';

    let connection = null;
    let sessionId = null;
    let authTimeout = null;
    const AUTH_TIMEOUT_MS = 30000; // 30 seconds

    // Configuration - can be set via meta tag or use default
    const config = {
        hubUrl: document.querySelector('meta[name="chat-hub-url"]')?.getAttribute('content') || '/securechat',
        codeword: null // Set dynamically
    };

    function init() {
        console.log('Secure chat module loaded');
        showChatModal();
    }

    function showChatModal() {
        // Create modal if it doesn't exist
        let modal = document.getElementById('secure-chat-modal');
        if (!modal) {
            modal = createChatModal();
            document.body.appendChild(modal);
        }
        modal.classList.add('active');
        showAuthPrompt();
    }

    function createChatModal() {
        const modal = document.createElement('div');
        modal.id = 'secure-chat-modal';
        modal.className = 'chat-modal';
        modal.innerHTML = `
            <div class="chat-header">
                <span>Support Chat</span>
                <button class="chat-close" onclick="window.SecureChat.closeChat()">&times;</button>
            </div>
            <div class="chat-body" id="chat-body">
                <!-- Content loaded dynamically -->
            </div>
            <div class="chat-input-area" id="chat-input-area" style="display: none;">
                <input type="text" class="chat-input" id="chat-message-input" placeholder="Type your message..." />
                <button class="chat-send-btn" id="chat-send-btn">Send</button>
            </div>
        `;
        return modal;
    }

    function showAuthPrompt() {
        const chatBody = document.getElementById('chat-body');
        const countdown = { seconds: AUTH_TIMEOUT_MS / 1000 };

        chatBody.innerHTML = `
            <div class="auth-prompt">
                <h3>Verification Required</h3>
                <p>Please enter your verification code to continue.</p>
                <input type="text" id="codeword-input" placeholder="Enter code" autofocus />
                <button onclick="window.SecureChat.authenticate()">Verify</button>
                <div class="countdown">Time remaining: <span id="countdown">${countdown.seconds}</span>s</div>
            </div>
        `;

        // Start countdown
        const countdownEl = document.getElementById('countdown');
        authTimeout = setInterval(() => {
            countdown.seconds--;
            if (countdownEl) {
                countdownEl.textContent = countdown.seconds;
            }
            if (countdown.seconds <= 0) {
                clearInterval(authTimeout);
                handleAuthTimeout();
            }
        }, 1000);

        // Allow Enter key to submit
        document.getElementById('codeword-input').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                authenticate();
            }
        });
    }

    async function authenticate() {
        const input = document.getElementById('codeword-input');
        const codeword = input.value.trim();

        if (!codeword) {
            return;
        }

        clearInterval(authTimeout);

        try {
            // Connect to SignalR hub (configurable URL)
            connection = new signalR.HubConnectionBuilder()
                .withUrl(config.hubUrl)
                .withAutomaticReconnect()
                .build();

            setupConnectionHandlers();

            await connection.start();
            console.log('Connected to chat hub');

            // Authenticate
            const result = await connection.invoke('AuthenticateClient', codeword);

            if (result.success) {
                sessionId = result.sessionId;
                console.log('Authentication successful');
                showChatInterface();
            } else {
                console.log('Authentication failed');
                handleAuthFailure();
            }
        } catch (error) {
            console.error('Connection error:', error);
            handleAuthFailure();
        }
    }

    function setupConnectionHandlers() {
        connection.on('ReceiveMessage', (message) => {
            displayMessage(message);
        });

        connection.on('SupportJoined', () => {
            displaySystemMessage('A support agent has joined the chat');
        });

        connection.on('SessionEnded', () => {
            displaySystemMessage('This session has ended');
            setTimeout(() => closeChat(), 3000);
        });

        connection.on('ClientDisconnected', () => {
            displaySystemMessage('Connection lost');
        });
    }

    function showChatInterface() {
        const chatBody = document.getElementById('chat-body');
        chatBody.innerHTML = '<div class="message system">Connected to support. Please wait for an agent...</div>';

        document.getElementById('chat-input-area').style.display = 'block';

        // Setup send button
        document.getElementById('chat-send-btn').addEventListener('click', sendMessage);
        document.getElementById('chat-message-input').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                sendMessage();
            }
        });
    }

    async function sendMessage() {
        const input = document.getElementById('chat-message-input');
        const message = input.value.trim();

        if (!message || !connection || !sessionId) {
            return;
        }

        try {
            await connection.invoke('SendMessage', sessionId, message);
            displayMessage({
                message: message,
                timestamp: new Date().toISOString(),
                fromSupport: false
            });
            input.value = '';
        } catch (error) {
            console.error('Failed to send message:', error);
            displaySystemMessage('Failed to send message. Please try again.');
        }
    }

    function displayMessage(message) {
        const chatBody = document.getElementById('chat-body');
        const messageEl = document.createElement('div');
        messageEl.className = `message ${message.fromSupport ? 'support' : 'user'}`;
        messageEl.textContent = message.message;

        chatBody.appendChild(messageEl);
        chatBody.scrollTop = chatBody.scrollHeight;
    }

    function displaySystemMessage(text) {
        const chatBody = document.getElementById('chat-body');
        const messageEl = document.createElement('div');
        messageEl.className = 'message system';
        messageEl.textContent = text;

        chatBody.appendChild(messageEl);
        chatBody.scrollTop = chatBody.scrollHeight;
    }

    function handleAuthTimeout() {
        console.log('Authentication timeout');
        handleAuthFailure();
    }

    function handleAuthFailure() {
        // Get fallback URL from page metadata
        const fallbackMeta = document.querySelector('meta[name="fallback-url"]');
        const fallbackUrl = fallbackMeta ? fallbackMeta.getAttribute('content') : 'https://www.example.com/support';

        // Show "service unavailable" message briefly
        const chatBody = document.getElementById('chat-body');
        chatBody.innerHTML = `
            <div class="message system">
                Service temporarily unavailable.<br/>
                Redirecting to standard support...
            </div>
        `;

        // In a real system, this might:
        // 1. Forward to real support chat
        // 2. Strip the query parameter
        // 3. Show a generic error

        setTimeout(() => {
            closeChat();
            // In demo, just close. In production, would redirect
            console.log('Would redirect to:', fallbackUrl);
        }, 2000);
    }

    function closeChat() {
        const modal = document.getElementById('secure-chat-modal');
        if (modal) {
            modal.classList.remove('active');
        }

        if (connection) {
            connection.stop();
            connection = null;
        }

        if (authTimeout) {
            clearInterval(authTimeout);
        }

        sessionId = null;
    }

    // Public API
    return {
        init: init,
        authenticate: authenticate,
        closeChat: closeChat
    };
})();
