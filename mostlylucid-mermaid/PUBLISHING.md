# Publishing Guide

This guide explains how to publish `@mostlylucid/mermaid-enhancements` to npm.

## Prerequisites

1. **npm account**: Create one at https://www.npmjs.com/signup
2. **npm authentication**: Run `npm login` to authenticate

## Pre-publish Checklist

- [ ] Update version in `package.json`
- [ ] Update `CHANGELOG.md` with changes
- [ ] Run tests (if any)
- [ ] Build TypeScript: `npm run build`
- [ ] Test the package locally
- [ ] Review files that will be published: `npm pack --dry-run`

## Publishing Steps

### 1. Update Version

Follow [Semantic Versioning](https://semver.org/):

```bash
# Patch release (bug fixes)
npm version patch

# Minor release (new features, backward compatible)
npm version minor

# Major release (breaking changes)
npm version major
```

### 2. Build

```bash
npm run build
```

This runs TypeScript compiler and generates the `dist/` directory.

### 3. Test Package Locally

Create a test project and install from local directory:

```bash
cd /path/to/test-project
npm install /path/to/mostlylucid-mermaid
```

### 4. Publish to npm

```bash
# Publish to npm registry
npm publish --access public

# For scoped packages (@mostlylucid/...), specify access level
npm publish --access public
```

### 5. Verify Publication

```bash
# View package on npm
npm view @mostlylucid/mermaid-enhancements

# Install from npm to verify
npm install @mostlylucid/mermaid-enhancements
```

## Publishing a Beta Version

```bash
# Update to beta version
npm version 1.1.0-beta.0

# Publish with beta tag
npm publish --tag beta --access public

# Users can install beta with:
npm install @mostlylucid/mermaid-enhancements@beta
```

## Unpublishing (Emergency Only)

**Warning**: Unpublishing is permanent and should only be used in emergencies.

```bash
# Unpublish specific version
npm unpublish @mostlylucid/mermaid-enhancements@1.0.0

# Unpublish entire package (within 72 hours of publish)
npm unpublish @mostlylucid/mermaid-enhancements --force
```

## Deprecating a Version

If you need to discourage use of a version without unpublishing:

```bash
npm deprecate @mostlylucid/mermaid-enhancements@1.0.0 "Critical bug, please upgrade to 1.0.1"
```

## Post-publish Checklist

- [ ] Verify package on npm: https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements
- [ ] Test installation: `npm install @mostlylucid/mermaid-enhancements`
- [ ] Create GitHub release with changelog
- [ ] Update documentation if needed
- [ ] Announce on social media/forums (optional)

## Automated Publishing with GitHub Actions

Consider setting up automated publishing with GitHub Actions:

```yaml
# .github/workflows/publish.yml
name: Publish to npm

on:
  release:
    types: [created]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '18'
          registry-url: 'https://registry.npmjs.org'
      - run: npm ci
      - run: npm run build
      - run: npm publish --access public
        env:
          NODE_AUTH_TOKEN: ${{ secrets.NPM_TOKEN }}
```

## Troubleshooting

### "Package name already exists"

The package name might be taken. Try a different name or use a scoped package (`@yourscope/package-name`).

### "You do not have permission to publish"

Make sure you're logged in with the correct account:

```bash
npm whoami
npm login
```

### "Version already published"

You can't republish the same version. Increment the version number:

```bash
npm version patch
```

## Resources

- [npm Documentation](https://docs.npmjs.com/)
- [Semantic Versioning](https://semver.org/)
- [npm CLI Reference](https://docs.npmjs.com/cli/)
