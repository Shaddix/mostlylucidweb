# Semantic Audience Segmentation Demo

This demo application showcases semantic-based audience segmentation using .NET, ONNX Runtime, and Ollama.

## Features

- **Product Generation**: Uses Ollama to generate realistic ecommerce products
- **Semantic Embeddings**: Leverages ONNX Runtime (all-MiniLM-L6-v2) for creating product and customer embeddings
- **K-means Clustering**: Discovers customer segments automatically based on behavioral similarity
- **Real-time Segmentation**: Updates customer segments as they interact with products
- **CPU-Friendly**: Runs entirely on CPU, no GPU required

## Prerequisites

1. **.NET 9 SDK** - https://dotnet.microsoft.com/download
2. **Ollama** - https://ollama.ai/
   ```bash
   # Install Ollama
   curl https://ollama.ai/install.sh | sh

   # Pull the model
   ollama pull llama3.2:3b
   ```

3. **ONNX Model** - Download the embedding model:
   ```bash
   cd ../Mostlylucid.SemanticSearch
   chmod +x download-models.sh
   ./download-models.sh
   ```

## Running the Demo

```bash
dotnet run
```

Navigate to: http://localhost:5000

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Product Catalog (Ollama Generated)            │
│  ↓                                              │
│  ONNX Embeddings (384-dim vectors)             │
└─────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────┐
│  Customer Behavior Tracking                     │
│  - Views, Searches, Purchases                   │
│  ↓                                              │
│  Customer Profile Embeddings                    │
└─────────────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────────────┐
│  K-means Clustering                             │
│  ↓                                              │
│  Discovered Customer Segments                   │
│  ↓                                              │
│  Personalized Recommendations                   │
└─────────────────────────────────────────────────┘
```

## How It Works

1. **Product Generation**: Ollama generates 50+ diverse products across 8 categories
2. **Embedding Creation**: Each product gets a 384-dimensional semantic embedding
3. **Customer Simulation**: Synthetic customers with realistic browsing/purchase behavior
4. **Profile Building**: Customer interactions create semantic profile embeddings
5. **Clustering**: K-means discovers 5-10 natural customer segments
6. **Real-time Updates**: As customers interact, segments adapt dynamically

## Key Components

### Services

- **OllamaProductGenerator**: Generates realistic products using LLM
- **SemanticSegmentationService**: Performs K-means clustering on customer embeddings
- **CustomerProfileService**: Builds and updates customer profile embeddings

### Models

- **Product**: Product with semantic embedding
- **Customer**: Customer with behavioral data and profile embedding
- **CustomerSegment**: Discovered segment with centroid and characteristics
- **SegmentationResult**: Real-time analysis result

## Example Segments Discovered

After running with 100 synthetic customers:

- **Tech Enthusiasts** (23 customers) - Electronics, gadgets, innovation
- **Budget-Conscious Families** (19 customers) - Value, affordability, bulk items
- **Eco-Conscious Health** (18 customers) - Sustainable, organic, wellness
- **Luxury Seekers** (21 customers) - Premium, designer, exclusive
- **Creative Professionals** (19 customers) - Design, art, creative tools

## Performance

CPU-only performance on modest hardware:

- Product embedding generation: ~80ms per product
- Customer profile building: ~100ms per customer
- K-means clustering (100 customers, 5 segments): ~200ms
- Real-time segment assignment: ~150ms

## Related Articles

See the full blog article: [Semantic Audience Segmentation](/blog/semantic-audience-segmentation)

Also check out:
- [Semantic Search with ONNX and Qdrant](/blog/semantic-search-with-onnx-and-qdrant)
- [Semantic Intelligence Series](/blog/semanticintelligence-part8)

## License

MIT
