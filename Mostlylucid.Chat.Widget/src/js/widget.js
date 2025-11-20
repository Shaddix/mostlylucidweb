import * as signalR from '@microsoft/signalr';
import Alpine from 'alpinejs';
import '../css/widget.css';

class ChatWidget {
    constructor(options = {}) {
        this.hubUrl = options.hubUrl || 'http://localhost:5100/chathub';
        this.userName = options.userName || '';
        this.userEmail = options.userEmail || '';
        this.container = null;
        this.connection = null;
        this.isConnected = false;

        // Initialize Alpine.js
        window.Alpine = Alpine;
        Alpine.start();

        this.init();
    }

    init() {
        // Create widget container
        this.container = document.createElement('div');
        this.container.className = 'chat-widget';
        this.container.setAttribute('x-data', 'chatWidget()');

        // Inject HTML
        this.container.innerHTML = this.getTemplate();

        // Append to body
        document.body.appendChild(this.container);

        // Setup Alpine component
        Alpine.data('chatWidget', () => this.getAlpineData());

        // Setup SignalR connection
        this.setupSignalR();
    }

    getTemplate() {
        return `
            <!-- Chat Button -->
            <button
                class="chat-widget-button"
                @click="toggleChat()"
                x-show="!isOpen">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
                </svg>
                <span class="chat-widget-badge" x-show="unreadCount > 0" x-text="unreadCount"></span>
            </button>

            <!-- Chat Window -->
            <div class="chat-widget-window" x-show="isOpen" x-cloak>
                <!-- Header -->
                <div class="chat-widget-header">
                    <div>
                        <h3>Chat with Scott</h3>
                        <p x-show="isConnected && adminOnline" class="status-online">
                            <span class="status-indicator"></span> Available
                        </p>
                        <p x-show="isConnected && !adminOnline" class="status-offline">
                            <span class="status-indicator"></span> Away
                        </p>
                        <p x-show="!isConnected">Connecting...</p>
                    </div>
                    <button class="chat-widget-close" @click="toggleChat()">
                        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                <!-- Welcome Screen -->
                <div class="chat-widget-welcome" x-show="!isRegistered">
                    <h4>👋 Welcome!</h4>
                    <p>Let's start a conversation</p>
                    <form @submit.prevent="register()">
                        <input
                            type="text"
                            placeholder="Your name"
                            x-model="userName"
                            required>
                        <input
                            type="email"
                            placeholder="Email (optional)"
                            x-model="userEmail">
                        <button type="submit">Start Chat</button>
                    </form>
                </div>

                <!-- Messages -->
                <div class="chat-widget-messages" x-show="isRegistered" x-ref="messagesContainer">
                    <template x-for="message in messages" :key="message.id">
                        <div class="chat-message" :class="message.senderType">
                            <div class="chat-message-avatar" x-text="getInitials(message.senderName)"></div>
                            <div class="chat-message-content">
                                <div class="chat-message-bubble" x-text="message.content"></div>
                                <div class="chat-message-time" x-text="formatTime(message.timestamp)"></div>
                            </div>
                        </div>
                    </template>

                    <!-- Typing indicator -->
                    <div class="chat-message admin" x-show="isTyping">
                        <div class="chat-message-avatar">S</div>
                        <div class="chat-message-content">
                            <div class="chat-message-bubble">
                                <div class="typing-indicator">
                                    <span></span>
                                    <span></span>
                                    <span></span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Input -->
                <div class="chat-widget-input" x-show="isRegistered">
                    <form @submit.prevent="sendMessage()">
                        <input
                            type="text"
                            placeholder="Type a message..."
                            x-model="messageInput"
                            @input="handleTyping()"
                            :disabled="!isConnected">
                        <button type="submit" :disabled="!isConnected || !messageInput.trim()">
                            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                            </svg>
                        </button>
                    </form>
                </div>
            </div>
        `;
    }

    getAlpineData() {
        const self = this;

        return {
            isOpen: false,
            isConnected: false,
            isRegistered: false,
            userName: this.userName,
            userEmail: this.userEmail,
            messages: [],
            messageInput: '',
            unreadCount: 0,
            isTyping: false,
            typingTimeout: null,
            adminOnline: false,

            toggleChat() {
                this.isOpen = !this.isOpen;
                if (this.isOpen) {
                    this.unreadCount = 0;
                    this.$nextTick(() => this.scrollToBottom());
                }
            },

            async register() {
                if (!this.userName.trim()) return;

                try {
                    await self.connection.invoke('RegisterUser',
                        this.userName,
                        this.userEmail,
                        window.location.href);
                    this.isRegistered = true;

                    // Save to localStorage
                    localStorage.setItem('chatWidget_userName', this.userName);
                    localStorage.setItem('chatWidget_userEmail', this.userEmail);
                } catch (err) {
                    console.error('Failed to register:', err);
                }
            },

            async sendMessage() {
                if (!this.messageInput.trim() || !this.isConnected) return;

                const content = this.messageInput.trim();
                this.messageInput = '';

                try {
                    await self.connection.invoke('SendMessage', content);
                } catch (err) {
                    console.error('Failed to send message:', err);
                }
            },

            handleTyping() {
                if (this.typingTimeout) {
                    clearTimeout(this.typingTimeout);
                }

                self.connection.invoke('UserTyping', true).catch(err =>
                    console.error('Failed to send typing indicator:', err));

                this.typingTimeout = setTimeout(() => {
                    self.connection.invoke('UserTyping', false).catch(err =>
                        console.error('Failed to send typing indicator:', err));
                }, 2000);
            },

            scrollToBottom() {
                const container = this.$refs.messagesContainer;
                if (container) {
                    container.scrollTop = container.scrollHeight;
                }
            },

            getInitials(name) {
                return name
                    .split(' ')
                    .map(n => n[0])
                    .join('')
                    .toUpperCase()
                    .substring(0, 2);
            },

            formatTime(timestamp) {
                const date = new Date(timestamp);
                return date.toLocaleTimeString('en-US', {
                    hour: 'numeric',
                    minute: '2-digit',
                    hour12: true
                });
            },

            addMessage(message) {
                this.messages.push(message);
                this.$nextTick(() => this.scrollToBottom());

                if (!this.isOpen && message.senderType === 'admin') {
                    this.unreadCount++;
                }
            }
        };
    }

    setupSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl)
            .withAutomaticReconnect()
            .build();

        // Event handlers
        this.connection.on('ConversationHistory', (messages) => {
            const component = Alpine.$data(this.container);
            component.messages = messages;
            component.$nextTick(() => component.scrollToBottom());
        });

        this.connection.on('MessageReceived', (message) => {
            const component = Alpine.$data(this.container);
            component.addMessage(message);
        });

        this.connection.on('AdminMessage', (message) => {
            const component = Alpine.$data(this.container);
            component.addMessage(message);

            // Play notification sound or show notification
            if (!component.isOpen) {
                this.showNotification(message);
            }
        });

        this.connection.on('AdminTyping', (isTyping) => {
            const component = Alpine.$data(this.container);
            component.isTyping = isTyping;
            if (isTyping) {
                component.$nextTick(() => component.scrollToBottom());
            }
        });

        this.connection.on('PresenceUpdate', (data) => {
            const component = Alpine.$data(this.container);
            component.adminOnline = data.adminOnline || false;
        });

        // Connection state
        this.connection.onreconnecting(() => {
            const component = Alpine.$data(this.container);
            component.isConnected = false;
        });

        this.connection.onreconnected(() => {
            const component = Alpine.$data(this.container);
            component.isConnected = true;
        });

        this.connection.onclose(() => {
            const component = Alpine.$data(this.container);
            component.isConnected = false;
        });

        // Start connection
        this.connection.start()
            .then(() => {
                const component = Alpine.$data(this.container);
                component.isConnected = true;
                this.isConnected = true;

                // Auto-register if we have saved credentials
                const savedName = localStorage.getItem('chatWidget_userName');
                const savedEmail = localStorage.getItem('chatWidget_userEmail');

                if (savedName) {
                    component.userName = savedName;
                    component.userEmail = savedEmail || '';
                    component.register();
                }
            })
            .catch(err => {
                console.error('SignalR connection error:', err);
            });
    }

    showNotification(message) {
        if ('Notification' in window && Notification.permission === 'granted') {
            new Notification('New message from Scott', {
                body: message.content,
                icon: '/chat-icon.png'
            });
        }
    }

    requestNotificationPermission() {
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
        }
    }
}

// Auto-initialize if data-chat-widget attribute is present
document.addEventListener('DOMContentLoaded', () => {
    const scriptTag = document.querySelector('script[data-chat-widget]');
    if (scriptTag) {
        const options = {
            hubUrl: scriptTag.getAttribute('data-hub-url') || 'http://localhost:5100/chathub',
            userName: scriptTag.getAttribute('data-user-name') || '',
            userEmail: scriptTag.getAttribute('data-user-email') || ''
        };

        new ChatWidget(options);
    }
});

// Export for manual initialization
export default ChatWidget;
