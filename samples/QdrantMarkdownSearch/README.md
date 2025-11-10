# Qdrant Markdown Search Sample

A minimal, self-hosted semantic search engine demonstrating Qdrant vector database with ASP.NET Core 9.0 and local Ollama embeddings.

**100% self-hosted • 0% API costs • Complete privacy**

## What This Demonstrates

This sample shows how to build a semantic search system that:
- Indexes markdown files into a vector database
- Uses locally-run AI models (no external APIs)
- Provides semantic search capabilities
- Runs entirely in Docker containers
- Costs nothing to operate

## Features

- **Self-Hosted Vector Search**: Qdrant running in Docker
- **Local AI Embeddings**: Ollama with nomic-embed-text model
- **Markdown Indexing**: Automatically indexes .md files on startup
- **RESTful API**: Simple search and stats endpoints
- **Web Interface**: Built-in search UI
- **Zero Cloud Dependencies**: Everything runs on your machine

## Architecture

```
┌─────────────────┐
│   ASP.NET Core  │
│   Web API       │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼──────┐
│Qdrant │ │ Ollama  │
│Vector │ │Embedding│
│  DB   │ │ Model   │
└───────┘ └─────────┘
```

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/get-started) and Docker Compose
- At least 2GB RAM free (for Ollama model)

## Quick Start

### 1. Clone and Navigate

```bash
cd samples/QdrantMarkdownSearch
```

### 2. Start Docker Services

This will start Qdrant and Ollama (and pull the embedding model):

```bash
docker-compose up -d
```

**Note**: First time setup takes a few minutes to download the Ollama embedding model (~270MB).

Check the logs to see when Ollama is ready:

```bash
docker-compose logs -f ollama
```

Wait for the message: `successfully pulled nomic-embed-text`

### 3. Run the Application

```bash
dotnet restore
dotnet run
```

The application will:
1. Initialize the Qdrant collection
2. Index all markdown files from `MarkdownDocs/`
3. Start the web server on `http://localhost:5000`

### 4. Try It Out

Open your browser to: http://localhost:5000

You'll see a search interface. Try searching for:
- "what is semantic search"
- "privacy benefits"
- "vector databases explained"
- "local AI models"

The search understands meaning, not just keywords!

## API Endpoints

### Search

```bash
GET /api/search?query=your+search+term&limit=10
```

Example:
```bash
curl "http://localhost:5000/api/search?query=vector%20embeddings&limit=5"
```

Response:
```json
{
  "query": "vector embeddings",
  "results": [
    {
      "id": "abc-123",
      "title": "Understanding Vector Databases",
      "content": "A vector database stores high-dimensional vectors...",
      "fileName": "vector-databases.md",
      "score": 0.89
    }
  ],
  "count": 5,
  "searchTimeMs": 45
}
```

### Stats

```bash
GET /api/stats
```

Returns indexing statistics and configuration.

### Health Check

```bash
GET /health
```

Simple health check endpoint.

## Project Structure

```
QdrantMarkdownSearch/
├── Models/
│   └── SearchResult.cs          # Search result models
├── Services/
│   ├── IEmbeddingService.cs     # Embedding service interface
│   ├── OllamaEmbeddingService.cs # Ollama implementation
│   ├── IVectorSearchService.cs  # Search service interface
│   ├── QdrantVectorSearchService.cs # Qdrant implementation
│   └── MarkdownIndexingService.cs # Background indexer
├── MarkdownDocs/                 # Your markdown files go here
│   ├── getting-started.md
│   ├── vector-databases.md
│   └── ollama-embeddings.md
├── Program.cs                    # Main application & API
├── appsettings.json             # Configuration
├── docker-compose.yml           # Docker services
└── README.md                    # This file
```

## Adding Your Own Content

Simply drop markdown files into the `MarkdownDocs/` directory and restart the application:

```bash
# Add your files
cp ~/my-docs/*.md MarkdownDocs/

# Restart to re-index
dotnet run
```

The indexer will:
1. Extract the title from the first `#` heading
2. Convert markdown to plain text
3. Generate embeddings via Ollama
4. Store in Qdrant with metadata

## Configuration

Edit `appsettings.json` to customize:

```json
{
  "Qdrant": {
    "Endpoint": "http://localhost:6333",
    "CollectionName": "markdown_docs",
    "VectorSize": 768
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "nomic-embed-text"
  },
  "MarkdownPath": "MarkdownDocs"
}
```

## How It Works

### 1. Markdown Indexing

On startup, `MarkdownIndexingService`:
- Scans the `MarkdownDocs/` directory
- Extracts titles from markdown headers
- Converts markdown to plain text
- Sends each document for embedding generation

### 2. Embedding Generation

`OllamaEmbeddingService`:
- Sends text to local Ollama API
- Receives 768-dimensional vector
- No external API calls, completely private

### 3. Vector Storage

`QdrantVectorSearchService`:
- Creates a collection with cosine similarity
- Stores vectors with metadata (title, filename, content)
- Uses upsert for idempotent indexing

### 4. Semantic Search

When you search:
1. Query is converted to a vector via Ollama
2. Qdrant finds similar vectors using cosine similarity
3. Results are ranked by similarity score
4. Metadata and content are returned

## Understanding the Results

The `score` in search results is a similarity score between 0 and 1:
- **0.8-1.0**: Highly relevant, very similar content
- **0.6-0.8**: Relevant, somewhat similar content
- **0.4-0.6**: Possibly relevant, loosely related
- **0.0-0.4**: Not very relevant

Cosine similarity is used, which measures the angle between vectors.

## Performance

On a typical development machine:
- **Indexing**: ~200ms per document
- **Search**: ~50-100ms per query
- **Memory**: ~300MB for the app + 1GB for Ollama
- **Disk**: ~500MB for Ollama model

The sample can handle:
- Thousands of documents
- Concurrent searches
- Real-time indexing

## Troubleshooting

### Ollama not responding

```bash
# Check if Ollama is running
docker-compose ps

# View Ollama logs
docker-compose logs ollama

# Restart Ollama
docker-compose restart ollama
```

### Qdrant connection failed

```bash
# Check if Qdrant is running
docker-compose ps

# View Qdrant logs
docker-compose logs qdrant

# Qdrant dashboard
open http://localhost:6333/dashboard
```

### Port conflicts

If ports 6333, 6334, or 11434 are already in use, edit `docker-compose.yml`:

```yaml
ports:
  - "6335:6333"  # Use different external port
```

### Slow embedding generation

First run is slower as models are loaded into memory. Subsequent requests are much faster. Consider:
- Using a GPU-enabled Ollama container for faster embeddings
- Increasing Docker memory allocation
- Using a smaller embedding model

## Extending the Sample

Ideas for enhancements:

### Use Different Embedding Models

```bash
# Pull a different model
docker exec -it ollama-sample ollama pull all-minilm

# Update appsettings.json
"Ollama": {
  "Model": "all-minilm"
}
```

### Add Filtering

Extend the search to filter by metadata:

```csharp
// In QdrantVectorSearchService.cs
var filter = new Filter
{
    Must = {
        new Condition {
            Field = new Field { Key = "category" },
            Match = new Match { Keyword = "tutorials" }
        }
    }
};
```

### Watch for File Changes

Add a `FileSystemWatcher` to automatically re-index when markdown files change:

```csharp
var watcher = new FileSystemWatcher("MarkdownDocs");
watcher.Changed += async (s, e) => await ReindexFile(e.FullPath);
watcher.EnableRaisingEvents = true;
```

### Add Authentication

Protect your API with authentication:

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer();
```

## Cost Comparison

Running this sample:
- **Self-hosted**: $0/month (uses your local machine)
- **Equivalent with OpenAI embeddings**: ~$0.02 per 1M tokens
- **Managed Pinecone**: $70/month minimum
- **Managed Weaviate**: $25/month minimum

**Annual savings: $300-840** for a small application!

## Resources

- [Qdrant Documentation](https://qdrant.tech/documentation/)
- [Ollama Documentation](https://ollama.ai/docs)
- [Nomic Embed Text Model](https://huggingface.co/nomic-ai/nomic-embed-text-v1)
- [Full Blog Post](../../Mostlylucid/Markdown/qdrantwithaspdotnetcore.md)

## Learn More

This sample is part of the [mostlylucid blog](https://github.com/scottgal/mostlylucidweb) project.

For a production implementation with more features, see the main blog application which includes:
- Full-text search fallback
- Multi-language support
- Hybrid search (vector + filters)
- Result caching
- Monitoring and metrics

## License

This sample is part of the mostlylucid blog project and is available under the same license.

## Questions or Issues?

Open an issue on the [GitHub repository](https://github.com/scottgal/mostlylucidweb/issues).

---

**Happy self-hosting!** 🚀

Remember: You own your data, you control your infrastructure, and you pay zero API fees.
