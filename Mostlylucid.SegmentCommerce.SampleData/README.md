# SegmentCommerce Sample Data Generator

CLI tool for generating realistic e-commerce product data using:
- **Taxonomy-based generation** - Structured product definitions from `gadget-taxonomy.json`
- **Ollama enhancement** - AI-powered description improvement (optional)
- **ComfyUI image generation** - Product photography via Stable Diffusion (optional)

## Quick Start

```bash
# Check service status
dotnet run -- status

# List available categories
dotnet run -- list

# Generate products (taxonomy only, fast)
dotnet run -- generate --no-ollama --no-images

# Generate with Ollama enhancement
dotnet run -- generate --no-images

# Generate with images (requires ComfyUI)
dotnet run -- generate

# Generate and write to database
dotnet run -- generate --db
```

## Prerequisites

### Ollama (optional, for enhanced descriptions)
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull a model
ollama pull llama3.2

# Start Ollama
ollama serve
```

### ComfyUI (optional, for image generation)
```bash
# Start ComfyUI with Docker (requires NVIDIA GPU)
docker compose -f docker-compose.comfyui.yml up -d

# Download SDXL model (first time only)
# Place sd_xl_base_1.0.safetensors in the ComfyUI models/checkpoints folder
```

### PostgreSQL (for --db flag)
```bash
# Use the main project's dev dependencies
docker compose -f ../devdeps-docker-compose.yml up -d
```

## Commands

### `generate`
Generate sample product data.

```bash
Options:
  -c, --category <CATEGORY>  Generate for specific category only
  -n, --count <COUNT>        Products per category (default: 10)
  --no-ollama                Skip Ollama enhancement
  --no-images                Skip ComfyUI image generation
  -o, --output <PATH>        Output directory (default: ./Output)
  --db                       Write products to PostgreSQL
  --connection <STRING>      Override database connection string
  --dry-run                  Preview without writing
```

Examples:
```bash
# Quick test - 5 tech products, no AI
dotnet run -- generate -c tech -n 5 --no-ollama --no-images --dry-run

# Full generation for one category
dotnet run -- generate -c tech -n 20 --db

# All categories to JSON
dotnet run -- generate -o ./data --no-images
```

### `list`
Explore the gadget taxonomy.

```bash
# Show all categories
dotnet run -- list

# Show tech category details
dotnet run -- list -c tech

# Export as JSON
dotnet run -- list --json > taxonomy-summary.json
```

### `status`
Check if required services are available.

```bash
dotnet run -- status
```

## Gadget Taxonomy

The taxonomy (`Data/gadget-taxonomy.json`) defines:

- **Categories**: tech, fashion, home, sport, books, food
- **Subcategories**: audio, computing, wearables, etc.
- **Product types**: headphones, keyboards, smartwatches, etc.
- **Attributes**: variants, features, brands, colours, materials
- **Price ranges**: Per product type or category defaults
- **Image prompts**: Templates for ComfyUI generation

### Adding Custom Products

Edit `Data/gadget-taxonomy.json`:

```json
{
  "type": "your-product-type",
  "variants": ["variant1", "variant2"],
  "features": ["feature1", "feature2"],
  "priceRange": { "min": 29.99, "max": 199.99 },
  "brands": ["Brand1", "Brand2"],
  "colours": ["Black", "White"],
  "imagePrompt": "Professional photo of {variant} {type}, {features}"
}
```

## Output

Generated data is saved to:
- `./Output/products.json` - All products as JSON
- `./Output/images/<category>/<product>/` - Generated images

## Configuration

Edit `appsettings.json`:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2"
  },
  "ComfyUI": {
    "BaseUrl": "http://localhost:8188"
  },
  "Generation": {
    "ProductsPerCategory": 10,
    "ImagesPerProduct": 3,
    "ImageWidth": 512,
    "ImageHeight": 512
  }
}
```

Environment variables (prefix `SAMPLEDATA_`):
```bash
export SAMPLEDATA_Ollama__Model=mistral
export SAMPLEDATA_ConnectionStrings__DefaultConnection="Host=..."
```
