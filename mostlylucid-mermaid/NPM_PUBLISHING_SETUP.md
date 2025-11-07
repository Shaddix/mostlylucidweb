# NPM Publishing Setup Guide

This package is configured to automatically publish to npm when you create a GitHub release. Here's how to set it up:

## Monorepo Setup

**Important:** This package is part of a monorepo. The GitHub Actions workflow is located at the repository root (`.github/workflows/publish-npm.yml`) and is configured to work in the `mostlylucid-mermaid` subdirectory using:

```yaml
defaults:
  run:
    working-directory: mostlylucid-mermaid
```

This means all npm commands (install, test, build, publish) run in the correct package directory automatically.

## Prerequisites

1. An npm account (create one at [npmjs.com](https://www.npmjs.com/signup))
2. Admin access to this GitHub repository

## Step 1: Generate an NPM Access Token

1. Log in to your npm account at [npmjs.com](https://www.npmjs.com)
2. Click on your profile picture → **Access Tokens**
3. Click **Generate New Token** → **Classic Token**
4. Select **Automation** token type
5. Give it a descriptive name like `github-actions-mostlylucid-mermaid`
6. Click **Generate Token**
7. **Copy the token immediately** - you won't be able to see it again!

## Step 2: Add NPM Token to GitHub Secrets

1. Go to your GitHub repository
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `NPM_TOKEN` (exactly this name)
5. Value: Paste the npm token you copied
6. Click **Add secret**

**Important Notes:**
- ✅ This is the **ONLY** secret you need to add manually
- ✅ `GITHUB_TOKEN` is **automatic** - don't add it, it's already there!
- ✅ GitHub Actions automatically provides `GITHUB_TOKEN` for every workflow
- ✅ The workflow uses two tokens:
  - `NPM_TOKEN` → for publishing to npm (you add this)
  - `GITHUB_TOKEN` → for creating GitHub releases (automatic)

## Step 3: Publishing a New Version

### Quick Start (Using Helper Script)

The easiest way to publish is using the provided helper script:

```bash
# Publish a specific version
./scripts/publish.sh 1.2.3

# Auto-increment patch version (1.0.0 → 1.0.1)
./scripts/publish.sh patch

# Auto-increment minor version (1.0.0 → 1.1.0)
./scripts/publish.sh minor

# Auto-increment major version (1.0.0 → 2.0.0)
./scripts/publish.sh major

# Publish a pre-release
./scripts/publish.sh 1.2.3-beta.1
```

The script will:
- ✅ Validate the version format
- ✅ Update package.json
- ✅ Run tests
- ✅ Build the project
- ✅ Commit changes
- ✅ Create and push the tag
- ✅ Trigger the GitHub Action

### Option 1: Automatic Publishing via Tag (Manual)

Simply create and push a tag with the format `ml-mermaidv{VERSION}`:

```bash
# For a patch release (1.0.0 → 1.0.1)
git tag ml-mermaidv1.0.1
git push origin ml-mermaidv1.0.1

# For a minor release (1.0.0 → 1.1.0)
git tag ml-mermaidv1.1.0
git push origin ml-mermaidv1.1.0

# For a major release (1.0.0 → 2.0.0)
git tag ml-mermaidv2.0.0
git push origin ml-mermaidv2.0.0

# For a pre-release (beta, alpha, rc)
git tag ml-mermaidv1.1.0-beta.1
git push origin ml-mermaidv1.1.0-beta.1
```

The GitHub Action will automatically:
1. Extract the version from the tag name (e.g., `ml-mermaidv1.1.1` → `1.1.1`)
2. Update `package.json` with the version
3. Run all tests
4. Build the project
5. Publish to npm with provenance
6. Create a GitHub Release with release notes

**Note**: Pre-release versions (containing `-beta`, `-alpha`, `-rc`, etc.) will be marked as "pre-release" on GitHub.

### Option 2: Manual Publishing via npm version Command

1. Update the version using npm:
   ```bash
   npm version patch  # for bug fixes (1.0.0 → 1.0.1)
   npm version minor  # for new features (1.0.0 → 1.1.0)
   npm version major  # for breaking changes (1.0.0 → 2.0.0)
   npm version prerelease --preid=beta  # for pre-releases (1.0.0 → 1.0.1-beta.0)
   ```

2. Create the tag manually:
   ```bash
   # Get the version from package.json
   VERSION=$(node -p "require('./package.json').version")

   # Create and push the tag
   git tag ml-mermaidv${VERSION}
   git push origin ml-mermaidv${VERSION}
   ```

3. The workflow will trigger automatically

### Option 3: Manual Workflow Trigger

You can also trigger the workflow manually without creating a tag:

1. Go to **Actions** → **Publish to npm**
2. Click **Run workflow**
3. Select the branch
4. Click **Run workflow**

## Monitoring the Publish Process

1. Go to **Actions** tab in your GitHub repository
2. Click on the latest **Publish to npm** workflow run
3. Watch the progress in real-time
4. Check for any errors

## Verifying the Publication

After publishing:

1. Check your package on npm: `https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements`
2. Verify the version number matches
3. Test installing in a new project:
   ```bash
   npm install @mostlylucid/mermaid-enhancements
   ```

## Troubleshooting

### "npm ERR! code E403"
- **Cause**: Invalid or missing NPM_TOKEN
- **Fix**: Regenerate the npm token and update the GitHub secret

### "npm ERR! 402 Payment Required"
- **Cause**: Scoped packages require a paid npm account (unless public)
- **Fix**: The workflow includes `--access public` flag, but verify your npm account can publish public scoped packages

### "npm ERR! 404 Not Found"
- **Cause**: Package name already taken or not properly scoped
- **Fix**: Ensure package name in `package.json` matches your npm org/username

### Build Fails
- **Cause**: TypeScript compilation errors
- **Fix**: Run `npm run build` locally to see errors and fix them before releasing

## Package Provenance

The workflow includes `--provenance` flag which adds build provenance to the package. This:
- Increases trust by showing where and how the package was built
- Requires `id-token: write` permission
- Is automatically verified by npm

## Best Practices

1. **Always test locally before releasing**:
   ```bash
   npm run build
   npm test
   ```

2. **Use semantic versioning**:
   - PATCH (1.0.x): Bug fixes
   - MINOR (1.x.0): New features (backward compatible)
   - MAJOR (x.0.0): Breaking changes

3. **Write clear release notes**:
   - List new features
   - Document breaking changes
   - Include migration guides if needed

4. **Keep the CHANGELOG.md updated**:
   - Document all changes
   - Group by version
   - Include dates

5. **Test the package after publishing**:
   - Install in a fresh project
   - Verify all features work
   - Check TypeScript types are correct

## Security Notes

- **Never commit npm tokens** to the repository
- **Rotate tokens periodically** (every 3-6 months)
- **Use Automation tokens** (not Publish tokens) for CI/CD
- **Enable 2FA** on your npm account for extra security

## Additional Resources

- [npm Publishing Documentation](https://docs.npmjs.com/creating-and-publishing-scoped-public-packages)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Semantic Versioning](https://semver.org/)
