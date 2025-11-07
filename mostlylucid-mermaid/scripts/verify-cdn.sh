#!/bin/bash

# CDN Verification Script
# Checks if the package is available on unpkg and jsdelivr

set -e

PACKAGE_NAME="@mostlylucid/mermaid-enhancements"
VERSION=${1:-latest}

echo "🔍 Verifying CDN availability for $PACKAGE_NAME@$VERSION"
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check URL
check_url() {
    local url=$1
    local name=$2

    echo -n "Checking $name... "

    if curl -s -f -I "$url" > /dev/null; then
        echo -e "${GREEN}✓ Available${NC}"
        echo "  URL: $url"
        return 0
    else
        echo -e "${RED}✗ Not available yet${NC}"
        echo "  URL: $url"
        return 1
    fi
}

echo "📦 Package Information:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Check npm
echo ""
echo "NPM Registry:"
NPM_URL="https://registry.npmjs.org/$PACKAGE_NAME"
if check_url "$NPM_URL" "npm registry"; then
    # Get version info
    VERSION_INFO=$(curl -s "$NPM_URL" | grep -o '"latest":"[^"]*"' | head -1 || echo "")
    if [ -n "$VERSION_INFO" ]; then
        echo "  Latest: $VERSION_INFO"
    fi
fi

echo ""
echo "CDN Availability:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Check unpkg
echo ""
echo "1. unpkg:"
UNPKG_MAIN="https://unpkg.com/$PACKAGE_NAME@$VERSION"
UNPKG_MIN="https://unpkg.com/$PACKAGE_NAME@$VERSION/dist/index.min.js"
UNPKG_CSS="https://unpkg.com/$PACKAGE_NAME@$VERSION/src/styles.css"

check_url "$UNPKG_MAIN" "Main entry"
check_url "$UNPKG_MIN" "Minified bundle"
check_url "$UNPKG_CSS" "Styles"

# Check jsdelivr
echo ""
echo "2. jsdelivr:"
JSDELIVR_MAIN="https://cdn.jsdelivr.net/npm/$PACKAGE_NAME@$VERSION"
JSDELIVR_MIN="https://cdn.jsdelivr.net/npm/$PACKAGE_NAME@$VERSION/dist/index.min.js"
JSDELIVR_CSS="https://cdn.jsdelivr.net/npm/$PACKAGE_NAME@$VERSION/src/styles.css"

check_url "$JSDELIVR_MAIN" "Main entry"
check_url "$JSDELIVR_MIN" "Minified bundle"
check_url "$JSDELIVR_CSS" "Styles"

echo ""
echo "📝 Usage Examples:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "unpkg (ESM):"
echo "  import { init } from 'https://unpkg.com/$PACKAGE_NAME@$VERSION/dist/index.min.js';"
echo ""
echo "jsdelivr (ESM):"
echo "  import { init } from 'https://cdn.jsdelivr.net/npm/$PACKAGE_NAME@$VERSION/dist/index.min.js';"
echo ""
echo "CSS:"
echo "  <link rel=\"stylesheet\" href=\"https://unpkg.com/$PACKAGE_NAME@$VERSION/src/styles.css\">"
echo ""

echo ""
echo "⏱️  Note: CDNs can take 2-5 minutes to index new packages after publishing"
echo ""
