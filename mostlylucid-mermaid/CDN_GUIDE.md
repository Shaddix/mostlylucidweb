# CDN Distribution Guide

## How CDNs Work (No Action Required!)

**TL;DR**: unpkg and jsdelivr automatically serve your package when you publish to npm. No GitHub Action needed!

## The Flow

```
1. You publish to npm
   ↓
2. npm registry updates
   ↓
3. unpkg/jsdelivr detect the update (automatic)
   ↓
4. They download and cache your package
   ↓
5. Your package is available on CDNs (2-5 minutes)
```

## CDN URLs

After publishing version `1.2.3` to npm, your package is automatically available at:

### unpkg

```
# Latest version (auto-updates)
https://unpkg.com/@mostlylucid/mermaid-enhancements

# Specific version (pinned)
https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3

# Minified bundle
https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js

# Styles
https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/src/styles.css
```

### jsdelivr

```
# Latest version
https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements

# Specific version
https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3

# Minified bundle
https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js

# Styles
https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3/src/styles.css
```

## What Gets Served?

The `package.json` configuration controls what's served:

```json
{
  "unpkg": "dist/index.min.js",
  "jsdelivr": "dist/index.min.js"
}
```

When someone visits:
- `https://unpkg.com/@mostlylucid/mermaid-enhancements` → serves `dist/index.min.js`
- `https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements` → serves `dist/index.min.js`

## Verifying CDN Availability

After publishing, use the verification script:

```bash
# Check if package is available on CDNs
./scripts/verify-cdn.sh

# Check specific version
./scripts/verify-cdn.sh 1.2.3
```

## Usage in HTML

### Option 1: Auto-updating (latest version)

```html
<!DOCTYPE html>
<html>
<head>
  <link href="https://unpkg.com/boxicons@2.1.4/css/boxicons.min.css" rel="stylesheet">
  <link rel="stylesheet" href="https://unpkg.com/@mostlylucid/mermaid-enhancements/src/styles.css">
</head>
<body>
  <div class="mermaid">
    graph TD
      A --> B
  </div>

  <script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
    import { init } from 'https://unpkg.com/@mostlylucid/mermaid-enhancements';

    await init();
  </script>
</body>
</html>
```

### Option 2: Version-pinned (recommended for production)

```html
<script type="module">
  import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
  import { init } from 'https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js';

  await init();
</script>
```

## CDN Features

### unpkg
- ✅ Automatic npm synchronization
- ✅ Serves any file in your package
- ✅ Support for version ranges
- ✅ Automatic HTTPS
- ✅ Brotli/gzip compression
- ⏱️  Cache: 1 year for versioned, 10 minutes for latest

### jsdelivr
- ✅ Automatic npm synchronization
- ✅ Global CDN (multiple locations)
- ✅ Load balancing
- ✅ DDoS protection
- ✅ Automatic minification available
- ✅ Supports version combining
- ⏱️  Cache: Similar to unpkg

## Advanced CDN Features

### jsdelivr Combine Multiple Files

```html
<!-- Combine multiple files into one request -->
<script src="https://cdn.jsdelivr.net/combine/
  npm/mermaid@11,
  npm/@mostlylucid/mermaid-enhancements@1.2.3
"></script>
```

### jsdelivr Automatic Minification

```html
<!-- Add .min.js to any file -->
<script src="https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js"></script>
```

## Troubleshooting

### Package not appearing on CDN?

Wait 2-5 minutes after publishing, then:

```bash
# Verify it's on npm
curl https://registry.npmjs.org/@mostlylucid/mermaid-enhancements

# Check unpkg
curl -I https://unpkg.com/@mostlylucid/mermaid-enhancements

# Check jsdelivr
curl -I https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements
```

### Getting 404 errors?

1. **Check the version exists on npm**:
   ```bash
   npm view @mostlylucid/mermaid-enhancements versions
   ```

2. **Check the file path is correct**:
   ```bash
   # List all files in the package
   npm pack --dry-run @mostlylucid/mermaid-enhancements
   ```

3. **Verify package.json `files` field** includes the directory:
   ```json
   {
     "files": ["src/", "dist/", "README.md", "LICENSE"]
   }
   ```

### Force CDN refresh?

#### unpkg
```
# Add a query parameter to bypass cache
https://unpkg.com/@mostlylucid/mermaid-enhancements?t=1234567890
```

#### jsdelivr
```
# Purge cache via API
curl https://purge.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js
```

## Best Practices

### ✅ DO:
- Pin to specific versions in production
- Use minified bundles from CDN
- Include SRI hashes for security (advanced)
- Test CDN URLs before deploying

### ❌ DON'T:
- Use `@latest` in production (use version numbers)
- Link to non-minified files from CDN
- Forget to include styles
- Skip the `files` field in package.json

## Monitoring

After each publish, the GitHub Action shows:

```
📦 Published to npm
🌐 CDN URLs:
   unpkg: https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3
   jsdelivr: https://cdn.jsdelivr.net/npm/@mostlylucid/mermaid-enhancements@1.2.3
```

You can manually verify with:

```bash
./scripts/verify-cdn.sh 1.2.3
```

## SRI (Subresource Integrity) Hashes

For extra security, generate SRI hashes:

```bash
# Generate SRI hash for a file
curl -s https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js | \
  openssl dgst -sha384 -binary | \
  openssl base64 -A
```

Use in HTML:

```html
<script type="module"
  src="https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js"
  integrity="sha384-HASH_HERE"
  crossorigin="anonymous">
</script>
```

## Performance Tips

1. **Use version-specific URLs** (better caching)
2. **Prefer jsdelivr for global audiences** (multi-region)
3. **Preload critical resources**:
   ```html
   <link rel="preload"
     href="https://unpkg.com/@mostlylucid/mermaid-enhancements@1.2.3/dist/index.min.js"
     as="script"
     crossorigin>
   ```

4. **Use HTTP/2 Push** (if your server supports it)
5. **Monitor CDN availability** with uptime services

## Summary

✅ **No GitHub Action needed** - CDNs work automatically!
✅ **Publish to npm** - CDNs index within minutes
✅ **Use the verification script** - Check availability
✅ **Pin versions** - Better caching and stability
✅ **Minified by default** - Configured via package.json

The `unpkg` and `jsdelivr` fields in package.json tell the CDNs which file to serve by default!
