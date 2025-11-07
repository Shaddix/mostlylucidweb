# Monorepo Setup Guide

This package is part of the `mostlylucidweb` monorepo and has a specific setup to ensure GitHub Actions work correctly.

## Directory Structure

```
mostlylucidweb/                    # Repository root
├── .github/
│   └── workflows/
│       └── publish-npm.yml        # Workflow at repo root
├── mostlylucid-mermaid/           # Package directory
│   ├── package.json
│   ├── src/
│   ├── dist/N
│   └── scripts/
│       └── publish.sh
└── [other projects]
```

## How It Works

### GitHub Actions Workflow

The workflow file **must** be at the repository root (`.github/workflows/publish-npm.yml`) because that's where GitHub looks for workflows.

To make the workflow operate on the package in the subdirectory, we use:

```yaml
jobs:
  publish:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: mostlylucid-mermaid  # All commands run here
```

This setting makes all `run:` steps execute in the `mostlylucid-mermaid` directory automatically, so:
- `npm ci` installs from `mostlylucid-mermaid/package.json`
- `npm test` runs tests in that directory
- `npm run build` builds from that directory
- `npm publish` publishes from that directory

### Publishing from the Package Directory

When you want to publish, **always run the publish script from within the package directory**:

```bash
cd mostlylucid-mermaid
./scripts/publish.sh 1.2.3
```

The script will:
1. Update `package.json` in the current directory
2. Run tests and build locally
3. Commit the version change
4. Create and push tag `ml-mermaidv1.2.3`
5. GitHub Actions picks up the tag and publishes

### Tag Format

Tags **must** follow the pattern `ml-mermaidv{VERSION}`:
- ✅ `ml-mermaidv1.0.0`
- ✅ `ml-mermaidv1.2.3-beta.1`
- ❌ `v1.0.0` (won't trigger this workflow)
- ❌ `mermaidv1.0.0` (wrong prefix)

The `ml-mermaid` prefix ensures tags for this package don't conflict with other packages in the monorepo.

## Troubleshooting

### "package.json not found" in GitHub Actions

**Symptom**:
```
npm error enoent ENOENT: no such file or directory, open '/home/runner/work/mostlylucidweb/mostlylucidweb/package.json'
```

**Cause**: The workflow is running in the repo root instead of the package directory.

**Fix**: Ensure the workflow has the `defaults.run.working-directory` setting:

```yaml
defaults:
  run:
    working-directory: mostlylucid-mermaid
```

### Script Not Executable

**Symptom**: `bash: ./scripts/publish.sh: Permission denied`

**Fix**:
```bash
chmod +x scripts/publish.sh
```

### Wrong Directory

**Symptom**: Script fails to find package.json

**Fix**: Always run from the package directory:
```bash
cd mostlylucid-mermaid
./scripts/publish.sh 1.2.3
```

## Best Practices

1. **Always work in the package directory**:
   ```bash
   cd mostlylucid-mermaid
   ```

2. **Test locally before publishing**:
   ```bash
   npm test
   npm run build:all
   ```

3. **Use the publish script** (don't create tags manually):
   ```bash
   ./scripts/publish.sh 1.2.3
   ```

4. **Monitor the GitHub Actions workflow**:
   - Visit: https://github.com/scottgal/mostlylucidweb/actions
   - Watch for the "Publish to npm" workflow

5. **Verify publication**:
   - npm: https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements
   - GitHub Release: https://github.com/scottgal/mostlylucidweb/releases

## Adding New Packages to the Monorepo

If you add another npm package to this monorepo:

1. **Create a new workflow** (e.g., `publish-other-package.yml`) with:
   - Different tag pattern (e.g., `other-packagev*`)
   - Different working directory (e.g., `other-package`)

2. **Keep workflows at repo root**: `.github/workflows/`

3. **Use unique tag prefixes** to avoid conflicts

Example for a second package:

```yaml
on:
  push:
    tags:
      - 'other-pkgv*'  # Different prefix

jobs:
  publish:
    defaults:
      run:
        working-directory: other-package  # Different directory
```
