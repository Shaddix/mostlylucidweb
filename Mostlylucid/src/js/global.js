export  function globalSetup() {


    return {
        isMobileMenuOpen: false,
        isDarkMode: false,
        // Function to initialize the theme based on localStorage or system preference
        themeInit() {
            if (
                localStorage.theme === "dark" ||
                (!("theme" in localStorage) &&
                    window.matchMedia("(prefers-color-scheme: dark)").matches)
            ) {
                localStorage.theme = "dark";
                document.documentElement.classList.add("dark");
                document.documentElement.classList.remove("light");
                this.isDarkMode = true;

                // Store theme state globally for late-loading modules (Cloudflare fix)
                window.__themeState = 'dark';

                // Dispatch dark theme event for Mermaid and other listeners
                // Use setTimeout to ensure listeners have time to register
                setTimeout(() => {
                    document.body.dispatchEvent(new CustomEvent('dark-theme-set'));
                }, 0);
            } else {
                localStorage.theme = "base";
                document.documentElement.classList.remove("dark");
                document.documentElement.classList.add("light");
                this.isDarkMode = false;

                // Store theme state globally for late-loading modules (Cloudflare fix)
                window.__themeState = 'light';

                // Dispatch light theme event for Mermaid and other listeners
                // Use setTimeout to ensure listeners have time to register
                setTimeout(() => {
                    document.body.dispatchEvent(new CustomEvent('light-theme-set'));
                }, 0);
            }
            this.applyTheme();
        },

        // Function to switch the theme and update the stylesheets accordingly
        themeSwitch() {
            if (localStorage.theme === "dark") {
                localStorage.theme = "light";
                window.__themeState = 'light';
                document.body.dispatchEvent(new CustomEvent('light-theme-set'));
                document.documentElement.classList.remove("dark");
                document.documentElement.classList.add("light");
                this.isDarkMode = false;
            } else {
                localStorage.theme = "dark";
                window.__themeState = 'dark';
                document.body.dispatchEvent(new CustomEvent('dark-theme-set'));
                document.documentElement.classList.add("dark");
                document.documentElement.classList.remove("light");
                this.isDarkMode = true;
            }
            this.applyTheme();
        },

        // No longer needed - CSS handles theme switching automatically via .dark class
        applyTheme() {
            // Theme switching is now handled purely by CSS
            // The .dark class on <html> triggers all dark mode styles
        }
    };
}