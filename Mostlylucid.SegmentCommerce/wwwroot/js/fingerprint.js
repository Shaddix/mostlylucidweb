/**
 * Fingerprint.js - Zero-cookie device fingerprinting
 * 
 * This script generates a device fingerprint and stores it in localStorage.
 * The session ID is managed by session-store.js (also in localStorage).
 * 
 * The fingerprint is used for probabilistic device identification when:
 * - User is not logged in (no identity-based tracking)
 * - Session expires but same device returns
 */
(() => {
    const FP_STORAGE_KEY = "sc_fingerprint";
    const FP_COOKIE = "sc_fp"; // Still set cookie as fallback for server-side reading

    if (typeof document === "undefined" || typeof localStorage === "undefined") {
        return;
    }

    function setCookie(name, value, days) {
        const expires = new Date(Date.now() + days * 24 * 60 * 60 * 1000).toUTCString();
        const secure = window.location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `${name}=${value}; Path=/; SameSite=Lax; Expires=${expires}${secure}`;
    }

    async function sha256Hex(input) {
        const encoder = new TextEncoder();
        const data = encoder.encode(input);
        const hash = await crypto.subtle.digest("SHA-256", data);
        const bytes = Array.from(new Uint8Array(hash));
        return bytes.map(b => b.toString(16).padStart(2, "0")).join("");
    }

    function randomId() {
        return (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/-/g, "");
    }

    /**
     * Generate device fingerprint from browser characteristics
     */
    async function generateFingerprint() {
        const fpParts = [
            navigator.userAgent || "",
            navigator.language || "",
            navigator.platform || "",
            screen.width + "x" + screen.height,
            screen.colorDepth || "",
            Intl.DateTimeFormat().resolvedOptions().timeZone || "",
            new Date().getTimezoneOffset()
        ];

        let hash;
        try {
            hash = await sha256Hex(fpParts.join("|"));
        } catch (err) {
            console.warn("[Fingerprint] Hash failed; using random id", err);
            hash = randomId();
        }

        return hash;
    }

    /**
     * Ensure fingerprint exists in localStorage (and cookie as fallback)
     */
    async function ensureFingerprint() {
        let fingerprint = localStorage.getItem(FP_STORAGE_KEY);
        
        if (!fingerprint) {
            fingerprint = await generateFingerprint();
            localStorage.setItem(FP_STORAGE_KEY, fingerprint);
            console.debug("[Fingerprint] Generated new fingerprint");
        }
        
        // Also set cookie for server-side reading (FingerprintController)
        setCookie(FP_COOKIE, fingerprint, 30);
        
        return fingerprint;
    }

    /**
     * Get fingerprint synchronously (returns null if not yet generated)
     */
    function getFingerprint() {
        return localStorage.getItem(FP_STORAGE_KEY);
    }

    // Initialize fingerprint
    ensureFingerprint();

    // Expose for other scripts if needed
    window.SegmentFingerprint = {
        get: getFingerprint,
        ensure: ensureFingerprint
    };
})();
