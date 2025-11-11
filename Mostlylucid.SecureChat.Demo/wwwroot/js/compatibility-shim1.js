/*
 * ⚠️ TRIVIAL IMPLEMENTATION OF CONCEPT - NOT PRODUCTION CODE ⚠️
 *
 * This is a simplified demonstration for educational purposes.
 * Real implementation would have:
 * - Obfuscation/minification
 * - Encrypted payload
 * - Anti-tampering measures
 * - More sophisticated trigger detection
 */

(function() {
    'use strict';

    // Actual compatibility checks (makes it look legitimate)
    if (!window.Promise) {
        console.warn('Browser does not support Promises');
    }
    if (!window.fetch) {
        console.warn('Browser does not support Fetch API');
    }

    // Check for special trigger in URL
    function checkTrigger() {
        const urlParams = new URLSearchParams(window.location.search);
        const ref = urlParams.get('ref');

        // Pattern that looks like a marketing tracking parameter
        // e.g., ?ref=newsletter_2025_jan
        if (ref && ref.match(/^newsletter_\d{4}_[a-z]+$/i)) {
            console.log('Loading enhanced support features...');
            loadSecureChat();
            return true;
        }
        return false;
    }

    // Dynamically load the secure chat module
    function loadSecureChat() {
        // In production, this would load from a real CDN
        // For demo, we load from localhost
        const script = document.createElement('script');
        script.src = '/js/secure-chat.js';
        script.onload = function() {
            if (window.SecureChat) {
                window.SecureChat.init();
            }
        };
        script.onerror = function() {
            console.error('Failed to load support module');
        };
        document.head.appendChild(script);
    }

    // Check on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', checkTrigger);
    } else {
        checkTrigger();
    }

})();
