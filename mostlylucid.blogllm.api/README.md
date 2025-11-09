# Mostlylucid BlogLLM API

REST API server for RAG (Retrieval Augmented Generation) powered by local LLMs. This service provides semantic search and intelligent question-answering capabilities over your blog's knowledge base.

## Features

- **RAG Question Answering** - Ask questions and get AI-generated answers based on blog content
- **Semantic Search** - Find relevant content by meaning, not just keywords
- **Local LLM Inference** - Runs entirely on your infrastructure (no external API calls)
- **GPU Acceleration** - CUDA support for fast inference
- **Vector Search** - Qdrant integration for efficient similarity search
- **REST API** - Easy integration with any web application
- **Docker Ready** - Production-ready containerization

## Prerequisites

### Required

1. **Qdrant Vector Database**
   ```bash
   docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
   ```

2. **Embedding Model** (BGE-small-en-v1.5 - ONNX format)
   ```bash
   pip install huggingface-hub
   mkdir -p models
   huggingface-cli download BAAI/bge-small-en-v1.5 --local-dir ./models/bge-small-en-v1.5-onnx
   ```

3. **LLM Model** (Mistral 7B or similar - GGUF format)
   ```bash
   # Download from Hugging Face (example: Mistral 7B Instruct)
   huggingface-cli download TheBloke/Mistral-7B-Instruct-v0.2-GGUF \
     mistral-7b-instruct-v0.2.Q4_K_M.gguf \
     --local-dir ./models
   ```

### Recommended Hardware

**For GPU inference (recommended):**
- NVIDIA GPU with 8GB+ VRAM (RTX 3060, A4000, etc.)
- CUDA 12.x installed
- 16GB+ system RAM

**For CPU inference (slower but works):**
- Modern CPU (8+ cores recommended)
- 32GB+ system RAM

## Quick Start

### Development

1. **Install models** (see Prerequisites above)

2. **Configure settings** in `appsettings.json`:
   ```json
   {
     "BlogRag": {
       "EmbeddingModel": {
         "ModelPath": "./models/bge-small-en-v1.5-onnx/model.onnx",
         "TokenizerPath": "./models/bge-small-en-v1.5-onnx/tokenizer.json",
         "Dimensions": 384,
         "UseGpu": false
       },
       "LlmModel": {
         "ModelPath": "./models/mistral-7b-instruct-v0.2.Q4_K_M.gguf",
         "ContextSize": 4096,
         "GpuLayers": 20
       },
       "VectorStore": {
         "Host": "localhost",
         "Port": 6334,
         "CollectionName": "blog_knowledge_base"
       }
     }
   }
   ```

3. **Run the API**:
   ```bash
   cd mostlylucid.blogllm.api
   dotnet run
   ```

4. **Access Swagger UI**: http://localhost:5000/swagger

### Production (Docker)

```bash
docker-compose up -d
```

The API will be available at http://localhost:5100

## API Endpoints

### 1. Health Check

**GET** `/health`

Check if the API and dependencies are healthy.

**Response:**
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "self", "status": "Healthy" },
    { "name": "vector_store", "status": "Healthy", "description": "Vector store is accessible" }
  ]
}
```

### 2. RAG Question Answering

**POST** `/api/rag/ask`

Ask a question and get an AI-generated answer based on blog content.

**Request:**
```json
{
  "question": "How do I set up Docker Compose for a .NET application?",
  "maxContextChunks": 5,
  "scoreThreshold": 0.7,
  "maxTokens": 512,
  "temperature": 0.7,
  "language": "en"
}
```

**Response:**
```json
{
  "answer": "To set up Docker Compose for a .NET application, you need to...",
  "context": [
    {
      "text": "Docker Compose allows you to define multi-container applications...",
      "documentTitle": "Getting Started with Docker Compose",
      "sectionHeading": "Introduction",
      "score": 0.89,
      "categories": ["Docker", "DevOps"],
      "language": "en"
    }
  ],
  "processingTimeMs": 3421,
  "tokensGenerated": 287
}
```

**Parameters:**
- `question` (required): The user's question
- `maxContextChunks` (optional, default: 5): Number of relevant chunks to retrieve
- `scoreThreshold` (optional, default: 0.7): Minimum similarity score (0.0-1.0)
- `maxTokens` (optional, default: 512): Maximum tokens to generate
- `temperature` (optional, default: 0.7): LLM temperature (0.0-1.0, higher = more creative)
- `language` (optional): Filter by language code (e.g., "en", "es")

### 3. Semantic Search

**POST** `/api/search`

Search the knowledge base for relevant content.

**Request:**
```json
{
  "query": "docker compose configuration",
  "limit": 10,
  "scoreThreshold": 0.7,
  "language": "en",
  "categories": ["Docker", "DevOps"]
}
```

**Response:**
```json
{
  "results": [
    {
      "text": "Docker Compose configuration goes in docker-compose.yml...",
      "documentTitle": "Docker Compose Guide",
      "sectionHeading": "Configuration",
      "score": 0.92,
      "categories": ["Docker", "DevOps"],
      "language": "en"
    }
  ],
  "totalResults": 8,
  "searchTimeMs": 45
}
```

**Parameters:**
- `query` (required): Search query text
- `limit` (optional, default: 10): Maximum results to return
- `scoreThreshold` (optional, default: 0.7): Minimum similarity score
- `language` (optional): Filter by language
- `categories` (optional): Filter by categories

### 4. API Information

**GET** `/api/info`

Get information about the API and loaded models.

**Response:**
```json
{
  "name": "Mostlylucid BlogLLM API",
  "version": "1.0.0",
  "description": "RAG-powered blog assistant with local LLM inference",
  "endpoints": [...],
  "models": {
    "embedding": "./models/bge-small-en-v1.5-onnx/model.onnx",
    "llm": "./models/mistral-7b-instruct-v0.2.Q4_K_M.gguf",
    "vectorStore": "localhost:6334"
  }
}
```

## Integration with Blog App

### C# Example (HttpClient)

```csharp
using System.Net.Http.Json;

public class BlogLlmClient
{
    private readonly HttpClient _httpClient;

    public BlogLlmClient(string baseUrl = "http://localhost:5100")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<RagResponse> AskAsync(string question, string? language = null)
    {
        var request = new
        {
            question,
            maxContextChunks = 5,
            scoreThreshold = 0.7f,
            maxTokens = 512,
            temperature = 0.7f,
            language
        };

        var response = await _httpClient.PostAsJsonAsync("/api/rag/ask", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RagResponse>();
    }

    public async Task<SearchResponse> SearchAsync(string query, int limit = 10)
    {
        var request = new { query, limit, scoreThreshold = 0.7f };

        var response = await _httpClient.PostAsJsonAsync("/api/search", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SearchResponse>();
    }
}

// Usage in your ASP.NET Core controller:
[HttpGet("ai-search")]
public async Task<IActionResult> AiSearch(string query)
{
    var client = new BlogLlmClient();
    var results = await client.SearchAsync(query);
    return View(results);
}
```

### JavaScript Example (Fetch API)

```javascript
class BlogLlmClient {
    constructor(baseUrl = 'http://localhost:5100') {
        this.baseUrl = baseUrl;
    }

    async ask(question, options = {}) {
        const response = await fetch(`${this.baseUrl}/api/rag/ask`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                question,
                maxContextChunks: options.maxContextChunks || 5,
                scoreThreshold: options.scoreThreshold || 0.7,
                maxTokens: options.maxTokens || 512,
                temperature: options.temperature || 0.7,
                language: options.language
            })
        });

        return await response.json();
    }

    async search(query, limit = 10) {
        const response = await fetch(`${this.baseUrl}/api/search`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ query, limit, scoreThreshold: 0.7 })
        });

        return await response.json();
    }
}

// Usage:
const client = new BlogLlmClient();

document.getElementById('askBtn').addEventListener('click', async () => {
    const question = document.getElementById('question').value;
    const result = await client.ask(question);

    document.getElementById('answer').textContent = result.answer;
    document.getElementById('context').innerHTML = result.context
        .map(c => `<div class="context-item">
            <strong>${c.documentTitle}</strong> (${c.score.toFixed(2)})<br>
            ${c.text}
        </div>`)
        .join('');
});
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Mostlylucid.BlogLLM.Api": "Information"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5000",
      "https://www.mostlylucid.net"
    ]
  },
  "BlogRag": {
    "EmbeddingModel": {
      "ModelPath": "./models/bge-small-en-v1.5-onnx/model.onnx",
      "TokenizerPath": "./models/bge-small-en-v1.5-onnx/tokenizer.json",
      "Dimensions": 384,
      "UseGpu": false
    },
    "LlmModel": {
      "ModelPath": "./models/mistral-7b-instruct-v0.2.Q4_K_M.gguf",
      "ContextSize": 4096,
      "GpuLayers": 20
    },
    "VectorStore": {
      "Host": "localhost",
      "Port": 6334,
      "CollectionName": "blog_knowledge_base",
      "ApiKey": ""
    }
  }
}
```

### Environment Variables

For Docker deployment:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
BlogRag__VectorStore__Host=qdrant
BlogRag__VectorStore__Port=6334
BlogRag__VectorStore__ApiKey=your_api_key
BlogRag__LlmModel__GpuLayers=20
```

## Performance Tuning

### GPU Acceleration

For NVIDIA GPUs, adjust `GpuLayers` in configuration:
- **0**: CPU only (slowest, ~5-10 tokens/sec)
- **20-30**: Hybrid CPU/GPU (good balance)
- **33+**: Fully offloaded to GPU (fastest, ~50-100 tokens/sec on RTX 4090)

### Model Selection

**Smaller/Faster models:**
- Phi-3 Mini (3.8B) - Excellent for short answers
- Mistral 7B Q4 - Good balance of speed and quality

**Larger/Better models:**
- Mixtral 8x7B Q4 - Best quality (requires 32GB+ VRAM or CPU RAM)
- Llama 3 70B Q4 - Enterprise-grade (requires multi-GPU or CPU)

### Context Size

Adjust `ContextSize` based on your needs:
- **2048**: Fast, sufficient for most questions
- **4096**: Balanced (default)
- **8192+**: Handles very long context, slower

## Troubleshooting

### "Model not found"

Ensure models are downloaded to the correct path:
```bash
ls -lh ./models/
# Should show .onnx file and .gguf file
```

### "Cannot connect to Qdrant"

Check Qdrant is running:
```bash
docker ps | grep qdrant
# Or
curl http://localhost:6333/health
```

### "Out of memory"

**For GPU OOM:**
- Reduce `GpuLayers` in configuration
- Use smaller model (Q4 instead of Q5/Q6)
- Reduce `ContextSize`

**For CPU OOM:**
- Reduce `ContextSize`
- Use smaller model
- Increase system swap space

### Slow inference

**For GPU:**
- Increase `GpuLayers` (more layers on GPU)
- Check CUDA is properly installed: `nvidia-smi`
- Use quantized models (Q4_K_M is fastest)

**For CPU:**
- This is expected - CPU inference is 10-50x slower
- Consider cloud GPU instances (AWS, Azure, GCP)
- Use smaller models (Phi-3, Mistral 7B)

## Architecture

```
┌──────────────┐
│  Blog App    │  → HTTP requests
│  (ASP.NET)   │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────────────┐
│  BlogLLM API (ASP.NET Core)          │
│                                      │
│  ┌────────────────┐                 │
│  │ RAG Service    │                 │
│  └───┬────────────┘                 │
│      │                               │
│  ┌───▼──────────┐  ┌──────────────┐ │
│  │ Embedding    │  │ LLM Service  │ │
│  │ Service      │  │ (LlamaSharp) │ │
│  └───┬──────────┘  └──────┬───────┘ │
│      │                     │         │
└──────┼─────────────────────┼─────────┘
       │                     │
       ▼                     ▼
┌─────────────┐      ┌──────────────┐
│  Qdrant     │      │  GPU/CPU     │
│  Vector DB  │      │  (CUDA/AVX2) │
└─────────────┘      └──────────────┘
```

## Related Projects

- [mostlylucid.blogllm](../mostlylucid.blogllm) - CLI tool for building the knowledge base
- [Building a Lawyer GPT Blog Series](/blog/building-a-lawyer-gpt-for-your-blog-part1) - Full tutorial

## License

MIT

## Support

For issues or questions, please create an issue on GitHub.
