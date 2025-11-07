/**
 * Mermaid Theme Switcher
 *
 * Automatically switches Mermaid diagram themes based on light/dark mode
 * Supports OS-level theme changes and manual theme switching
 *
 * @module theme-switcher
 */

import { enhanceMermaidDiagrams } from './enhancements.js';
import type { Theme } from './types';

const elementSelector = 'div.mermaid, pre.mermaid';
let mediaQueryList: MediaQueryList | null = null;
let mediaChangeHandler: ((e: MediaQueryListEvent) => void) | null = null;
let darkThemeHandlerRef: (() => Promise<void>) | null = null;
let lightThemeHandlerRef: (() => Promise<void>) | null = null;
let listenersAttached = false;

/**
 * Normalize code fence blocks to Mermaid-compatible format
 * Converts <pre><code class="language-mermaid">...</code></pre> into <pre class="mermaid">...</pre>
 */
const normalizeMermaidCodeFences = () => {
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

/**
 * Load and render Mermaid diagrams with the specified theme
 * @param {string} theme - Theme name ('dark' or 'default')
 */
const loadMermaid = async (theme: Theme): Promise<void> => {
    if (!window.mermaid) {
        console.warn('Mermaid library not found. Make sure mermaid is loaded before calling initMermaid()');
        return;
    }

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

        // Enhance diagrams with pan/zoom and export after rendering completes
        // Use requestAnimationFrame for better timing
        await new Promise<void>(resolve => {
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    enhanceMermaidDiagrams();
                    resolve();
                });
            });
        });
    } catch (err) {
        console.error('Mermaid render error:', err);
    }
};

/**
 * Save original diagram source code for re-rendering
 */
const saveOriginalData = (): void => {
    const elements = document.querySelectorAll(elementSelector);
    if (elements.length === 0) return;

    elements.forEach((el) => {
        if (el.getAttribute('data-processed') != null) return;
        // Store the raw text content so special characters aren't HTML-escaped
        const textContent = el.textContent;
        if (textContent) {
            el.setAttribute('data-original-code', textContent);
        }
    });
};

/**
 * Reset processed diagrams to their original source for re-rendering
 */
const resetProcessed = (): void => {
    const elements = document.querySelectorAll(elementSelector);
    if (elements.length === 0) return;

    elements.forEach((el) => {
        const originalCode = el.getAttribute('data-original-code');
        if (originalCode != null) {
            el.removeAttribute('data-processed');
            el.innerHTML = originalCode;
        }
    });
};

/**
 * Initialize Mermaid with automatic theme switching support
 *
 * This function:
 * - Normalizes code fence blocks
 * - Sets up theme change listeners
 * - Handles OS-level theme preferences
 * - Re-renders diagrams when theme changes
 * - Applies enhancements (pan/zoom, export, fullscreen)
 *
 * @returns {Promise<void>}
 *
 * @example
 * // Basic usage
 * import { initMermaid } from '@mostlylucid/mermaid-enhancements';
 *
 * document.addEventListener('DOMContentLoaded', async () => {
 *   await initMermaid();
 * });
 *
 * @example
 * // With custom theme switching events
 * import { initMermaid } from '@mostlylucid/mermaid-enhancements';
 *
 * await initMermaid();
 *
 * // Trigger theme change
 * document.body.dispatchEvent(new Event('dark-theme-set'));
 * document.body.dispatchEvent(new Event('light-theme-set'));
 */
export async function initMermaid() {
    // Normalize any code fences first so they get picked up
    normalizeMermaidCodeFences();

    const mermaidElements = document.querySelectorAll(elementSelector);
    if (mermaidElements.length === 0) return;

    try {
        saveOriginalData();
    } catch (error) {
        console.error('Error saving original data:', error);
        return;
    }

    const handleDarkThemeSet = async (): Promise<void> => {
        try {
            resetProcessed();
            await loadMermaid('dark');
        } catch (error) {
            console.error('Error during dark theme set:', error);
        }
    };

    const handleLightThemeSet = async (): Promise<void> => {
        try {
            resetProcessed();
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

    // Listen for custom theme change events
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
            mediaChangeHandler = async (e: MediaQueryListEvent): Promise<void> => {
                try {
                    resetProcessed();
                    await loadMermaid(e.matches ? 'dark' : 'default');
                } catch (err) {
                    console.error('Error handling OS theme change:', err);
                }
            };
            mediaQueryList.addEventListener('change', mediaChangeHandler);
        }
    } catch (e) {
        // Non-fatal in test environments
    }

    // Detect current theme with multiple fallbacks
    let isDarkMode = false;

    // Check global state (if set by your theme system)
    if (typeof window.__themeState !== 'undefined') {
        isDarkMode = window.__themeState === 'dark';
    }
    // Check localStorage
    else if (typeof localStorage !== 'undefined' && localStorage.theme) {
        isDarkMode = localStorage.theme === 'dark';
    }
    // Check DOM class
    else if (document.documentElement.classList.contains('dark')) {
        isDarkMode = true;
    }
    // Check OS preference
    else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        isDarkMode = true;
    }

    await loadMermaid(isDarkMode ? 'dark' : 'default');
}
