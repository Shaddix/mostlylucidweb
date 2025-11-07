# @mostlylucid/mermaid-enhancements

> Enhance Mermaid.js diagrams with interactive pan/zoom, fullscreen lightbox, export capabilities, and automatic theme switching.


[![npm version](https://img.shields.io/npm/v/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
[![npm downloads](https://img.shields.io/npm/dm/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
[![License: Unlicense](https://img.shields.io/badge/License-Unlicense-blue.svg)](http://unlicense.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-Ready-blue.svg)](https://www.typescriptlang.org/)
[![Bundle Size](https://img.shields.io/bundlephobia/minzip/@mostlylucid/mermaid-enhancements)](https://bundlephobia.com/package/@mostlylucid/mermaid-enhancements)


## Features

- **Interactive Pan & Zoom** - Navigate large diagrams with smooth mouse wheel zooming and drag-to-pan
- **Fullscreen Lightbox** - View diagrams in an immersive fullscreen mode
- **Export to PNG/SVG** - Download high-quality diagrams with a single click
- **Automatic Theme Switching** - Seamlessly adapts to light/dark mode changes
- **Responsive Design** - Works perfectly on mobile, tablet, and desktop
- **Keyboard Shortcuts** - Press ESC to close lightbox, double-click to zoom
- **Customizable Styling** - Easy-to-override CSS variables
- **Accessible** - ARIA labels and keyboard navigation support
- **TypeScript Support** - Full type definitions included
- **Zero Configuration** - Works out of the box

## 📦 Installation

```bash
npm install @mostlylucid/mermaid-enhancements
```

### Peer Dependencies

This package requires Mermaid.js:

```bash
npm install mermaid
```

## 🚀 Quick Start

### Basic Usage

```typescript
import mermaid from 'mermaid';
import { init } from '@mostlylucid/mermaid-enhancements';
import '@mostlylucid/mermaid-enhancements/styles.css';

// Initialize Mermaid with enhancements
await init();
```

### Minified Version (Production)

For production use, import the minified version (58% smaller):

```typescript
import { init } from '@mostlylucid/mermaid-enhancements/min';
import '@mostlylucid/mermaid-enhancements/styles.css';

await init();
```

**Size comparison:**
- Regular: 23.46 KB
- Minified: 9.78 KB (58.3% smaller)

### HTML (via CDN)

```html
<!DOCTYPE html>
<html>
<head>
    <!-- Boxicons for control button icons -->
    <link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">

    <!-- Mermaid Enhancements CSS -->
    <link rel="stylesheet" href="https://unpkg.com/@mostlylucid/mermaid-enhancements/src/styles.css">
</head>
<body>
    <!-- Your Mermaid diagram -->
    <div class="mermaid">
graph TD
    A[Start] --> B{Decision}
    B -->|Yes| C[Result 1]
    B -->|No| D[Result 2]
    </div>

    <script type="module">
        import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
        // Use minified version for production
        import { init } from 'https://unpkg.com/@mostlylucid/mermaid-enhancements/dist/index.min.js';

        await init();
    </script>
</body>
</html>
```

**CDN Options:**

```html
<!-- unpkg (minified, recommended) -->
<script type="module" src="https://unpkg.com/@mostlylucid/mermaid-enhancements"></script>

<!-- jsdelivr (minified, with version pinning) -->
<script type="module" src="https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.0.0"></script>

<!-- Direct import -->
<script type="module">
  import { init } from 'https://unpkg.com/@mostlylucid/mermaid-enhancements/dist/index.min.js';
  await init();
</script>
```

## 🎯 API Reference

### `init()`

Initialize Mermaid with all enhancements. This is the main entry point.

```typescript
import { init } from '@mostlylucid/mermaid-enhancements';

await init();
```

**Returns:** `Promise<void>`

### `initMermaid()`

Initialize Mermaid with theme switching support. Called automatically by `init()`.

```typescript
import { initMermaid } from '@mostlylucid/mermaid-enhancements';

await initMermaid();
```

**Features:**
- Normalizes code fence blocks
- Sets up theme change listeners
- Handles OS-level theme preferences
- Re-renders diagrams when theme changes

**Returns:** `Promise<void>`

### `enhanceMermaidDiagrams()`

Add pan/zoom, fullscreen, and export capabilities to rendered diagrams.

```typescript
import { enhanceMermaidDiagrams } from '@mostlylucid/mermaid-enhancements';

enhanceMermaidDiagrams();
```

**Returns:** `void`

### `cleanupMermaidEnhancements()`

Clean up all pan-zoom instances. Call before re-rendering diagrams or unmounting.

```typescript
import { cleanupMermaidEnhancements } from '@mostlylucid/mermaid-enhancements';

cleanupMermaidEnhancements();
```

**Returns:** `void`

## 🎨 Styling

The package includes a complete CSS file with sensible defaults. Import it in your project:

```typescript
import '@mostlylucid/mermaid-enhancements/styles.css';
```

### Customization

Override CSS variables or classes to customize the appearance:

```css
/* Custom control button colors */
.mermaid-control-btn {
    color: #your-color;
}

.mermaid-control-btn:hover {
    background: #your-hover-color;
}

/* Custom lightbox background */
.mermaid-lightbox {
    background: rgba(0, 0, 0, 0.95);
}
```

## 🌓 Theme Switching

The package automatically detects and responds to theme changes through multiple methods:

### 1. Custom Events

Dispatch custom events to trigger theme changes:

```javascript
// Switch to dark theme
document.body.dispatchEvent(new Event('dark-theme-set'));

// Switch to light theme
document.body.dispatchEvent(new Event('light-theme-set'));
```

### 2. Global State

Set a global theme state:

```javascript
window.__themeState = 'dark'; // or 'light'
await initMermaid();
```

### 3. LocalStorage

The package checks `localStorage.theme`:

```javascript
localStorage.theme = 'dark'; // or 'light'
```

### 4. CSS Class

Add a `dark` class to the `<html>` element:

```javascript
document.documentElement.classList.add('dark');
```

### 5. OS Preference

Automatically detects system-level dark mode preference via `prefers-color-scheme`.

## 🎮 Control Buttons

Each diagram gets the following controls:

| Icon | Action | Description |
|------|--------|-------------|
| 🖼️ | Fullscreen | Open diagram in lightbox |
| ➕ | Zoom In | Increase zoom level |
| ➖ | Zoom Out | Decrease zoom level |
| 🔄 | Reset | Reset zoom and position |
| ✋ | Pan | Toggle pan mode |
| 📷 | Export PNG | Download as PNG image |
| 📄 | Export SVG | Download as SVG file |

## 🔌 Framework Integration

### React

```tsx
import { useEffect } from 'react';
import { init, cleanupMermaidEnhancements } from '@mostlylucid/mermaid-enhancements';
import '@mostlylucid/mermaid-enhancements/styles.css';

function MermaidDiagram({ chart }: { chart: string }) {
    useEffect(() => {
        init();

        return () => cleanupMermaidEnhancements();
    }, [chart]);

    return (
        <div className="mermaid">
            {chart}
        </div>
    );
}
```

### Vue

```vue
<template>
    <div class="mermaid">
        {{ chart }}
    </div>
</template>

<script setup>
import { onMounted, onUnmounted } from 'vue';
import { init, cleanupMermaidEnhancements } from '@mostlylucid/mermaid-enhancements';
import '@mostlylucid/mermaid-enhancements/styles.css';

const props = defineProps(['chart']);

onMounted(async () => {
    await init();
});

onUnmounted(() => {
    cleanupMermaidEnhancements();
});
</script>
```

### Svelte

```svelte
<script>
    import { onMount, onDestroy } from 'svelte';
    import { init, cleanupMermaidEnhancements } from '@mostlylucid/mermaid-enhancements';
    import '@mostlylucid/mermaid-enhancements/styles.css';

    export let chart;

    onMount(async () => {
        await init();
    });

    onDestroy(() => {
        cleanupMermaidEnhancements();
    });
</script>

<div class="mermaid">
    {@html chart}
</div>
```

## 🛠️ Development

### Run Demo

```bash
npm install
npm run dev
```

This will start a local server and open the demo page at `http://localhost:3000`.

### Build

```bash
npm run build
```

## 📝 TypeScript

Full TypeScript support is included:

```typescript
import type { PanZoomInstance, Theme, ExportFormat } from '@mostlylucid/mermaid-enhancements';

// Types are automatically available
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This is free and unencumbered software released into the public domain. See [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

Built with:
- [Mermaid.js](https://mermaid.js.org/) - Diagram generation
- [svg-pan-zoom](https://github.com/bumbu/svg-pan-zoom) - Pan and zoom functionality
- [html-to-image](https://github.com/bubkoo/html-to-image) - Export capabilities
- [Boxicons](https://boxicons.com/) - Icon library

## 📊 Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## 🐛 Troubleshooting

### Controls not appearing

Make sure you've included Boxicons:

```html
<link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">
```

### Theme not switching

Ensure Mermaid is loaded before calling `init()`:

```typescript
// Wait for Mermaid to be available
await new Promise(resolve => setTimeout(resolve, 100));
await init();
```

### Export not working

The export feature requires modern browsers with Canvas API support. Check browser console for errors.

## 📮 Support

- [GitHub Issues](https://github.com/scottgal/mostlylucidweb/issues)
- [Documentation](https://github.com/scottgal/mostlylucidweb/tree/main/mostlylucid-mermaid)

---

Made with ❤️ for the Mermaid.js community
