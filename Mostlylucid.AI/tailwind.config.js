const defaultTheme = require("tailwindcss/defaultTheme");

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
                // AI-themed color palette
                "primary": "#1a1a2e",           // Dark navy for text/headers
                "secondary": "#6366f1",         // Indigo for buttons/links
                "accent": "#8b5cf6",            // Purple for hover states
                "ai-blue": "#4f46e5",           // Gradient start
                "ai-purple": "#7c3aed",         // Gradient end

                // Light mode
                "light-bg": "#f8fafc",          // Light background
                "light-surface": "#ffffff",     // Card background
                "light-border": "#e2e8f0",      // Border color

                // Dark mode
                "dark-bg": "#0f172a",           // Dark background (slate-900)
                "dark-surface": "#1e293b",      // Dark card background (slate-800)
                "dark-border": "#334155",       // Dark border (slate-700)

                // Custom colors
                "custom-light-bg": "#f8fafc",
                "custom-dark-bg": "#0f172a",
            },

            backgroundImage: {
                "ai-gradient": "linear-gradient(135deg, #4f46e5 0%, #7c3aed 100%)",
                "ai-gradient-dark": "linear-gradient(135deg, #3730a3 0%, #5b21b6 100%)",
            },

            typography: (theme) => ({
                DEFAULT: {
                    css: {
                        color: theme("colors.primary"),
                        a: {
                            fontWeight: theme("fontWeight.semibold"),
                            color: theme("colors.secondary"),
                            textDecoration: "underline",
                            transition: "color 300ms",
                            "&:hover": {
                                color: theme("colors.accent"),
                            },
                        },
                        "p, li": {
                            fontWeight: theme("fontWeight.light"),
                        },
                        h1: { fontSize: theme("fontSize.3xl") },
                        h2: { fontSize: theme("fontSize.2xl") },
                        h3: { fontSize: theme("fontSize.xl") },
                        h4: { fontSize: theme("fontSize.lg") },
                        "h1, h2, h3, h4, h5, h6": {
                            fontWeight: theme("fontWeight.semibold"),
                            color: theme("colors.primary"),
                        },
                        blockquote: {
                            borderLeftWidth: "4px",
                            borderColor: theme("colors.secondary"),
                            backgroundColor: "#eef2ff",
                            padding: `${theme("spacing.4")} ${theme("spacing.6")}`,
                            color: theme("colors.primary"),
                            fontStyle: "normal",
                        },
                    },
                },
                dark: {
                    css: {
                        color: theme("colors.slate.200"),
                        a: {
                            color: theme("colors.indigo.400"),
                            "&:hover": {
                                color: theme("colors.purple.400"),
                            },
                        },
                        "h1, h2, h3, h4, h5, h6": {
                            color: theme("colors.white"),
                        },
                        blockquote: {
                            borderColor: theme("colors.indigo.500"),
                            backgroundColor: "#1e1b4b",
                            color: theme("colors.indigo.200"),
                        },
                    },
                },
            }),
        },
    },

    plugins: [
        require("@tailwindcss/typography"),
        require("@tailwindcss/forms"),
        require("daisyui"),
    ],

    daisyui: {
        themes: [
            {
                light: {
                    "primary": "#6366f1",
                    "secondary": "#8b5cf6",
                    "accent": "#4f46e5",
                    "neutral": "#1a1a2e",
                    "base-100": "#f8fafc",
                    "base-200": "#f1f5f9",
                    "base-300": "#e2e8f0",
                    "info": "#3b82f6",
                    "success": "#22c55e",
                    "warning": "#f59e0b",
                    "error": "#ef4444",
                },
                dark: {
                    "primary": "#818cf8",
                    "secondary": "#a78bfa",
                    "accent": "#6366f1",
                    "neutral": "#1e293b",
                    "base-100": "#0f172a",
                    "base-200": "#1e293b",
                    "base-300": "#334155",
                    "info": "#60a5fa",
                    "success": "#4ade80",
                    "warning": "#fbbf24",
                    "error": "#f87171",
                },
            },
        ],
    },
};
