# @mostlylucid/mermaid-enhancements

> Enhance Mermaid.js diagrams with interactive pan/zoom, fullscreen lightbox, export capabilities, and automatic theme switching.


[![npm version](https://img.shields.io/npm/v/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
[![npm downloads](https://img.shields.io/npm/dm/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
[![License: Unlicense](https://img.shields.io/badge/License-Unlicense-blue.svg)](http://unlicense.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-Ready-blue.svg)](https://www.typescriptlang.org/)
[![Bundle Size](https://img.shields.io/bundlephobia/minzip/@mostlylucid/mermaid-enhancements)](https://bundlephobia.com/package/@mostlylucid/mermaid-enhancements)


## Features

- **Interactive Pan & Zoom** - Navigate large diagrams with smooth mouse wheel zooming and drag-to-pan
- **Touch Support** - Full touch gesture support (pinch-to-zoom, swipe-to-pan) for mobile devices
- **Fullscreen Lightbox** - View diagrams in immersive fullscreen mode with integrated toolbar
- **Export to PNG/SVG** - Download high-quality diagrams (2x pixel ratio for PNG)
- **Automatic Theme Switching** - Seamlessly adapts to light/dark mode changes
- **Customizable Toolbar** - Show/hide/toggle individual toolbars or all toolbars at once
- **Enhanced Styling** - Theme-appropriate borders, drop shadows, and backdrop blur
- **Mobile/Cloudflare Optimized** - Uses direct DOM node references for better compatibility
- **Responsive Design** - Works perfectly on mobile, tablet, and desktop
- **Keyboard Shortcuts** - Press ESC to close lightbox, double-click to zoom
- **Accessible** - ARIA labels and keyboard navigation support
- **TypeScript Support** - Full type definitions included
- **Zero Configuration** - Works out of the box

## Installation

```bash
npm install @mostlylucid/mermaid-enhancements
```

### Peer Dependencies

This package requires Mermaid.js:

```bash
npm install mermaid
```

## Quick Start

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

## API Reference

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

### `configure(config)`

Configure toolbar buttons and icons globally. Must be called before `init()`.

```typescript
import { configure } from '@mostlylucid/mermaid-enhancements';

configure({
  controls: {
    showControls: false,  // Hide entire toolbar
    fullscreen: true,
    zoomIn: true,
    zoomOut: false,       // Hide zoom out button
    reset: true,
    exportPng: true,
    exportSvg: false      // Hide SVG export
  },
  icons: {
    fullscreen: 'fas fa-expand',  // Use Font Awesome instead of Boxicons
    zoomIn: 'fas fa-plus'
  }
});
```

**Parameters:**
- `config` (EnhancementConfig): Configuration object

**Returns:** `void`

### `hideToolbar(diagramId?)`

Hide the toolbar for a specific diagram or all diagrams. Can be called at any time after initialization.

```typescript
import { hideToolbar } from '@mostlylucid/mermaid-enhancements';

// Hide toolbar for a specific diagram
hideToolbar('diagram-1');

// Hide toolbars for all diagrams
hideToolbar();
```

**Parameters:**
- `diagramId` (string, optional): The ID of the diagram wrapper. If omitted, hides all toolbars.

**Returns:** `void`

### `showToolbar(diagramId?)`

Show the toolbar for a specific diagram or all diagrams. Can be called at any time after initialization.

```typescript
import { showToolbar } from '@mostlylucid/mermaid-enhancements';

// Show toolbar for a specific diagram
showToolbar('diagram-1');

// Show toolbars for all diagrams
showToolbar();
```

**Parameters:**
- `diagramId` (string, optional): The ID of the diagram wrapper. If omitted, shows all toolbars.

**Returns:** `void`

### `toggleToolbar(diagramId?)`

Toggle the toolbar visibility for a specific diagram or all diagrams. Can be called at any time after initialization.

```typescript
import { toggleToolbar } from '@mostlylucid/mermaid-enhancements';

// Toggle toolbar for a specific diagram
toggleToolbar('diagram-1');

// Toggle toolbars for all diagrams
toggleToolbar();
```

**Parameters:**
- `diagramId` (string, optional): The ID of the diagram wrapper. If omitted, toggles all toolbars.

**Returns:** `void`

**Note:** To get the diagram ID, inspect the `data-diagram-id` attribute on the `.mermaid-wrapper` element, or capture the return value from `enhanceMermaidDiagrams()` which returns the generated diagram IDs.

## Styling

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

## Theme Switching

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

## Toolbar Customization

Configure which buttons appear in the toolbar. All configurations are **global** and affect all diagrams on the page.

### Hide Entire Toolbar

```typescript
import { configure, init } from '@mostlylucid/mermaid-enhancements';

configure({
  controls: {
    showControls: false  // Hide all toolbars
  }
});

await init();
```

### Hide Button Groups

```typescript
configure({
  controls: {
    export: false,      // Hide both PNG and SVG export buttons
    fullscreen: false   // Hide fullscreen button
  }
});
```

### Granular Button Control

```typescript
configure({
  controls: {
    fullscreen: true,
    zoomIn: true,
    zoomOut: true,
    reset: false,        // Hide reset button
    exportPng: true,
    exportSvg: false     // Hide SVG export, keep PNG
  }
});

await init();
```

### Custom Icon Library

By default, Boxicons are used. You can configure different icon classes:

```typescript
configure({
  icons: {
    fullscreen: 'fas fa-expand',
    zoomIn: 'fas fa-plus',
    zoomOut: 'fas fa-minus',
    reset: 'fas fa-undo',
    exportPng: 'fas fa-image',
    exportSvg: 'fas fa-code'
  }
});
```

## Control Buttons

Each diagram gets the following controls (when enabled):

| Action | Description |
|--------|-------------|
| Fullscreen | Open diagram in lightbox with close button on far right |
| Zoom In | Increase zoom level |
| Zoom Out | Decrease zoom level |
| Reset | Reset zoom and position to default |
| Export PNG | Download as high-quality PNG (2x pixel ratio) |
| Export SVG | Download as vector SVG file |

**Note:** Panning is always enabled by default (drag to pan). In lightbox mode, a close button (✕) appears on the far right of the toolbar.

## Framework Integration

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

## Development

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

## TypeScript

Full TypeScript support is included:

```typescript
import type { PanZoomInstance, Theme, ExportFormat } from '@mostlylucid/mermaid-enhancements';

// Types are automatically available
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This is free and unencumbered software released into the public domain. See [LICENSE](LICENSE) for details.

## Acknowledgments

Built with:
- [Mermaid.js](https://mermaid.js.org/) - Diagram generation
- [svg-pan-zoom](https://github.com/bumbu/svg-pan-zoom) - Pan and zoom functionality
- [html-to-image](https://github.com/bubkoo/html-to-image) - Export capabilities
- [Boxicons](https://boxicons.com/) - Icon library

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Troubleshooting

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

## Support

- [GitHub Issues](https://github.com/scottgal/mostlylucidweb/issues)
- [Documentation](https://github.com/scottgal/mostlylucidweb/tree/main/mostlylucid-mermaid)

---

Made with love for the Mermaid.js community
