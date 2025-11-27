# Bundle Sizes

## Distribution Files

All sizes shown are **uncompressed** (before gzip/brotli).

### Main Bundle

| File | Size | Minified | Savings | Gzipped (est.) |
|------|------|----------|---------|----------------|
| `index.js` | 1.05 KB | 0.45 KB | 57.0% | ~0.3 KB |
| `enhancements.js` | 15.05 KB | 6.27 KB | 58.4% | ~3.0 KB |
| `theme-switcher.js` | 7.25 KB | 3.00 KB | 58.7% | ~1.5 KB |
| `types.js` | 0.11 KB | 0.06 KB | 42.7% | ~0.05 KB |
| **Total** | **23.46 KB** | **9.78 KB** | **58.3%** | **~5 KB** |

### When Served Over HTTP

With gzip compression (typical server configuration):
- **Regular build**: ~23.46 KB → ~12 KB gzipped
- **Minified build**: ~9.78 KB → ~5 KB gzipped

With brotli compression (modern CDNs):
- **Regular build**: ~23.46 KB → ~10 KB brotli
- **Minified build**: ~9.78 KB → ~4 KB brotli

## Comparison with Dependencies

| Library | Size (minified + gzipped) |
|---------|---------------------------|
| Mermaid.js | ~250 KB |
| svg-pan-zoom | ~15 KB |
| html-to-image | ~25 KB |
| **This package (minified)** | **~5 KB** |

## Which Version Should I Use?

### Development
Use the **regular version** for easier debugging:
```typescript
import { init } from '@mostlylucid/mermaid-enhancements';
```

### Production
Use the **minified version** for smaller bundle size:
```typescript
import { init } from '@mostlylucid/mermaid-enhancements/min';
```

### CDN / Direct Browser
The unpkg/jsdelivr links automatically serve the **minified version**:
```html
<script type="module" src="https://unpkg.com/@mostlylucid/mermaid-enhancements"></script>
```

## Bundle Analysis

The minification process:
- ✅ Removes all comments
- ✅ Removes whitespace
- ✅ Mangles variable names (except public API)
- ✅ Removes dead code
- ✅ Optimizes function calls
- ✅ Preserves public API names: `init`, `configure`, `enhanceMermaidDiagrams`, etc.

## CSS Styles

The CSS file is **not minified by default** but is very small:
- `styles.css`: ~8 KB uncompressed
- Estimated gzipped: ~2 KB

You can minify it with your build tool if needed.

## Tree Shaking

This package is **tree-shakeable**. If you only import specific functions:

```typescript
// Only import what you need
import { enhanceMermaidDiagrams } from '@mostlylucid/mermaid-enhancements';
```

Modern bundlers (Webpack, Rollup, Vite, etc.) will exclude unused code.

## Tips for Smallest Bundle

1. **Use the minified export**: Import from `/min`
2. **Enable gzip/brotli**: Configure your server or CDN
3. **Use tree shaking**: Only import what you need
4. **Lazy load**: Load the enhancement after Mermaid renders
5. **Code splitting**: Split enhancements into separate chunk

Example of lazy loading:
```typescript
// Load Mermaid first
import mermaid from 'mermaid';
await mermaid.run();

// Then lazy load enhancements
const { enhanceMermaidDiagrams } = await import('@mostlylucid/mermaid-enhancements/min');
enhanceMermaidDiagrams();
```

## Updates

Bundle sizes are measured on each build. Run `npm run build:all` to see current sizes.

Last updated: <!-- This will be updated by CI/CD -->
