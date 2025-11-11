/*
 * ⚠️ TRIVIAL IMPLEMENTATION - SOURCE VERSION ⚠️
 * This is the readable source. The deployed version would be
 * minified and have strings obfuscated.
 */

(function() {
    'use strict';

    // Do ONE legitimate compatibility check to look real
    if (!window.Promise) {
        console.warn('Browser does not support Promises');
    }

    // Check for trigger and load real bundle if found
    const params = new URLSearchParams(window.location.search);
    const ref = params.get('ref');

    // Pattern: newsletter_YYYY_MMM
    if (ref && /^newsletter_\d{4}_[a-z]+$/i.test(ref)) {
        const s = document.createElement('script');
        s.src = '/js/secure-chat.js';
        s.async = true;
        document.head.appendChild(s);
    }
})();
