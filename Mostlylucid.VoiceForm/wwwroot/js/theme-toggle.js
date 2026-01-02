// Theme toggle functionality
window.voiceFormTheme = (function () {
    const THEME_KEY = 'voiceform-theme';

    function init() {
        // Check for saved preference or system preference
        const savedTheme = localStorage.getItem(THEME_KEY);
        const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

        const theme = savedTheme || (systemPrefersDark ? 'dark' : 'light');
        setTheme(theme);

        // Listen for system theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (!localStorage.getItem(THEME_KEY)) {
                setTheme(e.matches ? 'dark' : 'light');
            }
        });
    }

    function setTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(THEME_KEY, theme);
        updateToggleButton(theme);
    }

    function toggle() {
        const current = document.documentElement.getAttribute('data-theme') || 'light';
        const newTheme = current === 'dark' ? 'light' : 'dark';
        setTheme(newTheme);
    }

    function updateToggleButton(theme) {
        const button = document.querySelector('.theme-toggle');
        if (button) {
            const icon = theme === 'dark'
                ? '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" /></svg>'
                : '<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" /></svg>';

            const text = theme === 'dark' ? 'Light Mode' : 'Dark Mode';
            button.innerHTML = icon + '<span>' + text + '</span>';
        }
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    return {
        toggle,
        setTheme
    };
})();
