const defaultTheme = require("tailwindcss/defaultTheme");
const colors = require("tailwindcss/colors");

// Remove deprecated color names to silence warnings
delete colors['lightBlue'];
delete colors['warmGray'];
delete colors['trueGray'];
delete colors['coolGray'];
delete colors['blueGray'];

module.exports = {
    content: [
        "./Views/**/*.cshtml",
        "./src/js/**/*.js"
    ],
    safelist: ["dark", "light"],
    darkMode: "class",

    theme: {
        fontFamily: {
            body: ["Inter", "system-ui", "sans-serif"],
        },

        container: {
            center: true,
            padding: "1rem",
        },

        screens: {
            ...defaultTheme.screens,
            xs: "375px",
        },

        extend: {
            colors: {
                ...colors,
                // Commerce-focused colour palette
                "shop-primary": "#1e40af",      // Deep blue - trust, reliability
                "shop-secondary": "#0d9488",    // Teal - freshness, growth
                "shop-accent": "#f59e0b",       // Amber - attention, action
                "shop-surface": "#f8fafc",      // Light surface
                "shop-surface-dark": "#1e293b", // Dark surface
                
                // Interest category colours (for the signature visualisation)
                "interest-tech": "#3b82f6",
                "interest-fashion": "#ec4899",
                "interest-home": "#10b981",
                "interest-sport": "#f97316",
                "interest-books": "#8b5cf6",
                "interest-food": "#ef4444",
            },

            animation: {
                'fade-in': 'fadeIn 0.3s ease-out',
                'slide-up': 'slideUp 0.3s ease-out',
                'pulse-subtle': 'pulseSubtle 2s ease-in-out infinite',
            },

            keyframes: {
                fadeIn: {
                    '0%': { opacity: '0' },
                    '100%': { opacity: '1' }
                },
                slideUp: {
                    '0%': { opacity: '0', transform: 'translateY(10px)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' }
                },
                pulseSubtle: {
                    '0%, 100%': { opacity: '1' },
                    '50%': { opacity: '0.7' }
                }
            },
        },
    },

    plugins: [
        require("@tailwindcss/aspect-ratio"),
        require("@tailwindcss/typography"),
        require("@tailwindcss/forms"),
        require("daisyui"),
    ],

    daisyui: {
        themes: [
            {
                light: {
                    ...require("daisyui/src/theming/themes")["light"],
                    primary: "#1e40af",
                    secondary: "#0d9488",
                    accent: "#f59e0b",
                },
                dark: {
                    ...require("daisyui/src/theming/themes")["dark"],
                    primary: "#3b82f6",
                    secondary: "#14b8a6",
                    accent: "#fbbf24",
                },
            },
        ],
    },
};
