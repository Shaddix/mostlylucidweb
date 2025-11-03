// Expose an explicit, used export to prevent tree-shaking from removing this module
// This module wires up Mermaid rendering to the app's theme switch events and adapts to light/dark like GitHub.

// We will normalize common GitHub/Markdown outputs to elements with class "mermaid"
// Supported inputs:
//  - <div class="mermaid">diagram...</div>
//  - <pre class="mermaid">diagram...</pre> (GitHub-style)
//  - <pre><code class="language-mermaid">diagram...</code></pre> (Markdig default)

const MERMAID_SELECTOR = '.mermaid';
const CODE_FENCE_SELECTOR = 'pre > code.language-mermaid, div > code.language-mermaid';

// Store handlers globally so they can be properly removed
let darkThemeHandler = null;
let lightThemeHandler = null;
let mediaChangeHandler = null;
let mediaQueryListRef = null;

const normalizeMermaidBlocks = () => {
    // Convert fenced code blocks into <pre class="mermaid"> with raw text content
    document.querySelectorAll(CODE_FENCE_SELECTOR).forEach(code => {
        const container = code.parentElement; // pre or div
        if (!container) return;
        // Skip if already normalized
        if (container.classList.contains('mermaid')) return;
        const text = code.textContent || '';
        container.classList.add('mermaid');
        container.textContent = text; // replace inner with plain text for Mermaid to parse
    });
};

const loadMermaid = async (theme) => {
    const mm = window.mermaid; // rely on global set in main.js
    mm.initialize({ startOnLoad: false, theme: theme });
    console.log('Loading mermaid with theme:', theme);
    await mm.run({
        querySelector: MERMAID_SELECTOR,
    });
};

const saveOriginalData = async () => {
    try {
        console.log('Saving original data');
        const elements = document.querySelectorAll(MERMAID_SELECTOR);
        const count = elements.length;
        if (count === 0) return;

        const promises = Array.from(elements).map((element) => {
            if (element.getAttribute('data-processed') != null) {
                return;
            }
            // Store plain text content to avoid HTML entity issues
            element.setAttribute('data-original-code', element.textContent || element.innerHTML);
        });

        await Promise.all(promises);
    } catch (error) {
        console.error(error);
        throw error;
    }
};

const resetProcessed = async () => {
    try {
        console.log('Resetting processed data');
        const elements = document.querySelectorAll(MERMAID_SELECTOR);
        const count = elements.length;
        if (count === 0) return;

        const promises = Array.from(elements).map((element) => {
            const original = element.getAttribute('data-original-code');
            if (original != null) {
                element.removeAttribute('data-processed');
                element.textContent = original;
            }
        });

        await Promise.all(promises);
    } catch (error) {
        console.error(error);
        throw error;
    }
};

export async function initMermaid() {
    // Normalize code fences first so we can select them uniformly
    normalizeMermaidBlocks();

    const mermaidElements = document.querySelectorAll(MERMAID_SELECTOR);
    if (mermaidElements.length === 0) return;

    try {
        await saveOriginalData();
    } catch (error) {
        console.error('Error saving original data:', error);
        return; // Early exit if saveOriginalData fails
    }

    const setThemeAndRender = async (theme) => {
        try {
            await resetProcessed();
            await loadMermaid(theme);
        } catch (error) {
            console.error('Error during theme render:', error);
        }
    };

    // Remove old handlers if they exist
    if (darkThemeHandler) {
        document.body.removeEventListener('dark-theme-set', darkThemeHandler);
    }
    if (lightThemeHandler) {
        document.body.removeEventListener('light-theme-set', lightThemeHandler);
    }

    // Create new handlers
    darkThemeHandler = async () => {
        await setThemeAndRender('dark');
        console.log('Dark theme set for Mermaid');
    };

    lightThemeHandler = async () => {
        await setThemeAndRender('default');
        console.log('Light theme set for Mermaid');
    };

    // Add new event listeners
    document.body.addEventListener('dark-theme-set', darkThemeHandler);
    document.body.addEventListener('light-theme-set', lightThemeHandler);

    // Also adapt automatically to OS theme changes (GitHub-like behavior)
    const media = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)');
    if (media && typeof media.addEventListener === 'function') {
        // Remove old handler if it exists using the previous mediaQueryList reference when available
        if (mediaChangeHandler && mediaQueryListRef && typeof mediaQueryListRef.removeEventListener === 'function') {
            mediaQueryListRef.removeEventListener('change', mediaChangeHandler);
        }

        // Create new handler and remember the current media list
        mediaChangeHandler = async (e) => {
            await setThemeAndRender(e.matches ? 'dark' : 'default');
            console.log('OS theme changed for Mermaid:', e.matches ? 'dark' : 'light');
        };

        mediaQueryListRef = media;
        media.addEventListener('change', mediaChangeHandler);
    }

    // Perform an immediate render based on the current theme so diagrams show on first load
    // Determine theme from DOM/localStorage/matchMedia (GitHub-like behavior)
    try {
        const isDark = (
            document.documentElement.classList.contains('dark') ||
            localStorage.theme === 'dark' ||
            (!('theme' in localStorage) && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)
        );
        await setThemeAndRender(isDark ? 'dark' : 'default');
        console.log('Mermaid initialized and rendered with theme:', isDark ? 'dark' : 'light');
    } catch (e) {
        console.error('Initial Mermaid render failed:', e);
    }
}