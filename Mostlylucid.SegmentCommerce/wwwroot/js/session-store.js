/**
 * Session Store for SegmentCommerce
 * 
 * Implements cookieless session tracking using:
 * - localStorage for session ID persistence
 * - HTMX headers for server communication
 * - Alpine.js for reactive state management
 * 
 * Session flow:
 * 1. On init, load or generate session ID from localStorage
 * 2. Configure HTMX to send X-Session-ID header with all requests
 * 3. Listen for server response headers to update session ID if needed
 * 4. Persist dark mode preference
 */

const SESSION_KEY = 'sc_session_id';
const DARK_MODE_KEY = 'sc_dark_mode';
const SESSION_HEADER = 'X-Session-ID';
const XSRF_HEADER = 'X-XSRF-TOKEN';

/**
 * Generate a unique session ID
 * Format: s_<uuid without dashes>
 */
function generateSessionId() {
    const uuid = crypto.randomUUID 
        ? crypto.randomUUID() 
        : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    return `s_${uuid.replace(/-/g, '')}`;
}

/**
 * Get or create session ID from localStorage
 */
function getOrCreateSessionId() {
    let sessionId = localStorage.getItem(SESSION_KEY);
    if (!sessionId) {
        sessionId = generateSessionId();
        localStorage.setItem(SESSION_KEY, sessionId);
        console.debug('[SessionStore] Created new session:', sessionId);
    }
    return sessionId;
}

/**
 * Get XSRF token from meta tag
 */
function getXsrfToken() {
    const meta = document.querySelector('meta[name="xsrf-token"]');
    return meta?.getAttribute('content') || '';
}

/**
 * Alpine.js component for session management
 * Usage: x-data="sessionStore()" on root element
 */
function sessionStore() {
    return {
        sessionId: null,
        darkMode: false,
        
        init() {
            // Load session ID
            this.sessionId = getOrCreateSessionId();
            
            // Load dark mode preference
            const savedDarkMode = localStorage.getItem(DARK_MODE_KEY);
            if (savedDarkMode !== null) {
                this.darkMode = savedDarkMode === 'true';
            } else {
                // Check system preference
                this.darkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
            }
            
            // Watch for dark mode changes
            this.$watch('darkMode', (value) => {
                localStorage.setItem(DARK_MODE_KEY, value.toString());
            });
            
            // Configure HTMX headers
            this.configureHtmx();
            
            console.debug('[SessionStore] Initialized', { 
                sessionId: this.sessionId, 
                darkMode: this.darkMode 
            });
        },
        
        configureHtmx() {
            // Add session ID and XSRF token to all HTMX requests
            document.body.addEventListener('htmx:configRequest', (event) => {
                const headers = event.detail.headers;
                
                // Add session ID header
                if (this.sessionId) {
                    headers[SESSION_HEADER] = this.sessionId;
                }
                
                // Add XSRF token for non-GET requests
                if (event.detail.verb !== 'get') {
                    const xsrfToken = getXsrfToken();
                    if (xsrfToken) {
                        headers[XSRF_HEADER] = xsrfToken;
                    }
                }
            });
            
            // Update session ID from server response if provided
            document.body.addEventListener('htmx:afterRequest', (event) => {
                const xhr = event.detail.xhr;
                if (xhr) {
                    const serverSessionId = xhr.getResponseHeader(SESSION_HEADER);
                    if (serverSessionId && serverSessionId !== this.sessionId) {
                        console.debug('[SessionStore] Server updated session:', serverSessionId);
                        this.sessionId = serverSessionId;
                        localStorage.setItem(SESSION_KEY, serverSessionId);
                    }
                }
            });
            
            // Handle HTMX errors
            document.body.addEventListener('htmx:responseError', (event) => {
                console.error('[SessionStore] HTMX request failed:', event.detail);
            });
        },
        
        /**
         * Get session ID for use in non-HTMX requests (fetch, etc.)
         */
        getSessionId() {
            return this.sessionId;
        },
        
        /**
         * Build URL with session ID query parameter
         * Useful for non-HTMX navigation or external links
         */
        buildUrl(url) {
            const urlObj = new URL(url, window.location.origin);
            if (this.sessionId) {
                urlObj.searchParams.set('_sid', this.sessionId);
            }
            return urlObj.toString();
        },
        
        /**
         * Toggle dark mode
         */
        toggleDarkMode() {
            this.darkMode = !this.darkMode;
        }
    };
}

// Make sessionStore available globally for Alpine
window.sessionStore = sessionStore;

// Also expose utilities for non-Alpine usage
window.SegmentSession = {
    getSessionId: getOrCreateSessionId,
    getXsrfToken: getXsrfToken,
    SESSION_HEADER,
    
    /**
     * Get headers object for fetch requests
     */
    getHeaders() {
        return {
            [SESSION_HEADER]: getOrCreateSessionId(),
            [XSRF_HEADER]: getXsrfToken()
        };
    },
    
    /**
     * Fetch wrapper that includes session headers
     */
    async fetch(url, options = {}) {
        const headers = {
            ...this.getHeaders(),
            ...options.headers
        };
        return fetch(url, { ...options, headers });
    }
};
