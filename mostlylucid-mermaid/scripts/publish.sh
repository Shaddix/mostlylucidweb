#!/bin/bash

# Publish helper script for @mostlylucid/mermaid-enhancements
# Usage: ./scripts/publish.sh [version]
# Example: ./scripts/publish.sh 1.2.3

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}ℹ ${NC}$1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check if version is provided
if [ -z "$1" ]; then
    print_error "Version is required"
    echo ""
    echo "Usage: $0 <version>"
    echo ""
    echo "Examples:"
    echo "  $0 1.2.3           # Publish version 1.2.3"
    echo "  $0 1.2.3-beta.1    # Publish pre-release version"
    echo "  $0 patch           # Auto-increment patch version"
    echo "  $0 minor           # Auto-increment minor version"
    echo "  $0 major           # Auto-increment major version"
    exit 1
fi

VERSION=$1

# If version is a bump type (patch/minor/major), calculate the new version
if [[ "$VERSION" =~ ^(patch|minor|major|prepatch|preminor|premajor|prerelease)$ ]]; then
    print_info "Calculating $VERSION version bump..."
    VERSION=$(npm version $VERSION --no-git-tag-version | sed 's/v//')
    print_success "New version: $VERSION"
fi

# Validate version format
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-.*)?$ ]]; then
    print_error "Invalid version format: $VERSION"
    echo "Expected format: X.Y.Z or X.Y.Z-suffix (e.g., 1.2.3 or 1.2.3-beta.1)"
    exit 1
fi

print_success "Valid version format: $VERSION"

# Check if working directory is clean
if [ -n "$(git status --porcelain)" ]; then
    print_warning "Working directory is not clean. You have uncommitted changes:"
    git status --short
    echo ""
    read -p "Do you want to continue? (y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Aborted by user"
        exit 1
    fi
fi

# Show current branch
CURRENT_BRANCH=$(git branch --show-current)
print_info "Current branch: $CURRENT_BRANCH"

# Ask for confirmation
echo ""
echo "This will:"
echo "  1. Update package.json to version $VERSION"
echo "  2. Run tests"
echo "  3. Build the project"
echo "  4. Commit the version change"
echo "  5. Create and push tag ml-mermaidv$VERSION"
echo "  6. Trigger GitHub Action to publish to npm"
echo ""
read -p "Continue? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    print_info "Aborted by user"
    exit 1
fi

# Update package.json
print_info "Updating package.json to version $VERSION..."
npm version $VERSION --no-git-tag-version --allow-same-version
print_success "Updated package.json"

# Run tests
print_info "Running tests..."
if npm test; then
    print_success "Tests passed"
else
    print_error "Tests failed"
    echo ""
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Aborted due to test failures"
        # Revert package.json change
        git checkout package.json package-lock.json 2>/dev/null || true
        exit 1
    fi
fi

# Build
print_info "Building project..."
if npm run build; then
    print_success "Build successful"
else
    print_error "Build failed"
    # Revert package.json change
    git checkout package.json package-lock.json 2>/dev/null || true
    exit 1
fi

# Commit version change
print_info "Committing version change..."
git add package.json package-lock.json
git commit -m "chore: bump version to $VERSION" || print_warning "No changes to commit (version might already be set)"
print_success "Committed version change"

# Create and push tag
TAG_NAME="ml-mermaidv$VERSION"
print_info "Creating tag $TAG_NAME..."
git tag $TAG_NAME

print_info "Pushing commit and tag to origin..."
git push origin $CURRENT_BRANCH
git push origin $TAG_NAME

print_success "Tag $TAG_NAME pushed successfully!"

echo ""
print_success "🎉 Publishing workflow triggered!"
echo ""
echo "Next steps:"
echo "  1. Monitor the GitHub Actions workflow:"
echo "     https://github.com/scottgal/mostlylucidweb/actions"
echo ""
echo "  2. Once published, check npm:"
echo "     https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements/v/$VERSION"
echo ""
echo "  3. Verify the GitHub Release:"
echo "     https://github.com/scottgal/mostlylucidweb/releases/tag/$TAG_NAME"
echo ""
