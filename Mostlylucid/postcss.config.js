module.exports = {
    plugins: {
        'postcss-import': {},  // <-- Process @import first
        tailwindcss: {},  // <-- Tailwind is loaded here
        autoprefixer: {},
        cssnano: { preset: 'default' }
    }
}