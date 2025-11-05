// Mermaid theme/initialization helper with theme-aware rendering and normalization
'use strict';

const elementSelector = 'div.mermaid, pre.mermaid';
let mediaQueryList = null;
let mediaChangeHandler = null;
let darkThemeHandlerRef = null;
let lightThemeHandlerRef = null;
let listenersAttached = false;

const normalizeMermaidCodeFences = () => {
    // Convert <pre><code class="language-mermaid">...</code></pre> into <pre class="mermaid">...</pre>
    const codeBlocks = document.querySelectorAll('pre > code.language-mermaid');
    codeBlocks.forEach(code => {
        const pre = code.parentElement;
        if (!pre) return;
        if (pre.classList && pre.classList.contains('mermaid')) {
            // Already normalized
            return;
        }
        const text = code.textContent;
        // Replace inner content with raw text and add class
        pre.textContent = text;
        pre.classList.add('mermaid');
    });
};

const loadMermaid = async (theme) => {
    if (!window.mermaid) return;
    try {
        window.mermaid.initialize({
            startOnLoad: false,
            theme,
            // Force transparent background so container styles (light/dark) show through
            themeVariables: {
                background: 'transparent'
            }
        });
        await window.mermaid.run({
            querySelector: elementSelector,
        });
    } catch (err) {
        // Swallow errors to avoid breaking page/tests; log for debugging
        console.error('Mermaid render error:', err);
    }
};

const saveOriginalData = async () => {
    const elements = document.querySelectorAll(elementSelector);
    if (elements.length === 0) return;
    elements.forEach((el) => {
        if (el.getAttribute('data-processed') != null) return;
        // Store the raw text content so special characters aren't HTML-escaped
        el.setAttribute('data-original-code', el.textContent);
    });
};

const resetProcessed = async () => {
    const elements = document.querySelectorAll(elementSelector);
    if (elements.length === 0) return;
    elements.forEach((el) => {
        if (el.getAttribute('data-original-code') != null) {
            el.removeAttribute('data-processed');
            el.innerHTML = el.getAttribute('data-original-code');
        }
    });
};

export async function initMermaid() {
    // Normalize any code fences first so they get picked up
    normalizeMermaidCodeFences();

    const mermaidElements = document.querySelectorAll(elementSelector);
    if (mermaidElements.length === 0) return;

    try {
        await saveOriginalData();
    } catch (error) {
        console.error('Error saving original data:', error);
        return;
    }

    const handleDarkThemeSet = async () => {
        try {
            await resetProcessed();
            await loadMermaid('dark');
        } catch (error) {
            console.error('Error during dark theme set:', error);
        }
    };

    const handleLightThemeSet = async () => {
        try {
            await resetProcessed();
            await loadMermaid('default');
        } catch (error) {
            console.error('Error during light theme set:', error);
        }
    };

    // Remove previous listeners using saved references (prevents duplicates)
    if (listenersAttached) {
        if (darkThemeHandlerRef) document.body.removeEventListener('dark-theme-set', darkThemeHandlerRef);
        if (lightThemeHandlerRef) document.body.removeEventListener('light-theme-set', lightThemeHandlerRef);
    }

    darkThemeHandlerRef = handleDarkThemeSet;
    lightThemeHandlerRef = handleLightThemeSet;

    document.body.addEventListener('dark-theme-set', darkThemeHandlerRef);
    document.body.addEventListener('light-theme-set', lightThemeHandlerRef);
    listenersAttached = true;

    // OS theme change listener
    try {
        if (typeof window.matchMedia === 'function') {
            // Remove previous
            if (mediaQueryList && mediaChangeHandler) {
                mediaQueryList.removeEventListener('change', mediaChangeHandler);
            }
            mediaQueryList = window.matchMedia('(prefers-color-scheme: dark)');
            mediaChangeHandler = async (e) => {
                try {
                    await resetProcessed();
                    await loadMermaid(e.matches ? 'dark' : 'default');
                } catch (err) {
                    console.error('Error handling OS theme change:', err);
                }
            };
            mediaQueryList.addEventListener('change', mediaChangeHandler);
        }
    } catch (e) {
        // Non-fatal in test envs
    }

    const isDarkMode = localStorage.theme === 'dark';
    await loadMermaid(isDarkMode ? 'dark' : 'default');
}

// Preserve global for legacy calls
if (typeof window !== 'undefined') {
    window.initMermaid = initMermaid;
}