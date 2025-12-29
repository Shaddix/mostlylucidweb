# SegmentCommerce System Overview

This document describes the complete architecture and workflow of the SegmentCommerce demo e-commerce platform, including its sample data generation, LLM integration, and zero-PII customer profiling system.

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Project Structure](#project-structure)
3. [Data Generation Pipeline](#data-generation-pipeline)
4. [Database Schema](#database-schema)
5. [Image System](#image-system)
6. [Customer Profiling (Zero-PII)](#customer-profiling-zero-pii)
7. [Running the System](#running-the-system)
8. [Configuration Reference](#configuration-reference)

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SegmentCommerce System                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐       │
│  │   SampleData     │    │  SegmentCommerce │    │    PostgreSQL    │       │
│  │   CLI Tool       │───>│   Web App        │<──>│   (pgvector)     │       │
│  │                  │    │   :8082          │    │   :5432          │       │
│  └────────┬─────────┘    └────────┬─────────┘    └──────────────────┘       │
│           │                       │                                          │
│           v                       v                                          │
│  ┌──────────────────┐    ┌──────────────────┐                               │
│  │     Ollama       │    │  Image Storage   │                               │
│  │   (LLM/Embed)    │    │  D:\segmentdata  │                               │
│  │   :11434         │    │                  │                               │
│  └──────────────────┘    └──────────────────┘                               │
│                                                                              │
│  ┌──────────────────┐                                                       │
│  │    ComfyUI       │  (Optional - for generating product images)           │
│  │   :8188          │                                                       │
│  └──────────────────┘                                                       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Components

| Component | Purpose | Port |
|-----------|---------|------|
| **SegmentCommerce Web App** | ASP.NET Core MVC e-commerce site | 8082 |
| **SampleData CLI** | Data generation and database import tool | N/A |
| **PostgreSQL + pgvector** | Database with vector similarity search | 5432 |
| **Ollama** | Local LLM for text generation and embeddings | 11434 |
| **ComfyUI** | Stable Diffusion image generation (optional) | 8188 |

---

## Project Structure

```
C:\Blog\mostlylucidweb\
├── Mostlylucid.SegmentCommerce/           # Main web application
│   ├── Controllers/                        # MVC + API controllers
│   │   ├── Api/
│   │   │   ├── ImageController.cs          # Serves generated images
│   │   │   └── PlaceholderController.cs    # SVG placeholder generation
│   │   ├── HomeController.cs
│   │   ├── ProductsController.cs
│   │   └── ...
│   ├── Data/
│   │   ├── Entities/                       # EF Core entities
│   │   │   ├── ProductEntity.cs
│   │   │   ├── SellerEntity.cs
│   │   │   └── Profiles/
│   │   │       ├── PersistentProfileEntity.cs
│   │   │       └── SessionProfileEntity.cs
│   │   ├── Migrations/
│   │   └── SegmentCommerceDbContext.cs
│   ├── Services/
│   │   ├── Profiles/                       # Zero-PII profiling
│   │   ├── Segments/                       # Customer segmentation
│   │   ├── ProductService.cs
│   │   └── RecommendationService.cs
│   ├── Views/
│   ├── scripts/
│   │   └── puppeteer-snapshot.js           # Screenshot tests
│   ├── Program.cs
│   └── appsettings.json
│
├── Mostlylucid.SegmentCommerce.SampleData/ # Data generation CLI
│   ├── Commands/
│   │   ├── GenerateCommand.cs              # v1: taxonomy-based generation
│   │   ├── GenerateV2Command.cs            # v2: LLM-powered generation
│   │   ├── ImportCommand.cs                # Database import
│   │   ├── StatusCommand.cs                # Service health check
│   │   └── ClearCommand.cs                 # Database cleanup
│   ├── Services/
│   │   ├── LlmService.cs                   # Ollama API wrapper
│   │   ├── DataGenerator.cs                # Main orchestrator
│   │   ├── EmbeddingService.cs             # ONNX embeddings
│   │   ├── ComfyUIImageGenerator.cs        # Image generation
│   │   └── ...
│   ├── Models/
│   │   ├── GenerationModels.cs             # Generated data models
│   │   ├── GadgetTaxonomy.cs               # Product taxonomy
│   │   └── ...
│   ├── Data/
│   │   └── gadget-taxonomy.json            # Product categories/types
│   └── Program.cs
│
├── Mostlylucid.SegmentCommerce.Tests/      # Unit tests
│
└── D:\segmentdata\                         # Generated data (external)
    ├── sellers.json
    ├── products.json
    ├── customers.json
    ├── orders.json
    ├── dataset.json
    └── images/
        ├── tech/
        ├── fashion/
        ├── home/
        ├── sport/
        ├── books/
        └── food/
```

---

## Data Generation Pipeline

The SampleData CLI generates synthetic e-commerce data in a multi-phase pipeline:

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Phase 1   │───>│   Phase 2   │───>│   Phase 3   │───>│   Phase 4   │───>│   Phase 5   │
│   Sellers   │    │  Products   │    │  Customers  │    │   Orders    │    │ Embeddings  │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
      │                  │                  │                  │                  │
      v                  v                  v                  v                  v
   LLM/Fallback      LLM/Fallback       LLM/Fallback        Bogus            ONNX Model
   (personas)        (descriptions)     (personas)         (fake data)    (all-MiniLM-L6)
```

### Phase 1: Seller Generation

Each seller is assigned 1-3 product categories and gets:
- Business name (LLM-generated or template)
- Description and tagline
- Specialties
- Rating and review count

**LLM Prompt Example:**
```
Generate a unique e-commerce seller persona for these categories: Tech, Fashion.

Return JSON only:
{
  "name": "Creative business name (2-4 words)",
  "tagline": "Short catchy tagline",
  "description": "2-3 sentence business description",
  "specialties": ["specialty1", "specialty2", "specialty3"]
}
```

### Phase 2: Product Generation

Products are generated using:
1. **Taxonomy-based templates** (`Data/gadget-taxonomy.json`)
2. **Optional LLM enhancement** for descriptions

Each product includes:
- Name, description, price
- Category and tags
- Color variants
- Image prompt (for ComfyUI)
- Trending/featured flags

### Phase 3: Customer/Profile Generation

Customers are generated with:
- Anonymous profile key (SHA256 hash)
- Persona description
- Category interests (weighted scores)
- Price preferences
- Behavioral signals (views, cart adds, purchases)

**Zero-PII Design**: No real names, emails, or identifiable data.

### Phase 4: Order Generation

Orders are generated using **Bogus** library:
- Fake checkout data
- Order items linked to products
- Realistic timestamps

### Phase 5: Embedding Generation

Vector embeddings computed using local ONNX model:
- Model: `all-MiniLM-L6-v2` (384 dimensions)
- Applied to: sellers, products, customers
- Used for: similarity search, recommendations

---

## Database Schema

### Core Entities

```sql
-- Sellers
CREATE TABLE sellers (
    id UUID PRIMARY KEY,
    name VARCHAR(200),
    description TEXT,
    email VARCHAR(200),
    rating DECIMAL(3,2),
    review_count INT,
    is_verified BOOLEAN,
    is_active BOOLEAN
);

-- Products
CREATE TABLE products (
    id SERIAL PRIMARY KEY,
    seller_id UUID REFERENCES sellers(id),
    name VARCHAR(500),
    handle VARCHAR(200) UNIQUE,
    description TEXT,
    price DECIMAL(10,2),
    original_price DECIMAL(10,2),
    image_url VARCHAR(500),
    category VARCHAR(100),
    category_path VARCHAR(500),
    tags JSONB,
    status INT,
    is_trending BOOLEAN,
    is_featured BOOLEAN,
    color VARCHAR(100),
    size VARCHAR(50)
);

-- Product Variations (color/size variants)
CREATE TABLE product_variations (
    id SERIAL PRIMARY KEY,
    product_id INT REFERENCES products(id),
    color VARCHAR(100),
    size VARCHAR(50),
    price DECIMAL(10,2),
    stock_quantity INT,
    availability_status INT
);

-- Product Embeddings (pgvector)
CREATE TABLE product_embeddings (
    id SERIAL PRIMARY KEY,
    product_id INT REFERENCES products(id),
    embedding vector(384),
    model VARCHAR(100),
    source_text TEXT
);
```

### Profile Entities (Zero-PII)

```sql
-- Persistent Profiles (long-term behavioral data)
CREATE TABLE persistent_profiles (
    id UUID PRIMARY KEY,
    profile_key VARCHAR(256) UNIQUE,        -- Fingerprint hash
    identification_mode INT,
    
    -- Behavioral data (JSONB)
    interests JSONB,                         -- {"tech": 0.85, "fashion": 0.3}
    affinities JSONB,                        -- {"headphones": 0.9}
    brand_affinities JSONB,
    price_preferences JSONB,
    traits JSONB,
    
    -- Computed segments
    segments INT,                            -- Bitflags
    llm_segments JSONB,
    
    -- Embedding for similarity
    embedding vector(384),
    
    -- Statistics
    total_sessions INT,
    total_signals INT,
    total_purchases INT,
    total_cart_adds INT
);

-- Session Profiles (temporary, in-memory preferred)
CREATE TABLE session_profiles (
    id UUID PRIMARY KEY,
    session_id VARCHAR(100),
    profile_id UUID REFERENCES persistent_profiles(id),
    interests JSONB,
    signals JSONB,
    expires_at TIMESTAMP
);
```

### Current Data Counts

| Table | Count |
|-------|-------|
| sellers | 91 |
| products | 10,000 |
| product_variations | 30,000 |
| persistent_profiles | 5,000 |
| categories | 6 |

---

## Image System

### Image Sources

1. **AI-Generated Images** (ComfyUI/Stable Diffusion)
   - Location: `D:\segmentdata\images/{category}/{product_folder}/`
   - Served via: `ImageController` at `/api/images/products/...`
   - Format: PNG, 512x512

2. **SVG Placeholders** (for products without generated images)
   - Generated on-the-fly by `PlaceholderController`
   - Category-specific color schemes
   - Deterministic patterns based on product name hash

### Image Controller Flow

```
Request: GET /api/images/products/{category}/{productFolder}/{filename}
                              │
                              v
                 ┌────────────────────────┐
                 │  Check D:\segmentdata  │
                 │  \images\{path}        │
                 └───────────┬────────────┘
                             │
              ┌──────────────┴──────────────┐
              │                             │
         File Exists?                  File Not Found
              │                             │
              v                             v
       Return PNG                  Redirect to Placeholder
       (image/png)                 (/api/placeholder/...)
```

### Placeholder Color Schemes

| Category | Background | Foreground | Icon |
|----------|------------|------------|------|
| tech | #1a365d | #90cdf4 | CPU |
| fashion | #742a2a | #fed7d7 | Shirt |
| home | #22543d | #9ae6b4 | Home |
| sport | #744210 | #faf089 | Activity |
| books | #553c9a | #d6bcfa | Book |
| food | #7b341e | #fbd38d | Coffee |

---

## Customer Profiling (Zero-PII)

The system implements privacy-first customer profiling without storing any personally identifiable information.

### Identification Methods

1. **Browser Fingerprinting** (primary)
   - Collects: WebGL, Canvas, Audio fingerprints
   - Hashed with HMAC-SHA256
   - No cookies required

2. **Session-based** (fallback)
   - Temporary session ID
   - Signals elevated to persistent profile on session end

3. **Authenticated** (optional)
   - User ID from authentication system
   - Linked to persistent profile

### Signal Types

| Signal | Weight | Description |
|--------|--------|-------------|
| `view` | 0.1-0.3 | Product page view |
| `cart_add` | 0.3-0.6 | Added to cart |
| `purchase` | 0.8-1.0 | Completed purchase |
| `search` | 0.2-0.4 | Search query |

### Segment Computation

Segments are computed from behavioral data:

```csharp
[Flags]
public enum ProfileSegments
{
    None = 0,
    NewVisitor = 1 << 0,
    ReturningVisitor = 1 << 1,
    HighValue = 1 << 2,
    Bargain = 1 << 3,
    TechEnthusiast = 1 << 4,
    FashionFocused = 1 << 5,
    // ... more segments
}
```

### Decay Mechanism

Interest scores decay over time to reflect changing preferences:
- Recent signals weighted higher
- Older signals gradually reduce influence
- Prevents stale recommendations

---

## Running the System

### Prerequisites

1. **Docker** (for PostgreSQL)
2. **.NET 9 SDK**
3. **Node.js** (for Puppeteer tests)
4. **Ollama** (optional, for LLM features)

### Quick Start

```bash
# 1. Start PostgreSQL with pgvector
cd C:\Blog\mostlylucidweb\Mostlylucid.SegmentCommerce
docker compose up -d

# 2. Check service status
cd C:\Blog\mostlylucidweb
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- status

# 3. Import sample data (if D:\segmentdata exists)
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- import \
    --input "D:\segmentdata" \
    --connection "Host=localhost;Database=segmentcommerce;port=5432;Username=postgres;Password=postgres"

# 4. Or generate new data
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- gen \
    --sellers 50 --products 20 --customers 1000 \
    --output "D:\segmentdata"

# 5. Run the web application
dotnet run --project Mostlylucid.SegmentCommerce --urls http://localhost:8082

# 6. Run Puppeteer tests
cd Mostlylucid.SegmentCommerce
npm install
node scripts/puppeteer-snapshot.js
```

### SampleData CLI Commands

| Command | Description |
|---------|-------------|
| `status` | Check Ollama, ComfyUI, PostgreSQL availability |
| `list` | List taxonomy categories and product types |
| `generate` | v1 generator (taxonomy + optional Ollama/ComfyUI) |
| `gen` | v2 generator (full LLM-powered pipeline) |
| `import` | Import JSON data into PostgreSQL |
| `clear` | Clear all data from database |

### Common Workflows

**Generate fresh data with LLM:**
```bash
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- gen \
    --sellers 100 --products 50 --customers 5000 \
    --output "D:\segmentdata"
```

**Generate without LLM (faster, uses templates):**
```bash
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- gen \
    --no-llm --no-images \
    --sellers 50 --products 20 --customers 1000
```

**Import with database clear:**
```bash
dotnet run --project Mostlylucid.SegmentCommerce.SampleData -- import \
    --input "D:\segmentdata" --clear
```

---

## Configuration Reference

### Web App (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=segmentcommerce;port=5432;Username=postgres;Password=postgres"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text"
  },
  "ImageStorage": {
    "BasePath": "D:\\segmentdata\\images"
  },
  "ClientFingerprint": {
    "Enabled": true,
    "CollectWebGL": true,
    "CollectCanvas": true,
    "CollectAudio": true
  },
  "BackgroundWorkers": {
    "Enabled": false
  }
}
```

### SampleData CLI (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=segmentcommerce;port=5432;Username=postgres;Password=postgres"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder:7b",
    "TimeoutSeconds": 180
  },
  "ComfyUI": {
    "BaseUrl": "http://localhost:8188",
    "OutputPath": "./Output/images",
    "CheckpointName": "sd_xl_base_1.0.safetensors"
  },
  "Embedding": {
    "Enabled": true,
    "ModelPath": "./Models/model.onnx",
    "VectorSize": 384
  },
  "Generation": {
    "SellersCount": 20,
    "ProductsPerSeller": 10,
    "CustomersCount": 500
  }
}
```

### Environment Variables

Prefix: `SAMPLEDATA_`

```bash
# Override Ollama model
export SAMPLEDATA_Ollama__Model=llama3.2

# Override connection string
export SAMPLEDATA_ConnectionStrings__DefaultConnection="Host=..."
```

---

## Appendix: Key Files Reference

| File | Purpose |
|------|---------|
| `SegmentCommerce/Program.cs` | Web app entry, DI configuration |
| `SegmentCommerce/Data/SegmentCommerceDbContext.cs` | EF Core context, JSONB mapping |
| `SampleData/Program.cs` | CLI entry, command registration |
| `SampleData/Commands/ImportCommand.cs` | Database import logic |
| `SampleData/Services/DataGenerator.cs` | Main generation orchestrator |
| `SampleData/Services/LlmService.cs` | Ollama API wrapper |
| `SampleData/Data/gadget-taxonomy.json` | Product category definitions |
