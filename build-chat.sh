#!/bin/bash

# Build script for Chat System
# This script builds all components of the chat system

set -e  # Exit on error

echo "========================================="
echo "Building Chat System Components"
echo "========================================="

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Build Shared Library
echo -e "\n${BLUE}[1/4] Building Shared Library...${NC}"
dotnet build Mostlylucid.Chat.Shared/Mostlylucid.Chat.Shared.csproj -c Release
echo -e "${GREEN}✓ Shared library built${NC}"

# Build Server
echo -e "\n${BLUE}[2/4] Building Chat Server...${NC}"
dotnet build Mostlylucid.Chat.Server/Mostlylucid.Chat.Server.csproj -c Release
echo -e "${GREEN}✓ Chat server built${NC}"

# Build Widget
echo -e "\n${BLUE}[3/4] Building Chat Widget...${NC}"
cd Mostlylucid.Chat.Widget

if [ ! -d "node_modules" ]; then
    echo "Installing npm dependencies..."
    npm install
fi

npm run build
echo -e "${GREEN}✓ Chat widget built${NC}"

# Copy widget to server wwwroot
echo "Copying widget to server..."
mkdir -p ../Mostlylucid.Chat.Server/wwwroot
cp dist/widget.js ../Mostlylucid.Chat.Server/wwwroot/
echo -e "${GREEN}✓ Widget copied to server${NC}"

cd ..

# Build Tray App (Windows only)
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    echo -e "\n${BLUE}[4/4] Building Tray App (Windows)...${NC}"
    dotnet build Mostlylucid.Chat.TrayApp/Mostlylucid.Chat.TrayApp.csproj -c Release
    echo -e "${GREEN}✓ Tray app built${NC}"
else
    echo -e "\n${BLUE}[4/4] Skipping Tray App (Windows only)${NC}"
fi

echo -e "\n${GREEN}=========================================${NC}"
echo -e "${GREEN}Build completed successfully!${NC}"
echo -e "${GREEN}=========================================${NC}"

echo -e "\nNext steps:"
echo "  1. Start the server: cd Mostlylucid.Chat.Server && dotnet run"
echo "  2. Open example: Mostlylucid.Chat.Widget/examples/index.html"
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    echo "  3. Run tray app: cd Mostlylucid.Chat.TrayApp && dotnet run"
fi
