#!/bin/bash
# SegmentCommerce Sample Data Generator Runner
# Run this script to clear and regenerate sample data

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Default values
CLEAR=false
SKIP_CLEAR=false
NO_OLLAMA=false
NO_IMAGES=false
COUNT=20
CATEGORY=""
DRY_RUN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --clear|-c)
            CLEAR=true
            shift
            ;;
        --skip-clear)
            SKIP_CLEAR=true
            shift
            ;;
        --no-ollama)
            NO_OLLAMA=true
            shift
            ;;
        --no-images)
            NO_IMAGES=true
            shift
            ;;
        --count|-n)
            COUNT="$2"
            shift 2
            ;;
        --category)
            CATEGORY="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help|-h)
            echo "SegmentCommerce Sample Data Generator"
            echo ""
            echo "Usage:"
            echo "    ./run-generate.sh [options]"
            echo ""
            echo "Options:"
            echo "    --clear, -c     Clear database before generating"
            echo "    --skip-clear    Skip the clear step entirely"
            echo "    --no-ollama     Skip Ollama enhancement (faster)"
            echo "    --no-images     Skip image generation (faster)"
            echo "    --count, -n <n> Number of products per category (default: 20)"
            echo "    --category <c>  Generate for specific category only"
            echo "    --dry-run       Preview without writing anything"
            echo "    --help, -h      Show this help message"
            echo ""
            echo "Examples:"
            echo "    ./run-generate.sh --clear --count 50"
            echo "    ./run-generate.sh --no-ollama --no-images --count 10"
            echo "    ./run-generate.sh --category tech --count 30"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo ""
echo "============================================"
echo " SegmentCommerce Sample Data Generator"
echo "============================================"
echo ""

# Build the project first
echo "[1/4] Building project..."
dotnet build "$SCRIPT_DIR/Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --nologo -v q
echo "Build successful!"

# Clear database if requested
if [ "$CLEAR" = true ] && [ "$SKIP_CLEAR" = false ] && [ "$DRY_RUN" = false ]; then
    echo ""
    echo "[2/4] Clearing database..."
    dotnet run --project "$SCRIPT_DIR/Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --no-build -- clear --confirm
    echo "Database cleared!"
elif [ "$SKIP_CLEAR" = true ]; then
    echo ""
    echo "[2/4] Skipping database clear"
else
    echo ""
    echo "[2/4] No clear requested (use --clear to clear first)"
fi

# Generate products
echo ""
echo "[3/4] Generating sample data..."

GENERATE_ARGS="generate --db --count $COUNT"

if [ "$NO_OLLAMA" = true ]; then
    GENERATE_ARGS="$GENERATE_ARGS --no-ollama"
fi
if [ "$NO_IMAGES" = true ]; then
    GENERATE_ARGS="$GENERATE_ARGS --no-images"
fi
if [ -n "$CATEGORY" ]; then
    GENERATE_ARGS="$GENERATE_ARGS --category $CATEGORY"
fi
if [ "$DRY_RUN" = true ]; then
    GENERATE_ARGS="$GENERATE_ARGS --dry-run"
fi

echo "Running: dotnet run -- $GENERATE_ARGS"
dotnet run --project "$SCRIPT_DIR/Mostlylucid.SegmentCommerce.SampleData.csproj" -c Release --no-build -- $GENERATE_ARGS

echo ""
echo "[4/4] Complete!"
echo ""
echo "============================================"
echo " Generation Complete"
echo "============================================"

if [ "$DRY_RUN" = false ]; then
    echo ""
    echo "Output files saved to: ./Output"
    echo "Run the main app to see the products:"
    echo "  cd ../Mostlylucid.SegmentCommerce && dotnet run"
fi
