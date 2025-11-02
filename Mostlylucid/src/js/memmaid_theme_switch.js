// Expose an explicit, used export to prevent tree-shaking from removing this module
// This module wires up Mermaid rendering to the app's theme switch events.

const elementCode = 'div.mermaid';

const loadMermaid = async (theme) => {
    const mm = window.mermaid; // rely on global set in main.js
    mm.initialize({ startOnLoad: false, theme: theme });
    console.log('Loading mermaid with theme:', theme);
    await mm.run({
        querySelector: elementCode,
    });
};

const saveOriginalData = async () => {
    try {
        console.log('Saving original data');
        const elements = document.querySelectorAll(elementCode);
        const count = elements.length;
        if (count === 0) return;

        const promises = Array.from(elements).map((element) => {
            if (element.getAttribute('data-processed') != null) {
                console.log('Element already processed');
                return;
            }
            element.setAttribute('data-original-code', element.innerHTML);
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
        const elements = document.querySelectorAll(elementCode);
        const count = elements.length;
        if (count === 0) return;

        const promises = Array.from(elements).map((element) => {
            if (element.getAttribute('data-original-code') != null) {
                element.removeAttribute('data-processed');
                element.innerHTML = element.getAttribute('data-original-code');
            } else {
                console.log('Element already reset');
            }
        });

        await Promise.all(promises);
    } catch (error) {
        console.error(error);
        throw error;
    }
};

export async function initMermaid() {
    const mermaidElements = document.querySelectorAll(elementCode);
    if (mermaidElements.length === 0) return;

    try {
        await saveOriginalData();
    } catch (error) {
        console.error('Error saving original data:', error);
        return; // Early exit if saveOriginalData fails
    }

    const handleDarkThemeSet = async () => {
        try {
            await resetProcessed();
            await loadMermaid('dark');
            console.log('Dark theme set');
        } catch (error) {
            console.error('Error during dark theme set:', error);
        }
    };

    const handleLightThemeSet = async () => {
        try {
            await resetProcessed();
            await loadMermaid('default');
            console.log('Light theme set');
        } catch (error) {
            console.error('Error during light theme set:', error);
        }
    };

    document.body.removeEventListener('dark-theme-set', handleDarkThemeSet);
    document.body.removeEventListener('light-theme-set', handleLightThemeSet);
    document.body.addEventListener('dark-theme-set', handleDarkThemeSet);
    document.body.addEventListener('light-theme-set', handleLightThemeSet);

    const isDarkMode = localStorage.theme === 'dark';
    await loadMermaid(isDarkMode ? 'dark' : 'default').then(() => console.log('Initial load complete'));
}