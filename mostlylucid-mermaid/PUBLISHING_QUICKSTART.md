# Publishing Quick Start Guide

## TL;DR - Publish in 3 Ways

### 1️⃣ Super Easy (Recommended)

```bash
npm run publish:patch   # 1.0.0 → 1.0.1 (bug fixes)
npm run publish:minor   # 1.0.0 → 1.1.0 (new features)
npm run publish:major   # 1.0.0 → 2.0.0 (breaking changes)
```

### 2️⃣ Custom Version

```bash
./scripts/publish.sh 1.2.3
./scripts/publish.sh 1.2.3-beta.1
```

### 3️⃣ Manual Tag

```bash
git tag ml-mermaidv1.2.3
git push origin ml-mermaidv1.2.3
```

## What Happens When You Publish?

The GitHub Action automatically:

1. ✅ Extracts version from tag (`ml-mermaidv1.2.3` → `1.2.3`)
2. ✅ Updates `package.json`
3. ✅ Runs all tests
4. ✅ Builds TypeScript
5. ✅ Publishes to npm with provenance
6. ✅ Creates GitHub Release with notes

## Tag Format

Tags **must** follow this format:

```
ml-mermaidv{VERSION}
```

Examples:
- ✅ `ml-mermaidv1.0.0`
- ✅ `ml-mermaidv1.2.3`
- ✅ `ml-mermaidv2.0.0-beta.1`
- ❌ `v1.0.0` (wrong prefix)
- ❌ `1.0.0` (no prefix)
- ❌ `mermaid-v1.0.0` (wrong prefix)

## Version Types

| Type | Example | When to Use |
|------|---------|-------------|
| **Patch** | 1.0.0 → 1.0.1 | Bug fixes, documentation updates |
| **Minor** | 1.0.0 → 1.1.0 | New features (backward compatible) |
| **Major** | 1.0.0 → 2.0.0 | Breaking changes |
| **Pre-release** | 1.0.0 → 1.1.0-beta.1 | Testing before official release |

## Pre-Releases

Pre-release versions are automatically marked on GitHub:

```bash
# Beta release
./scripts/publish.sh 1.2.0-beta.1

# Alpha release
./scripts/publish.sh 1.2.0-alpha.1

# Release candidate
./scripts/publish.sh 1.2.0-rc.1
```

Users can install pre-releases:

```bash
npm install @mostlylucid/mermaid-enhancements@1.2.0-beta.1
npm install @mostlylucid/mermaid-enhancements@beta  # latest beta
```

## Workflow Monitoring

After pushing a tag:

1. **GitHub Actions**: https://github.com/scottgal/mostlylucidweb/actions
2. **npm Package**: https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements
3. **GitHub Releases**: https://github.com/scottgal/mostlylucidweb/releases

## Troubleshooting

### "Tests failed" - Should I continue?

The publish script will ask if you want to continue. Generally:
- ❌ Don't continue if core functionality is broken
- ✅ Continue if it's just minor test environment issues

### "Working directory not clean"

Commit or stash your changes first:

```bash
git add .
git commit -m "Your changes"
# Then run publish script again
```

### Tag already exists

Delete the tag locally and remotely:

```bash
git tag -d ml-mermaidv1.2.3
git push origin :refs/tags/ml-mermaidv1.2.3
```

Then create it again with a new version.

## Example Workflow

```bash
# 1. Make your changes
git add .
git commit -m "feat: add custom icon configuration"

# 2. Run tests locally
npm test

# 3. Build locally to verify
npm run build

# 4. Publish (use appropriate bump type)
npm run publish:minor

# 5. Monitor the GitHub Action
# Visit: https://github.com/scottgal/mostlylucidweb/actions

# 6. Verify on npm
# Visit: https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements
```

## First Time Setup

See [NPM_PUBLISHING_SETUP.md](./NPM_PUBLISHING_SETUP.md) for:
- Creating npm access token
- Adding GitHub secrets
- Detailed configuration

## How Tags and Branches Work

**Important**: GitHub tags are tied to specific commits, not branches.

When you create a tag `ml-mermaidv1.2.3` on your current branch:
1. The tag points to the current commit (wherever you are)
2. Pushing the tag triggers the GitHub Action
3. The Action checks out the **exact commit** that was tagged
4. It doesn't matter which branch you were on when you created the tag

**Example:**

```bash
# On feature branch
git checkout feature-branch
git commit -m "Add new feature"

# Create tag on this commit
git tag ml-mermaidv1.2.0

# Push tag (triggers action)
git push origin ml-mermaidv1.2.0

# The Action will build THIS EXACT COMMIT
# Even though it's on a feature branch
```

**Best Practice**: Tag commits on your main/release branch after merging:

```bash
# Merge feature to main
git checkout main
git merge feature-branch

# Tag the merge commit
git tag ml-mermaidv1.2.0
git push origin main
git push origin ml-mermaidv1.2.0
```

The GitHub Action logs will show:
- Which commit was checked out
- The commit message and author
- The exact SHA being built

## Need Help?

- 📖 Full Guide: [NPM_PUBLISHING_SETUP.md](./NPM_PUBLISHING_SETUP.md)
- 🐛 Issues: https://github.com/scottgal/mostlylucidweb/issues
- 📝 Changelog: [CHANGELOG.md](./CHANGELOG.md)
