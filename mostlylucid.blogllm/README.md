# mostlylucid.blogllm

A CPU-optimized RAG (Retrieval Augmented Generation) knowledge base builder for markdown documents. Transform your blog posts into a searchable, semantic vector database.

## Features

✨ **CPU-Friendly** - Optimized for servers without GPU
📝 **Markdown Native** - Parse blog posts with metadata extraction
🔍 **Semantic Search** - Find content by meaning, not just keywords
🎯 **Smart Chunking** - Intelligent text segmentation with overlap
💾 **Vector Storage** - Uses Qdrant for efficient similarity search
🎨 **Beautiful CLI** - Interactive terminal UI with Spectre.Console
⚙️ **Configurable** - Tune all parameters via UI or config file

## Quick Start

### Prerequisites

1. **Qdrant Vector Database**
   ```bash
   docker run -p 6333:6333 -p 6334:6334 -v ./qdrant_storage:/qdrant/storage qdrant/qdrant
   ```

2. **Embedding Model** (BGE-small-en-v1.5 - CPU optimized)
   ```bash
   # Install huggingface-cli
   pip install huggingface-hub

   # Download model
   mkdir -p models
   huggingface-cli download BAAI/bge-small-en-v1.5 --local-dir ./models/bge-small-en-v1.5-onnx
   ```

### Installation

```bash
cd mostlylucid.blogllm
dotnet build
dotnet run
```

## Usage

### 1. Ingest Documents

The tool will parse your markdown files, extract metadata, chunk the content intelligently, and generate embeddings.

```
📄 Ingest Markdown Documents
  → Enter path: /path/to/your/blog/posts
  → Processes all .md files recursively
```

**What gets extracted:**
- Title (from first `# Heading`)
- Categories (from `<!-- category -- Cat1, Cat2 -->`)
- Published date (from `<datetime>` tags)
- Language (from filename: `post.es.md` = Spanish)
- Code blocks with language tags
- Section structure

### 2. Search Knowledge Base

Semantic search across all ingested content:

```
🔍 Search Knowledge Base
  → Query: "how to use docker compose"
  → Returns top N most relevant chunks
  → Similarity scores show relevance
```

### 3. Configure Settings

Tune the system to your needs:

- **Embedding Model**: Path to ONNX model and tokenizer
- **Qdrant Connection**: Host, port, collection name
- **Chunking Parameters**: Max/min tokens, overlap
- **GPU Usage**: Enable/disable (off by default for CPU)

### 4. View Statistics

See how much content you've indexed:

```
📊 Show Statistics
  → Total chunks in database
  → Collection details
```

## Configuration

Edit `appsettings.json` or use the interactive CLI:

```json
{
  "BlogRag": {
    "EmbeddingModel": {
      "ModelPath": "./models/bge-small-en-v1.5-onnx/model.onnx",
      "TokenizerPath": "./models/bge-small-en-v1.5-onnx/tokenizer.json",
      "Dimensions": 384,
      "UseGpu": false
    },
    "VectorStore": {
      "Host": "localhost",
      "Port": 6334,
      "CollectionName": "blog_knowledge_base",
      "ApiKey": ""
    },
    "Chunking": {
      "MaxChunkTokens": 512,
      "MinChunkTokens": 100,
      "OverlapTokens": 50
    }
  }
}
```

## Architecture

```
┌─────────────────┐
│  Markdown Files │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Markdown Parser │  → Extracts metadata & structure
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Chunking Service│  → Splits into semantic chunks
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│Embedding Service│  → Generates 384-dim vectors
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Qdrant DB     │  → Stores vectors + metadata
└─────────────────┘
```

## Markdown Format

Your markdown files should follow this structure:

```markdown
# Post Title

<!-- category -- Tech, AI, Tutorial -->
<datetime class="hidden">2025-01-15T12:00</datetime>

## Introduction

Your content here...

## Section 1

More content...

```csharp
// Code blocks are preserved
public class Example { }
```
```

## Performance

On a typical CPU server:
- **Parsing**: ~100 files/second
- **Chunking**: ~50 chunks/second
- **Embedding**: ~10-20 chunks/second (CPU)
- **Search**: <100ms for top 10 results

For GPU acceleration, set `UseGpu: true` in config (requires CUDA).

## Use Cases

### 1. Blog Writing Assistant
Build a knowledge base of your blog to help write new posts:
```bash
# Ingest all your posts
mostlylucid.blogllm ingest /blog/posts

# Search when writing
mostlylucid.blogllm search "docker compose setup"
```

### 2. Documentation Search
Make your docs semantically searchable:
```bash
mostlylucid.blogllm ingest /docs
```

### 3. Research Assistant
Index research papers, notes, or articles:
```bash
mostlylucid.blogllm ingest /research
```

## Advanced Features

### Language Detection
Automatically detects language from filename:
- `post.md` → English
- `post.es.md` → Spanish
- `post.fr.md` → French

### Smart Chunking
- Respects section boundaries
- Maintains context with heading hierarchy
- Overlaps chunks for continuity
- Preserves code blocks intact

### Metadata Filtering
Search with filters (via SDK):
```csharp
var results = await vectorStore.SearchAsync(
    queryEmbedding: embedding,
    limit: 10,
    languageFilter: "en"
);
```

## Troubleshooting

### "Model not found"
Download the embedding model:
```bash
huggingface-cli download BAAI/bge-small-en-v1.5 --local-dir ./models/bge-small-en-v1.5-onnx
```

### "Cannot connect to Qdrant"
Ensure Qdrant is running:
```bash
docker ps | grep qdrant
```

### "Out of memory"
Reduce batch size or chunk size in config:
```json
{
  "Chunking": {
    "MaxChunkTokens": 256
  }
}
```

## License

MIT

## Related Projects

This tool is part of the "Building a Lawyer GPT for Your Blog" series:
- [Part 1: Introduction & Architecture](/blog/building-a-lawyer-gpt-for-your-blog-part1)
- [Part 2: GPU Setup](/blog/building-a-lawyer-gpt-for-your-blog-part2)
- [Part 3: Embeddings](/blog/building-a-lawyer-gpt-for-your-blog-part3)
- [Part 4: Ingestion Pipeline](/blog/building-a-lawyer-gpt-for-your-blog-part4)

## Contributing

PRs welcome! This is a production tool extracted from the blog series.

## Credits

Built with:
- [Markdig](https://github.com/xoofx/markdig) - Markdown parsing
- [ONNX Runtime](https://onnxruntime.ai/) - ML inference
- [Qdrant](https://qdrant.tech/) - Vector database
- [Spectre.Console](https://spectreconsole.net/) - Beautiful CLI
- [BGE-small-en-v1.5](https://huggingface.co/BAAI/bge-small-en-v1.5) - Embedding model
