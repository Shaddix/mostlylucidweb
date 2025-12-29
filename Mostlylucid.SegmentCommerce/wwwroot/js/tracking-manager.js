/**
 * Tracking Manager for SegmentCommerce
 * 
 * Manages different tracking modes:
 * - Session Only: No persistence beyond browser session (cookieless, no fingerprint)
 * - Fingerprint: Privacy-respecting device fingerprint (no cookies, cross-session)
 * - Cookie: Traditional cookie-based tracking (requires consent)
 * - Identity: Logged-in user tracking (demo mode uses cookie)
 * 
 * Respects privacy browsers (Firefox, Brave, etc.) by detecting limitations
 * and falling back gracefully.
 */

const TrackingManager = (() => {
    // Storage keys
    const KEYS = {
        MODE: 'sc_tracking_mode',
        SESSION_ID: 'sc_session_id',
        FINGERPRINT: 'sc_fingerprint',
        DEMO_USER: 'sc_demo_user',
        COOKIE_CONSENT: 'sc_cookie_consent'
    };

    // Cookie names (for server-side reading)
    const COOKIES = {
        SESSION: 'sc_sid',
        FINGERPRINT: 'sc_fp',
        TRACKING: 'sc_track',
        DEMO_USER: 'sc_demo'
    };

    // Tracking modes matching server enum
    const MODES = {
        NONE: 'none',           // Session only - no persistence
        FINGERPRINT: 'fingerprint', // Device fingerprint
        COOKIE: 'cookie',       // Cookie-based
        IDENTITY: 'identity'    // Logged-in user
    };

    // Privacy browser detection results
    let privacyFeatures = null;

    /**
     * Detect privacy features/limitations of current browser
     */
    function detectPrivacyFeatures() {
        if (privacyFeatures) return privacyFeatures;

        const ua = navigator.userAgent.toLowerCase();
        
        privacyFeatures = {
            // Browser detection
            isFirefox: ua.includes('firefox'),
            isBrave: navigator.brave !== undefined,
            isSafari: ua.includes('safari') && !ua.includes('chrome'),
            isTor: ua.includes('tor'),
            
            // Feature detection
            hasLocalStorage: (() => {
                try {
                    localStorage.setItem('_test', '1');
                    localStorage.removeItem('_test');
                    return true;
                } catch { return false; }
            })(),
            
            hasCookies: navigator.cookieEnabled,
            
            // Canvas fingerprinting (Firefox/Brave may block or randomize)
            hasCanvasFingerprint: (() => {
                try {
                    const canvas = document.createElement('canvas');
                    const ctx = canvas.getContext('2d');
                    if (!ctx) return false;
                    ctx.textBaseline = 'top';
                    ctx.font = '14px Arial';
                    ctx.fillText('test', 2, 2);
                    return canvas.toDataURL().length > 100;
                } catch { return false; }
            })(),
            
            // WebGL fingerprinting
            hasWebGL: (() => {
                try {
                    const canvas = document.createElement('canvas');
                    return !!(canvas.getContext('webgl') || canvas.getContext('experimental-webgl'));
                } catch { return false; }
            })(),
            
            // Enhanced tracking protection (Firefox)
            hasETP: (() => {
                // Firefox ETP can be detected by checking if some APIs are restricted
                // This is a heuristic - not 100% reliable
                try {
                    const test = document.cookie;
                    return false; // If we can read cookies, ETP isn't blocking us
                } catch {
                    return true;
                }
            })(),
            
            // Do Not Track header
            doNotTrack: navigator.doNotTrack === '1' || 
                        window.doNotTrack === '1' ||
                        navigator.msDoNotTrack === '1'
        };

        // Determine best available tracking mode
        privacyFeatures.recommendedMode = determineRecommendedMode(privacyFeatures);
        
        console.debug('[TrackingManager] Privacy features detected:', privacyFeatures);
        return privacyFeatures;
    }

    /**
     * Determine the best tracking mode for this browser
     */
    function determineRecommendedMode(features) {
        // If DNT is set, respect it
        if (features.doNotTrack) {
            return MODES.NONE;
        }
        
        // Tor browser - session only
        if (features.isTor) {
            return MODES.NONE;
        }
        
        // If cookies work, fingerprint is most reliable
        if (features.hasLocalStorage) {
            return MODES.FINGERPRINT;
        }
        
        // Fallback to session only
        return MODES.NONE;
    }

    /**
     * Generate a simple, privacy-respecting fingerprint
     * Uses only stable, non-invasive signals
     */
    async function generateFingerprint() {
        const features = detectPrivacyFeatures();
        
        // For privacy browsers, use a simpler fingerprint
        // that's less unique but still useful for same-device recognition
        const signals = [];
        
        // Basic signals (available everywhere)
        signals.push(navigator.language || 'unknown');
        signals.push(screen.width + 'x' + screen.height);
        signals.push(screen.colorDepth || 0);
        signals.push(new Date().getTimezoneOffset());
        signals.push(Intl.DateTimeFormat().resolvedOptions().timeZone || 'unknown');
        
        // Platform (may be frozen in newer browsers)
        const platform = navigator.userAgentData?.platform || navigator.platform || 'unknown';
        signals.push(platform);
        
        // Only add UA-based signals if not a privacy browser
        if (!features.isFirefox && !features.isBrave && !features.isTor) {
            signals.push(navigator.userAgent || 'unknown');
            signals.push(navigator.hardwareConcurrency || 0);
            signals.push(navigator.deviceMemory || 0);
        }
        
        // Hash the signals
        const fingerprint = await hashString(signals.join('|'));
        return fingerprint;
    }

    /**
     * SHA-256 hash a string
     */
    async function hashString(input) {
        try {
            const encoder = new TextEncoder();
            const data = encoder.encode(input);
            const hash = await crypto.subtle.digest('SHA-256', data);
            const bytes = Array.from(new Uint8Array(hash));
            return bytes.map(b => b.toString(16).padStart(2, '0')).join('');
        } catch (err) {
            // Fallback for older browsers
            console.warn('[TrackingManager] SHA-256 failed, using fallback:', err);
            let hash = 0;
            for (let i = 0; i < input.length; i++) {
                const char = input.charCodeAt(i);
                hash = ((hash << 5) - hash) + char;
                hash = hash & hash;
            }
            return Math.abs(hash).toString(16).padStart(16, '0');
        }
    }

    /**
     * Generate a random session ID
     */
    function generateSessionId() {
        const uuid = crypto.randomUUID 
            ? crypto.randomUUID() 
            : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
        return `s_${uuid.replace(/-/g, '')}`;
    }

    /**
     * Set a cookie
     */
    function setCookie(name, value, days = 7) {
        const features = detectPrivacyFeatures();
        if (!features.hasCookies) return false;
        
        try {
            const expires = new Date(Date.now() + days * 24 * 60 * 60 * 1000).toUTCString();
            const secure = window.location.protocol === 'https:' ? '; Secure' : '';
            document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; SameSite=Lax; Expires=${expires}${secure}`;
            return true;
        } catch {
            return false;
        }
    }

    /**
     * Get a cookie value
     */
    function getCookie(name) {
        try {
            const match = document.cookie
                .split(';')
                .map(c => c.trim())
                .find(c => c.startsWith(`${name}=`));
            return match ? decodeURIComponent(match.split('=')[1]) : null;
        } catch {
            return null;
        }
    }

    /**
     * Delete a cookie
     */
    function deleteCookie(name) {
        document.cookie = `${name}=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT`;
    }

    /**
     * Get or set value in localStorage with fallback
     */
    function storage(key, value = undefined) {
        const features = detectPrivacyFeatures();
        
        if (value === undefined) {
            // Get
            if (!features.hasLocalStorage) return null;
            try {
                return localStorage.getItem(key);
            } catch {
                return null;
            }
        } else if (value === null) {
            // Delete
            if (!features.hasLocalStorage) return;
            try {
                localStorage.removeItem(key);
            } catch {}
        } else {
            // Set
            if (!features.hasLocalStorage) return false;
            try {
                localStorage.setItem(key, value);
                return true;
            } catch {
                return false;
            }
        }
    }

    // ============ PUBLIC API ============

    /**
     * Current tracking state
     */
    const state = {
        mode: MODES.NONE,
        sessionId: null,
        fingerprint: null,
        demoUser: null,
        initialized: false,
        serverSession: null  // Session from server meta tag (for cookieless mode)
    };

    /**
     * Read session config from server-rendered script tag.
     * This enables truly cookieless "Session Only" mode.
     * Uses a JSON script tag to avoid Htmx.TagHelpers meta tag interference.
     */
    function readServerSession() {
        const script = document.getElementById('sc-session');
        if (!script) return null;
        
        const content = script.textContent || script.innerText;
        if (!content || !content.trim()) return null;
        
        try {
            return JSON.parse(content);
        } catch (e) {
            console.warn('[TrackingManager] Failed to parse server session:', e);
            return null;
        }
    }

    /**
     * Initialize tracking based on saved preferences or detection
     */
    async function init() {
        if (state.initialized) return state;
        
        const features = detectPrivacyFeatures();
        
        // Read server-provided session (always available in page)
        state.serverSession = readServerSession();
        
        // Check for sessionid in URL (indicates session-only mode, survives refresh)
        const urlSessionId = getSessionIdFromUrl();
        
        // Load saved mode or use recommended
        let savedMode = storage(KEYS.MODE);
        
        // If sessionid is in URL, force session-only mode
        if (urlSessionId) {
            savedMode = MODES.NONE;
        } else if (!savedMode || !Object.values(MODES).includes(savedMode)) {
            savedMode = features.recommendedMode;
        }
        
        // SESSION ONLY MODE (none): Use URL sessionid or server-provided session
        if (savedMode === MODES.NONE) {
            // Priority: URL sessionid > server session > generate new
            if (urlSessionId) {
                state.sessionId = urlSessionId;
                console.debug('[TrackingManager] Session Only mode - using URL sessionid:', state.sessionId);
            } else if (state.serverSession?.sessionId) {
                state.sessionId = state.serverSession.sessionId;
                // Also add to URL so it survives refresh
                updateUrlWithSessionId(state.sessionId);
                console.debug('[TrackingManager] Session Only mode - using server session:', state.sessionId);
            } else {
                // Fallback: generate ephemeral key
                state.sessionId = generateEphemeralKey();
                updateUrlWithSessionId(state.sessionId);
                console.debug('[TrackingManager] Session Only mode - generated ephemeral key:', state.sessionId);
            }
            
            // Don't save anything to localStorage or cookies
            state.mode = MODES.NONE;
            state.fingerprint = null;
            state.demoUser = null;
            state.initialized = true;
            
            // Do NOT sync to cookies in none mode
            console.debug('[TrackingManager] Initialized (Session Only - no cookies):', state);
            window.dispatchEvent(new CustomEvent('tracking:initialized', { detail: state }));
            return state;
        }
        
        // PERSISTENT MODES (fingerprint, cookie, identity): Use client storage
        
        // Get session ID from storage or generate new one
        state.sessionId = storage(KEYS.SESSION_ID);
        if (!state.sessionId) {
            // Prefer server session if available, else generate
            state.sessionId = state.serverSession?.sessionId || generateSessionId();
            storage(KEYS.SESSION_ID, state.sessionId);
        }
        
        // Load fingerprint if using that mode
        if (savedMode === MODES.FINGERPRINT || savedMode === MODES.COOKIE || savedMode === MODES.IDENTITY) {
            state.fingerprint = storage(KEYS.FINGERPRINT);
            if (!state.fingerprint) {
                state.fingerprint = await generateFingerprint();
                storage(KEYS.FINGERPRINT, state.fingerprint);
            }
        }
        
        // Load demo user if logged in
        state.demoUser = storage(KEYS.DEMO_USER);
        if (state.demoUser) {
            try {
                state.demoUser = JSON.parse(state.demoUser);
                savedMode = MODES.IDENTITY;
            } catch {
                state.demoUser = null;
            }
        }
        
        state.mode = savedMode;
        state.initialized = true;
        
        // Sync to cookies for server-side reading (only in persistent modes)
        syncToCookies();
        
        console.debug('[TrackingManager] Initialized:', state);
        
        // Dispatch event for Alpine/other listeners
        window.dispatchEvent(new CustomEvent('tracking:initialized', { detail: state }));
        
        return state;
    }

    /**
     * Sync current state to cookies for server-side reading.
     * IMPORTANT: In "none" mode, NO cookies are set - session is passed via headers only.
     */
    function syncToCookies() {
        // Session Only mode: NO cookies at all
        if (state.mode === MODES.NONE) {
            // Clear any existing cookies from previous modes
            deleteCookie(COOKIES.SESSION);
            deleteCookie(COOKIES.TRACKING);
            deleteCookie(COOKIES.FINGERPRINT);
            deleteCookie(COOKIES.DEMO_USER);
            console.debug('[TrackingManager] Session Only mode - cleared all cookies');
            return;
        }
        
        // Persistent modes: set cookies for server-side reading
        setCookie(COOKIES.SESSION, state.sessionId, 1); // 1 day for session
        
        // Set tracking mode cookie
        setCookie(COOKIES.TRACKING, state.mode, 365);
        
        // Set fingerprint cookie if using fingerprint mode
        if (state.mode === MODES.FINGERPRINT || state.mode === MODES.COOKIE || state.mode === MODES.IDENTITY) {
            if (state.fingerprint) {
                setCookie(COOKIES.FINGERPRINT, state.fingerprint, 30);
            }
        } else {
            deleteCookie(COOKIES.FINGERPRINT);
        }
        
        // Set demo user cookie if logged in
        if (state.demoUser) {
            setCookie(COOKIES.DEMO_USER, state.demoUser.id, 7);
        } else {
            deleteCookie(COOKIES.DEMO_USER);
        }
    }

    /**
     * Change tracking mode
     */
    async function setMode(newMode) {
        if (!Object.values(MODES).includes(newMode)) {
            console.error('[TrackingManager] Invalid mode:', newMode);
            return false;
        }
        
        const oldMode = state.mode;
        state.mode = newMode;
        
        // Session Only mode: Clear all client storage, add sessionid to URL
        if (newMode === MODES.NONE) {
            state.fingerprint = null;
            state.demoUser = null;
            
            // Clear localStorage
            storage(KEYS.MODE, null);
            storage(KEYS.SESSION_ID, null);
            storage(KEYS.FINGERPRINT, null);
            storage(KEYS.DEMO_USER, null);
            
            // Get fresh session from server or generate ephemeral key
            if (state.serverSession?.sessionId) {
                state.sessionId = state.serverSession.sessionId;
            } else {
                // Generate a simple ephemeral key client-side as fallback
                state.sessionId = generateEphemeralKey();
            }
            
            // Add sessionid to URL (survives refresh)
            updateUrlWithSessionId(state.sessionId);
            
            console.debug('[TrackingManager] Switched to Session Only - sessionid in URL:', state.sessionId);
        } else {
            // Persistent modes: save to localStorage, remove sessionid from URL
            storage(KEYS.MODE, newMode);
            
            // Remove sessionid from URL when leaving session-only mode
            removeSessionIdFromUrl();
            
            // Ensure we have a session ID in storage
            if (!storage(KEYS.SESSION_ID)) {
                storage(KEYS.SESSION_ID, state.sessionId);
            }
            
            // Generate fingerprint if needed
            if ((newMode === MODES.FINGERPRINT || newMode === MODES.COOKIE) && !state.fingerprint) {
                state.fingerprint = await generateFingerprint();
                storage(KEYS.FINGERPRINT, state.fingerprint);
            }
            
            // If downgrading from identity, clear demo user
            if (oldMode === MODES.IDENTITY && newMode !== MODES.IDENTITY) {
                state.demoUser = null;
                storage(KEYS.DEMO_USER, null);
            }
        }
        
        syncToCookies();
        
        // Dispatch event
        window.dispatchEvent(new CustomEvent('tracking:modeChanged', { 
            detail: { oldMode, newMode, state } 
        }));
        
        console.debug('[TrackingManager] Mode changed:', oldMode, '->', newMode);
        return true;
    }
    
    /**
     * Generate a short ephemeral key (client-side fallback)
     */
    function generateEphemeralKey() {
        // Simple base62 encoding of random bytes
        const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
        const bytes = new Uint8Array(8);
        crypto.getRandomValues(bytes);
        let key = '';
        for (const byte of bytes) {
            key += chars[byte % 62];
        }
        return key;
    }
    
    /**
     * Get sessionid from current URL query string
     */
    function getSessionIdFromUrl() {
        const params = new URLSearchParams(window.location.search);
        const sessionId = params.get('sessionid');
        // Validate format: 8-16 alphanumeric characters
        if (sessionId && /^[0-9A-Za-z]{8,16}$/.test(sessionId)) {
            return sessionId;
        }
        return null;
    }
    
    /**
     * Add sessionid to current URL
     */
    function updateUrlWithSessionId(sessionId) {
        const url = new URL(window.location.href);
        url.searchParams.set('sessionid', sessionId);
        window.history.replaceState({}, '', url.toString());
    }
    
    /**
     * Remove sessionid from current URL
     */
    function removeSessionIdFromUrl() {
        const url = new URL(window.location.href);
        if (url.searchParams.has('sessionid')) {
            url.searchParams.delete('sessionid');
            window.history.replaceState({}, '', url.toString());
        }
    }

    /**
     * Log in as a demo user
     * Calls server API to link session to demo user's profile.
     */
    async function loginDemoUser(user) {
        try {
            // Call server API to link session to demo user profile
            const response = await fetch('/api/demo-users/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Session-ID': state.sessionId
                },
                body: JSON.stringify({ demoUserId: user.id })
            });
            
            if (!response.ok) {
                const error = await response.json();
                console.error('[TrackingManager] Login failed:', error);
                return { success: false, error: error.error };
            }
            
            const result = await response.json();
            
            // Update local state
            state.demoUser = user;
            state.mode = MODES.IDENTITY;
            
            storage(KEYS.DEMO_USER, JSON.stringify(user));
            storage(KEYS.MODE, MODES.IDENTITY);
            
            // Ensure we have a fingerprint for identity mode
            if (!state.fingerprint) {
                state.fingerprint = await generateFingerprint();
                storage(KEYS.FINGERPRINT, state.fingerprint);
            }
            
            syncToCookies();
            
            window.dispatchEvent(new CustomEvent('tracking:login', { detail: { user, state, result } }));
            console.debug('[TrackingManager] Demo user logged in:', user, result);
            
            return { success: true, state, profileId: result.profileId };
        } catch (err) {
            console.error('[TrackingManager] Login error:', err);
            return { success: false, error: err.message };
        }
    }

    /**
     * Log out demo user
     * Calls server API to unlink session from profile.
     */
    async function logoutDemoUser() {
        const user = state.demoUser;
        
        try {
            // Call server API to unlink session
            await fetch('/api/demo-users/logout', {
                method: 'POST',
                headers: {
                    'X-Session-ID': state.sessionId
                }
            });
        } catch (err) {
            console.warn('[TrackingManager] Logout API call failed (continuing anyway):', err);
        }
        
        // Update local state regardless of API result
        state.demoUser = null;
        state.mode = MODES.FINGERPRINT; // Fall back to fingerprint mode
        
        storage(KEYS.DEMO_USER, null);
        storage(KEYS.MODE, MODES.FINGERPRINT);
        
        syncToCookies();
        
        window.dispatchEvent(new CustomEvent('tracking:logout', { detail: { user, state } }));
        console.debug('[TrackingManager] Demo user logged out');
        
        return state;
    }

    /**
     * Get HTTP headers for requests
     */
    function getHeaders() {
        const headers = {
            'X-Session-ID': state.sessionId,
            'X-Tracking-Mode': state.mode
        };
        
        if (state.fingerprint && state.mode !== MODES.NONE) {
            headers['X-Fingerprint'] = state.fingerprint;
        }
        
        if (state.demoUser) {
            headers['X-Demo-User'] = state.demoUser.id;
        }
        
        return headers;
    }

    /**
     * Reset all tracking data
     */
    function reset() {
        // Clear localStorage
        Object.values(KEYS).forEach(key => storage(key, null));
        
        // Clear cookies
        Object.values(COOKIES).forEach(name => deleteCookie(name));
        
        // Reset state
        state.mode = MODES.NONE;
        state.sessionId = generateSessionId();
        state.fingerprint = null;
        state.demoUser = null;
        
        storage(KEYS.SESSION_ID, state.sessionId);
        syncToCookies();
        
        window.dispatchEvent(new CustomEvent('tracking:reset', { detail: state }));
        console.debug('[TrackingManager] Reset complete');
        
        return state;
    }

    /**
     * Get human-readable mode description
     */
    function getModeInfo(mode = state.mode) {
        const info = {
            [MODES.NONE]: {
                name: 'Session Only',
                shortName: 'Session',
                icon: 'clock',
                color: 'gray',
                description: 'No persistent tracking. Your activity is only tracked during this browser session.',
                privacy: 'Maximum privacy. No data persists after you close the browser.',
                persistence: 'None - data lost when session ends'
            },
            [MODES.FINGERPRINT]: {
                name: 'Device Fingerprint',
                shortName: 'Fingerprint',
                icon: 'fingerprint',
                color: 'blue',
                description: 'Your device characteristics create a privacy-respecting identifier. No cookies required.',
                privacy: 'Good privacy. Uses only stable browser signals, no invasive tracking.',
                persistence: 'Cross-session on same device/browser'
            },
            [MODES.COOKIE]: {
                name: 'Cookie Tracking',
                shortName: 'Cookie',
                icon: 'cookie',
                color: 'yellow',
                description: 'A tracking cookie identifies you across sessions. Standard web tracking.',
                privacy: 'Standard privacy. Cookie can be cleared anytime.',
                persistence: 'Until cookie expires or is cleared'
            },
            [MODES.IDENTITY]: {
                name: 'Demo Account',
                shortName: 'Logged In',
                icon: 'user',
                color: 'green',
                description: 'You\'re logged in as a demo user with a pre-built profile.',
                privacy: 'Profile data is visible. This is a demo - no real personal data.',
                persistence: 'Linked to demo account'
            }
        };
        
        return info[mode] || info[MODES.NONE];
    }

    // Expose public API
    return {
        MODES,
        state,
        init,
        setMode,
        loginDemoUser,
        logoutDemoUser,
        getHeaders,
        reset,
        getModeInfo,
        detectPrivacyFeatures,
        generateFingerprint
    };
})();

// Make available globally
window.TrackingManager = TrackingManager;

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => TrackingManager.init());
} else {
    TrackingManager.init();
}
