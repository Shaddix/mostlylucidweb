// Initialize the mostlylucid namespace if not already defined
import hljsRazor from "highlightjs-cshtml-razor";
window.mostlylucid = window.mostlylucid || {};
import mermaid from "mermaid";
import Alpine from 'alpinejs';
import htmx from "htmx.org";
import hljs from "highlight.js";
import EasyMDE from "easymde";
import 'easymde/dist/easymde.min.css';
import '../css/easymde-overrides.css';

window.EasyMDE = EasyMDE;

window.Alpine = Alpine;
window.hljs=hljs;
window.htmx = htmx;
window.mermaid=mermaid;
window.mermaid.initialize({startOnLoad : false});
// Importing modules
import { typeahead } from "./typeahead";
import { submitTranslation, viewTranslation } from "./translations";
import { codeeditor } from "./simplemde_editor";
import { globalSetup } from "./global";
import  {comments} from  "./comments"; 
import { initMermaid } from "./memmaid_theme_switch";

window.mostlylucid.comments = comments();

// Attach imported modules to the mostlylucid namespace
window.mostlylucid.typeahead = typeahead;
window.mostlylucid.translations = {
    submitTranslation: submitTranslation,
    viewTranslation: viewTranslation
};
window.mostlylucid.simplemde = codeeditor(); // Assuming simplemde() returns the instance
window.globalSetup = globalSetup;

function setLogoutLink(container = document) {
    // Get the logout link - search within container for HTMX swaps
    var logoutLink = container.querySelector('a[data-logout-link]');

    if (logoutLink) {
        try {
            // Get the current URL
            var currentUrl = window.location.href;

            // Check if link already has the return URL set to avoid duplicate updates
            if (logoutLink.href.includes('returnUrl=' + encodeURIComponent(currentUrl))) {
                return;
            }

            // Update the href attribute to include the return URL
            var baseUrl = logoutLink.href.split('?')[0]; // Get the base URL without query parameters
            logoutLink.href = baseUrl + '?returnUrl=' + encodeURIComponent(currentUrl);

            console.log('Logout link updated:', logoutLink.href);
        } catch (error) {
            console.error('Error setting logout link:', error);
        }
    }
}

// Make setLogoutLink available globally for debugging and manual calls if needed
window.setLogoutLink = setLogoutLink;

window.mermaidinit =async  function() {

    try {
      await  window.initMermaid().then(r => console.log('Mermaid initialized'));
    } catch (e) {
        console.error('Failed to initialize Mermaid:', e);
    }

}


function highlightCodeBlocks(container = document) {
    // Only highlight code blocks that haven't been highlighted yet
    const codeBlocks = container.querySelectorAll('pre code:not(.hljs)');
    codeBlocks.forEach((block) => {
        hljs.highlightElement(block);
    });
}

// Cloudflare-safe HTMX event listener registration
function registerHTMXListener() {
    // Defensive check: Only register if htmx is available
    if (typeof window.htmx === 'undefined') {
        console.warn('HTMX not loaded yet, retrying in 100ms...');
        setTimeout(registerHTMXListener, 100);
        return;
    }

    // Only trigger updates after HTMX swaps content in #contentcontainer or #commentlist
    document.body.addEventListener('htmx:afterSettle', async function(evt) {
        const targetId = evt.detail.target.id;
        if (targetId !== 'contentcontainer' && targetId !== 'commentlist' && targetId!=="blogpost") {
            console.log("Ignoring swap event for target:", targetId);
            return;
        }

        console.log('HTMX afterSettle triggered for:', targetId);

        // Re-initialize Google Sign-In button if present in swapped content
        initGoogleSignIn();

        // Re-initialize Mermaid diagrams in the new content
        await mermaidinit();

        // Only highlight new code blocks in the swapped content
        highlightCodeBlocks(evt.detail.target);

        // Update logout link in the swapped content (search entire document in case it's in nav)
        setLogoutLink(document);
    }, { once: false }); // Don't use once - we need this for all HTMX swaps

    console.log('HTMX event listener registered successfully');
}

async function initializePage() {
    initGoogleSignIn();

    // Wait for Alpine to be ready before initializing Mermaid
    // This ensures themeInit() has run and the theme event has been fired
    await new Promise(resolve => {
        if (window.Alpine && window.Alpine.version) {
            resolve();
        } else {
            document.addEventListener('alpine:init', resolve, { once: true });
        }
    });

    // Now initialize Mermaid after theme is set
    await mermaidinit();

    const hljsRazor = require('highlightjs-cshtml-razor');
    hljs.registerLanguage("cshtml-razor", hljsRazor);
    highlightCodeBlocks();
    setLogoutLink();
    updateMetaUrls();

    console.log('Document is ready');

    // Register HTMX listener with Cloudflare-safe retry mechanism
    registerHTMXListener();
}

// Handle both cases: module loaded before or after page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        initializePage().catch(err => {
            console.error('Failed to initialize page:', err);
        });
    });
} else {
    // DOM already loaded, run immediately
    initializePage().catch(err => {
        console.error('Failed to initialize page:', err);
    });
}






function updateMetaUrls() {
    var currentUrl = window.location.href;

    // Set the current URL in the og:url and twitter:url meta tags
    document.getElementById('metaOgUrl').setAttribute('content', currentUrl);
    document.getElementById('metaTwitterUrl').setAttribute('content', currentUrl);
}

function renderButton(element) {
    // Check if the button has already been initialized
    if (!element.getAttribute('data-google-rendered')) {
        google.accounts.id.renderButton(
            element,
            {
                type: "standard",
                size: "large",
                width: 200,
                theme: "filled_black",
                text: "sign_in_with",
                shape: "rectangular",
                logo_alignment: "left"
            }
        );
        // Mark the element as initialized
        element.setAttribute('data-google-rendered', 'true');
    }
}

function initGoogleSignIn() {
    google.accounts.id.initialize({
        client_id: "839055275161-u7dqn2oco2729n6i5mk0fe7gap0bmg6g.apps.googleusercontent.com",
        callback: handleCredentialResponse
    });
    const element = document.getElementById('google_button');
    if (element) {
        renderButton(element);
    }
}

function handleCredentialResponse(response) {
    console.log('Handling credential response:', response);

    if (response.credential) {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/login', true);
        xhr.setRequestHeader('Content-Type', 'application/json');
        xhr.onload = function () {
            if (xhr.status === 200) {
                console.log('Login successful, reloading page...');
                window.location.reload();  // Ensure this is only triggered once
            } else {
                console.error('Failed to log in.');
            }
        };
        xhr.send(JSON.stringify({ idToken: response.credential }));
    } else {
        console.error('No credential in response.');
    }
}



hljs.addPlugin({
    "after:highlightElement": ({ el, text }) => {
        const wrapper = el.parentElement;
        if (wrapper == null) {
            return;
        }

        /**
         * Make the parent relative so we can absolutely
         * position the copy button
         */
        wrapper.classList.add("relative");
        const copyButton = document.createElement("button");
        copyButton.classList.add(
            "absolute",
            "top-2",
            "right-1",
            "p-2",
            "text-gray-500",
            "hover:text-gray-700",
            "bx",
            "bx-copy",
            "text-xl",
            "cursor-pointer"
        );
        copyButton.setAttribute("aria-label", "Copy code to clipboard");
        copyButton.setAttribute("title", "Copy code to clipboard");

        copyButton.onclick = () => {
            navigator.clipboard.writeText(text);

            // Notify user that the content has been copied
            showToast("The code block content has been copied to the clipboard.", 3000, "success");
        
        };
        // Append the copy button to the wrapper
        wrapper.prepend(copyButton);
    },
});

window.showToast = function(message, duration = 3000, type = 'success') {
    const toast = document.getElementById('toast');
    const toastText = document.getElementById('toast-text');
    const toastMessage = document.getElementById('toast-message');

    // Set message and type
    toastText.innerText = message;
    toastMessage.className = `alert alert-${type}`; // Change alert type (success, warning, error)

    // Show the toast
    toast.classList.remove('hidden');

    // Hide the toast after specified duration
    setTimeout(() => {
        toast.classList.add('hidden');
    }, duration);
}

Alpine.start();