#!/bin/bash

# Build script for creating obfuscated JavaScript files
# This demonstrates the concept - production would use Node.js tools

echo "🔨 Building obfuscated JavaScript files..."
echo ""

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_PATH="$PROJECT_ROOT/wwwroot/js/dev"
OUTPUT_PATH="$PROJECT_ROOT/wwwroot/js"

echo "📁 Dev source: $DEV_PATH"
echo "📁 Output: $OUTPUT_PATH"
echo ""

# Check if source files exist
if [ ! -f "$DEV_PATH/compatibility-shim1.src.js" ]; then
    echo "❌ Source file not found: compatibility-shim1.src.js"
    exit 1
fi

if [ ! -f "$DEV_PATH/secure-chat.src.js" ]; then
    echo "❌ Source file not found: secure-chat.src.js"
    exit 1
fi

echo "✅ Source files found"
echo ""
echo "📝 Note: This script is a placeholder. Real build would:"
echo "   - Use Terser or UglifyJS for minification"
echo "   - Apply custom string encryption"
echo "   - Obfuscate control flow"
echo "   - Add anti-debugging measures"
echo ""
echo "For this demo, manually minified versions are provided."
echo ""

# In a real project, you'd run something like:
# npx terser $DEV_PATH/compatibility-shim1.src.js -c -m -o $OUTPUT_PATH/compatibility-shim1.js
# Or use the C# build tool:
# dotnet run --project Build/BuildObfuscated.csproj

# Check current file sizes
if [ -f "$OUTPUT_PATH/compatibility-shim1.js" ]; then
    SHIM_SIZE=$(stat -f%z "$OUTPUT_PATH/compatibility-shim1.js" 2>/dev/null || stat -c%s "$OUTPUT_PATH/compatibility-shim1.js" 2>/dev/null)
    echo "📊 compatibility-shim1.js: $SHIM_SIZE bytes"
fi

if [ -f "$OUTPUT_PATH/secure-chat.js" ]; then
    CHAT_SIZE=$(stat -f%z "$OUTPUT_PATH/secure-chat.js" 2>/dev/null || stat -c%s "$OUTPUT_PATH/secure-chat.js" 2>/dev/null)
    echo "📊 secure-chat.js: $CHAT_SIZE bytes"
fi

echo ""
echo "✅ Build complete!"
