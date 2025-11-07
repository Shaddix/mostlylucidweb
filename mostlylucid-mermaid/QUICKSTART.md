# Quick Start Guide

Get up and running with @mostlylucid/mermaid-enhancements in 5 minutes!

## Installation

```bash
npm install @mostlylucid/mermaid-enhancements mermaid
```

## Basic HTML Setup

Create an `index.html` file:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Mermaid Enhancements Demo</title>

    <!-- Required: Boxicons for control button icons -->
    <link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">

    <!-- Required: Enhancements CSS -->
    <link rel="stylesheet" href="node_modules/@mostlylucid/mermaid-enhancements/src/styles.css">
</head>
<body>
    <!-- Your Mermaid diagram -->
    <div class="mermaid">
graph LR
    A[Start] --> B[Process]
    B --> C[End]
    </div>

    <!-- Required: Load Mermaid and initialize enhancements -->
    <script type="module">
        import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
        import { init } from './node_modules/@mostlylucid/mermaid-enhancements/src/index.ts';

        // Initialize
        await init();
    </script>
</body>
</html>
```

## Using a Bundler (Vite, Webpack, etc.)

### 1. Install Dependencies

```bash
npm install @mostlylucid/mermaid-enhancements mermaid
```

### 2. Create Your Component

**JavaScript/TypeScript:**

```typescript
import mermaid from 'mermaid';
import { init } from '@mostlylucid/mermaid-enhancements';
import '@mostlylucid/mermaid-enhancements/styles.css';

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    await init();
});
```

**HTML:**

```html
<!DOCTYPE html>
<html>
<head>
    <!-- Don't forget Boxicons! -->
    <link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">
</head>
<body>
    <div class="mermaid">
graph TD
    A[Start] --> B{Decision}
    B -->|Yes| C[Option 1]
    B -->|No| D[Option 2]
    </div>

    <script src="./main.ts" type="module"></script>
</body>
</html>
```

## Adding Theme Switching

```html
<button onclick="toggleTheme()">Toggle Theme</button>

<script>
function toggleTheme() {
    const isDark = document.body.classList.toggle('dark');
    document.documentElement.classList.toggle('dark', isDark);

    // Notify the enhancements
    const event = new Event(isDark ? 'dark-theme-set' : 'light-theme-set');
    document.body.dispatchEvent(event);
}
</script>
```

## What You Get

After initialization, each diagram automatically gets:

- **Pan & Zoom**: Mouse wheel to zoom, drag to pan
- **Control Buttons**: Zoom in/out, reset, fullscreen, export
- **Fullscreen Lightbox**: Click the fullscreen button
- **Export**: Save as PNG or SVG
- **Theme Switching**: Automatic light/dark mode support

## Customizing Styles

Override the default styles in your CSS:

```css
/* Change control button colors */
.mermaid-control-btn {
    color: #your-color !important;
}

.mermaid-control-btn:hover {
    background: #your-hover-color !important;
}

/* Change lightbox background */
.mermaid-lightbox {
    background: rgba(0, 0, 0, 0.9) !important;
}
```

## Keyboard Shortcuts

- **ESC**: Close fullscreen lightbox
- **Double-click**: Zoom in
- **Mouse wheel**: Zoom in/out

## Common Issues

### Controls Not Showing

Make sure you've included Boxicons:

```html
<link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">
```

### Diagrams Not Enhanced

Ensure `init()` is called after Mermaid has loaded and rendered:

```typescript
// Give Mermaid time to load
await new Promise(resolve => setTimeout(resolve, 100));
await init();
```

### Theme Not Switching

Make sure you're dispatching the correct events:

```javascript
// Switch to dark
document.body.dispatchEvent(new Event('dark-theme-set'));

// Switch to light
document.body.dispatchEvent(new Event('light-theme-set'));
```

## Next Steps

- Read the full [README](./README.md) for detailed documentation
- Check out the [demo](./examples/demo.html) for examples
- Explore the [TypeScript types](./src/types.ts) for advanced usage

## Need Help?

- [GitHub Issues](https://github.com/scottgal/mostlylucidweb/issues)
- [Examples](./examples/)
- [Full Documentation](./README.md)

Happy diagramming! 🎨📊
