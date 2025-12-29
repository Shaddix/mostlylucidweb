/**
 * Session Store for SegmentCommerce
 * 
 * Integrates with TrackingManager for unified session/tracking management.
 * Provides Alpine.js component for reactive state management.
 * 
 * Session flow:
 * 1. TrackingManager initializes and detects best tracking mode
 * 2. sessionStore() Alpine component syncs with TrackingManager state
 * 3. HTMX requests include appropriate headers based on tracking mode
 * 4. Server responses can update session state
 */

const SESSION_HEADER = 'X-Session-ID';
const TRACKING_MODE_HEADER = 'X-Tracking-Mode';
const FINGERPRINT_HEADER = 'X-Fingerprint';
const DEMO_USER_HEADER = 'X-Demo-User';
const XSRF_HEADER = 'X-XSRF-TOKEN';

const DARK_MODE_KEY = 'sc_dark_mode';

// Ephemeral session query parameter (cookieless mode)
const SESSION_QUERY_PARAM = 'sessionid';

/**
 * Get XSRF token from meta tag
 */
function getXsrfToken() {
    const meta = document.querySelector('meta[name="xsrf-token"]');
    return meta?.getAttribute('content') || '';
}

/**
 * Alpine.js component for session and tracking management
 * Usage: x-data="sessionStore()" on root element
 */
function sessionStore() {
    return {
        // Tracking state (synced with TrackingManager)
        sessionId: null,
        trackingMode: 'none',
        fingerprint: null,
        demoUser: null,
        
        // UI state
        darkMode: false,
        showTrackingPanel: false,
        privacyFeatures: null,
        
        // Store the original page URL for reloading (captured at init time)
        _pageUrl: null,
        
        async init() {
            // Capture the current page URL before any HTMX operations can change it
            this._pageUrl = window.location.origin + window.location.pathname;
            // Wait for TrackingManager to initialize
            if (!window.TrackingManager) {
                console.error('[SessionStore] TrackingManager not loaded!');
                return;
            }
            
            // Initialize TrackingManager and sync state
            await TrackingManager.init();
            this.syncFromTrackingManager();
            
            // Listen for tracking changes
            window.addEventListener('tracking:modeChanged', () => this.syncFromTrackingManager());
            window.addEventListener('tracking:login', () => this.syncFromTrackingManager());
            window.addEventListener('tracking:logout', () => this.syncFromTrackingManager());
            window.addEventListener('tracking:reset', () => this.syncFromTrackingManager());
            
            // Load dark mode preference
            const savedDarkMode = localStorage.getItem(DARK_MODE_KEY);
            if (savedDarkMode !== null) {
                this.darkMode = savedDarkMode === 'true';
            } else {
                this.darkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
            }
            
            // Watch for dark mode changes
            this.$watch('darkMode', (value) => {
                localStorage.setItem(DARK_MODE_KEY, value.toString());
            });
            
            // Configure HTMX headers
            this.configureHtmx();
            
            // Get privacy features for display
            this.privacyFeatures = TrackingManager.detectPrivacyFeatures();
            
            console.debug('[SessionStore] Initialized', { 
                sessionId: this.sessionId, 
                trackingMode: this.trackingMode,
                darkMode: this.darkMode 
            });
        },
        
        syncFromTrackingManager() {
            const state = TrackingManager.state;
            this.sessionId = state.sessionId;
            this.trackingMode = state.mode;
            this.fingerprint = state.fingerprint;
            this.demoUser = state.demoUser;
        },
        
        configureHtmx() {
            const self = this;
            
            // Add tracking headers/params to all HTMX requests
            window.addEventListener('htmx:configRequest', ({ detail }) => {
                // In "none" (session only) mode, append sessionid to query params
                if (self.trackingMode === 'none' && self.sessionId) {
                    detail.parameters = {
                        ...(detail.parameters || {}),
                        [SESSION_QUERY_PARAM]: self.sessionId
                    };
                } else {
                    // For other modes, use headers
                    const trackingHeaders = TrackingManager.getHeaders();
                    Object.assign(detail.headers, trackingHeaders);
                }
                
                // Add XSRF token for non-GET requests
                if (detail.verb !== 'get') {
                    const xsrfToken = getXsrfToken();
                    if (xsrfToken) {
                        detail.headers[XSRF_HEADER] = xsrfToken;
                    }
                }
            });
            
            // Handle HTMX errors
            window.addEventListener('htmx:responseError', ({ detail }) => {
                console.error('[SessionStore] HTMX request failed:', detail);
            });
        },
        
        // ============ Tracking Mode Methods ============
        
        async setTrackingMode(mode) {
            await TrackingManager.setMode(mode);
            this.syncFromTrackingManager();
        },
        
        async loginDemoUser(user) {
            await TrackingManager.loginDemoUser(user);
            this.syncFromTrackingManager();
            
            // Close the dropdown
            this.showTrackingPanel = false;
            
            // Force a clean page reload to get new recommendations
            // Use a hidden form to do a native POST>redirect, bypassing HTMX completely
            const reloadUrl = this._pageUrl || window.location.origin + window.location.pathname;
            console.debug('[SessionStore] loginDemoUser - reloading to:', reloadUrl);
            
            // Create and submit a hidden form to force native navigation
            const form = document.createElement('form');
            form.method = 'GET';
            form.action = reloadUrl;
            form.style.display = 'none';
            // Mark form to not be processed by HTMX
            form.setAttribute('data-hx-boost', 'false');
            form.setAttribute('hx-boost', 'false');
            document.body.appendChild(form);
            form.submit();
        },
        
        async logoutDemoUser() {
            await TrackingManager.logoutDemoUser();
            this.syncFromTrackingManager();
            
            // Close the dropdown
            this.showTrackingPanel = false;
            
            // Force a clean page reload using same approach as loginDemoUser
            const reloadUrl = this._pageUrl || window.location.origin + window.location.pathname;
            console.debug('[SessionStore] logoutDemoUser - reloading to:', reloadUrl);
            
            // Create and submit a hidden form to force native navigation
            const form = document.createElement('form');
            form.method = 'GET';
            form.action = reloadUrl;
            form.style.display = 'none';
            form.setAttribute('hx-boost', 'false');
            document.body.appendChild(form);
            form.submit();
        },
        
        resetTracking() {
            TrackingManager.reset();
            this.syncFromTrackingManager();
            
            // Close the dropdown
            this.showTrackingPanel = false;
            
            // Force a clean page reload using same approach as loginDemoUser
            const reloadUrl = this._pageUrl || window.location.origin + window.location.pathname;
            console.debug('[SessionStore] resetTracking - reloading to:', reloadUrl);
            
            // Create and submit a hidden form to force native navigation
            const form = document.createElement('form');
            form.method = 'GET';
            form.action = reloadUrl;
            form.style.display = 'none';
            form.setAttribute('hx-boost', 'false');
            document.body.appendChild(form);
            form.submit();
        },
        
        // ============ UI Helpers ============
        
        get modeInfo() {
            return TrackingManager.getModeInfo(this.trackingMode);
        },
        
        get isLoggedIn() {
            return this.demoUser !== null;
        },
        
        get modeColor() {
            const colors = {
                'none': 'bg-gray-500',
                'fingerprint': 'bg-blue-500',
                'cookie': 'bg-yellow-500',
                'identity': 'bg-green-500'
            };
            return colors[this.trackingMode] || colors['none'];
        },
        
        get modeIcon() {
            const mode = this.trackingMode;
            // Returns SVG path data for icons
            const icons = {
                'none': 'M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z', // clock
                'fingerprint': 'M12 11c0 3.517-1.009 6.799-2.753 9.571m-3.44-2.04l.054-.09A13.916 13.916 0 008 11a4 4 0 118 0c0 1.017-.07 2.019-.203 3m-2.118 6.844A21.88 21.88 0 0015.171 17m3.839 1.132c.645-2.266.99-4.659.99-7.132A8 8 0 008 4.07M3 15.364c.64-1.319 1-2.8 1-4.364 0-1.457.39-2.823 1.07-4', // fingerprint
                'cookie': 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9', // globe/cookie
                'identity': 'M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z' // user
            };
            return icons[mode] || icons['none'];
        },
        
        toggleDarkMode() {
            this.darkMode = !this.darkMode;
        },
        
        toggleTrackingPanel() {
            this.showTrackingPanel = !this.showTrackingPanel;
        }
    };
}

// Make sessionStore available globally for Alpine
window.sessionStore = sessionStore;

// Also expose utilities for non-Alpine usage
window.SegmentSession = {
    getHeaders: () => TrackingManager?.getHeaders() || {},
    getXsrfToken,
    SESSION_HEADER,
    
    async fetch(url, options = {}) {
        const headers = {
            ...this.getHeaders(),
            [XSRF_HEADER]: getXsrfToken(),
            ...options.headers
        };
        return fetch(url, { ...options, headers });
    }
};
