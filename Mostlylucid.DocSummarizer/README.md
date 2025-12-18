# DocSummarizer

> **Turn documents or URLs into evidence-grounded summaries or structured JSON — usable by humans or AI agents — without sending anything to the cloud.**

Every claim is traceable. Every fact cites its source. Runs entirely on your machine.

---

## Quick Start (30 seconds)

```bash
# 1. Install Ollama from https://ollama.ai, then:
ollama pull llama3.2:3b && ollama serve

# 2. Run (no other setup needed!)
docsummarizer -f document.pdf
```

That's it. ONNX embeddings auto-download. No Docker for Markdown files.

### Offline Mode (No Ollama Required)

```bash
# Pure extractive summarization - works completely offline
docsummarizer -f document.pdf -m Bert
```

Bert mode uses local ONNX models only. No LLM, no network (after first model download). ~3-5 seconds per document.

---

## Three Ways to Use DocSummarizer

### For Humans: Readable Summaries

```bash
# Auto-mode: smart selection (default)
docsummarizer -f report.pdf

# Fast extractive summary (no LLM, ~3-5s)
docsummarizer -f document.pdf -m Bert

# Production-grade with perfect citations
docsummarizer -f report.pdf -m BertRag

# Quick bullets for scanning
docsummarizer -f manual.pdf -t bullets

# Executive summary for leadership
docsummarizer -f quarterly.pdf -t executive -m BertRag

# Book report with themes
docsummarizer -f novel.pdf -t bookreport
```

### For AI Agents: Structured JSON

```bash
# Clean JSON output for LLM pipelines
docsummarizer tool -f contract.pdf

# Summarize a URL for your agent
docsummarizer tool -u "https://docs.example.com"

# Focus on specific topics
docsummarizer tool -f spec.pdf -q "security requirements"
```

Output includes `keyFacts` with confidence levels, `evidence` IDs, and `entities` — ready for downstream processing.

### For Developers: Batch & Integration

```bash
# Process entire directories with BertRag
docsummarizer -d ./documents -m BertRag -o Markdown --output-dir ./summaries

# Fast offline batch processing (no LLM)
docsummarizer -d ./documents -m Bert -o Json --output-dir ./summaries

# Compare models
docsummarizer benchmark -f doc.pdf -m "qwen2.5:1.5b,llama3.2:3b"

# Focused retrieval with production pipeline
docsummarizer -f large-manual.pdf -m BertRag --focus "security requirements"
```

---

## Why This Exists

Most summarizers give you text. This gives you *evidence*.

- Every claim includes `[chunk-N]` citations back to source
- Confidence levels (high/medium/low) based on supporting evidence
- Structured JSON output for agent integration, CI pipelines, or MCP servers
- Quality metrics catch hallucinations before they escape

If you need to *trust* a summary — or feed it to another system — that's the difference.

## Features

| Category | What You Get |
|----------|--------------|
| **Grounded Output** | Citations, confidence levels, evidence IDs |
| **Multiple Modes** | MapReduce (parallel), RAG (focused), Iterative |
| **Tool Mode** | Clean JSON for LLM agents, MCP, CI checks |
| **11 Templates** | brief, bullets, executive, technical, academic, bookreport, meeting... |
| **Web Fetching** | Security-hardened (SSRF protection, HTML sanitization) |
| **Playwright Mode** | Headless browser for JavaScript-rendered pages |
| **ONNX Embeddings** | Zero-config local embeddings, no Ollama embedding model needed |
| **Large Docs** | Hierarchical reduction handles 500+ pages |
| **Quality Analysis** | Hallucination detection, entity extraction |
| **Local Only** | Nothing leaves your machine |

## Quick Start

### Prerequisites

**For Bert mode (offline, no LLM):** Nothing required! Just run:
```bash
docsummarizer -f document.md -m Bert
```
ONNX models auto-download on first use (~23MB).

---

**For LLM-powered modes (Auto, BertRag, BertHybrid, MapReduce):** Ollama

```bash
# Install from https://ollama.ai
ollama pull llama3.2:3b        # Default model - good balance of speed/quality
ollama serve
```

> **Note**: Embedding models are handled automatically via ONNX. No need to pull `mxbai-embed-large` or any other embedding model unless you specifically want to use Ollama embeddings with `--embedding-backend Ollama`.

> **Speed tip**: For faster LLM summaries (~3s vs ~15s), use `--model qwen2.5:1.5b`

---

**For PDF/DOCX files:** Docling (optional - Markdown works without it)

```bash
docker run -d -p 5001:5001 quay.io/docling-project/docling-serve
```

**For legacy RAG mode:** Qdrant (optional - BertRag doesn't need it)

```bash
docker run -d -p 6333:6333 qdrant/qdrant
```

### Verify Setup

```bash
docsummarizer check --verbose
```

## Embedding Options

DocSummarizer supports two embedding backends for RAG mode:

### ONNX Embeddings (Default)

Zero-config local embeddings using ONNX Runtime. Models auto-download on first use.

| Model | Size | Dims | Max Seq | Best For |
|-------|------|------|---------|----------|
| `AllMiniLmL6V2` | ~23MB | 384 | 256 | **Default** - fast, general-purpose |
| `BgeSmallEnV15` | ~34MB | 384 | 512 | Best quality for size |
| `GteSmall` | ~34MB | 384 | 512 | Good all-around |
| `MultiQaMiniLm` | ~23MB | 384 | 512 | QA-optimized |
| `ParaphraseMiniLmL3` | ~17MB | 384 | 128 | Smallest/fastest |

```bash
# Use default ONNX embedding (AllMiniLmL6V2)
docsummarizer -f doc.pdf -m Rag

# Use a different ONNX model
docsummarizer -f doc.pdf -m Rag --embedding-model BgeSmallEnV15
```

### Ollama Embeddings (Optional)

If you prefer Ollama embeddings:

```bash
# Pull an embedding model
ollama pull mxbai-embed-large

# Use Ollama for embeddings
docsummarizer -f doc.pdf -m Rag --embedding-backend Ollama
```

## Usage

### Basic Summarization

```bash
# Summarize README.md in current directory (default)
docsummarizer

# Summarize a specific file
docsummarizer -f document.pdf

# Use RAG mode with focus query
docsummarizer -f contract.pdf -m Rag --focus "payment terms"

# Verbose progress
docsummarizer -f document.pdf -v
```

### Web URL Fetching (Security-Hardened)

```bash
# Summarize a web page (simple HTTP fetch)
docsummarizer --url "https://example.com/article.html" --web-enabled

# Summarize a JavaScript-rendered page (Playwright)
docsummarizer --url "https://react-app.com" --web-enabled --web-mode Playwright

# Summarize a remote PDF
docsummarizer --url "https://example.com/doc.pdf" --web-enabled
```

**Security features:**
- SSRF protection (blocks private IPs, cloud metadata endpoints)
- DNS rebinding protection (re-validates after redirects)
- Content-type gating (only allows safe document types)
- Decompression bomb protection
- HTML sanitization (removes scripts, event handlers, dangerous URLs)
- Image guardrails (size limits, count limits, hash deduplication)

**Web Fetch Modes:**

| Mode | Description | Use Case |
|------|-------------|----------|
| `Simple` | Fast HTTP client (default) | Static HTML pages, PDFs |
| `Playwright` | Headless Chromium browser | SPAs, React/Vue/Angular apps, JS-rendered content |

When using Playwright mode for the first time, Chromium will be automatically downloaded (~150MB).

### LLM Tool Mode

For AI agent integration, use the `tool` command to get structured JSON output:

```bash
# Summarize a URL and return JSON
docsummarizer tool --url "https://example.com/docs.html"

# Summarize a file with focus query
docsummarizer tool -f document.pdf -q "security requirements"

# Pipe to other tools
docsummarizer tool -f doc.pdf | jq '.summary.keyFacts'
```

**Tool output structure:**

```json
{
  "success": true,
  "source": "https://example.com/docs.html",
  "contentType": "text/html",
  "summary": {
    "executive": "Brief summary of the document.",
    "keyFacts": [
      {
        "claim": "The system supports 10,000 TPS.",
        "confidence": "high",
        "evidence": ["chunk-3", "chunk-7"],
        "type": "fact"
      }
    ],
    "topics": [
      {
        "name": "Architecture",
        "summary": "The system uses microservices...",
        "evidence": ["chunk-1", "chunk-2"]
      }
    ],
    "entities": {
      "people": ["John Smith"],
      "organizations": ["Acme Corp"],
      "concepts": ["OAuth 2.0", "REST API"]
    },
    "openQuestions": ["What is the disaster recovery plan?"]
  },
  "metadata": {
    "processingSeconds": 12.5,
    "chunksProcessed": 15,
    "model": "qwen2.5:1.5b",
    "mode": "MapReduce",
    "coverageScore": 0.95,
    "citationRate": 1.2,
    "fetchedAt": "2025-01-15T10:30:00Z"
  }
}
```

**Key design principles:**
- Every claim includes evidence IDs for traceability
- Confidence levels (high/medium/low) based on supporting evidence
- Clean executive summary (no citation markers) for display
- Metadata for debugging and quality assessment

### Structured Mode

Extract machine-readable JSON for programmatic processing:

```bash
docsummarizer -f document.pdf --structured -o Json
```

Extracts: entities, functions, key flows, facts with confidence, uncertainties, quotable passages.

### Batch Processing

```bash
# Process all files in a directory
docsummarizer -d ./documents -v

# Only PDFs, recursively
docsummarizer -d ./documents -e .pdf --recursive

# Output to Markdown files
docsummarizer -d ./documents -o Markdown --output-dir ./summaries
```

### Templates

```bash
# Use a template
docsummarizer -f doc.pdf --template executive
docsummarizer -f doc.pdf -t bullets

# Specify custom word count with template:wordcount syntax
docsummarizer -f doc.pdf -t bookreport:500
docsummarizer -f doc.pdf -t executive:100

# Or use --words to override any template's default
docsummarizer -f doc.pdf -t detailed --words 300
```

| Template | Words | Best For |
|----------|-------|----------|
| `default` | ~500 | General purpose |
| `brief` | ~50 | Quick scanning |
| `oneliner` | ~25 | Single sentence |
| `bullets` | auto | Key takeaways |
| `executive` | ~150 | C-suite reports |
| `detailed` | ~1000 | Comprehensive analysis |
| `technical` | ~300 | Tech documentation |
| `academic` | ~250 | Research papers |
| `citations` | auto | Key quotes with sources |
| `bookreport` | ~800 | Book report style |
| `meeting` | ~200 | Meeting notes with actions |

### Benchmarking

Compare models on the same document using the `benchmark` subcommand:

```bash
docsummarizer benchmark -f doc.pdf -m "qwen2.5:1.5b,llama3.2:3b,ministral-3:3b"
```

The benchmark parses the document once, then tests each model on the same chunks for fair comparison. Shows timing, word count, and words/second.

### Query Mode

Ask questions about a document instead of summarizing:

```bash
docsummarizer -f manual.pdf --query "How do I install the software?"
docsummarizer -f contract.pdf -q "What are the payment terms?"
```

For small documents, sends full text to LLM. For larger documents, uses semantic search to find relevant chunks first.

## Command Reference

### Main Command

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--file` | `-f` | Document path | - |
| `--directory` | `-d` | Batch directory | - |
| `--url` | `-u` | Web URL to fetch | - |
| `--web-enabled` | | Enable web fetching | `false` |
| `--web-mode` | | Simple or Playwright | `Simple` |
| `--mode` | `-m` | Auto, BertRag, Bert, BertHybrid, MapReduce, Rag, Iterative | `Auto` |
| `--focus` | | Focus query for RAG | - |
| `--query` | `-q` | Query mode | - |
| `--model` | | Ollama model | `llama3.2:3b` |
| `--embedding-backend` | | Onnx or Ollama | `Onnx` |
| `--embedding-model` | | ONNX model name | `AllMiniLmL6V2` |
| `--verbose` | `-v` | Show progress | `false` |
| `--template` | `-t` | Summary template | `default` |
| `--output-format` | `-o` | Console, Text, Markdown, Json | `Console` |


### Tool Command (LLM Integration)

```bash
docsummarizer tool [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--url` | `-u` | URL to fetch and summarize |
| `--file` | `-f` | File to summarize |
| `--query` | `-q` | Optional focus query |
| `--mode` | `-m` | Summarization mode |
| `--model` | | Ollama model |
| `--config` | `-c` | Configuration file |

### Other Commands

```bash
docsummarizer check [--verbose]                    # Verify dependencies
docsummarizer config [-o file]                     # Generate config file
docsummarizer templates                            # List available templates
docsummarizer benchmark -f file -m "model1,model2" # Compare models
```

## Summarization Modes

DocSummarizer includes a production-grade BERT-based pipeline alongside the original modes.

### Auto (Default - Smart Selection)

Let the tool pick the best mode based on document size, query presence, and LLM availability.

```bash
docsummarizer -f document.pdf
```

The Auto mode intelligently selects:
- **BertRag** for large documents or when embeddings are available
- **MapReduce** when offline or for comprehensive coverage
- **Iterative** for small documents that fit in context

### BertRag (Production Pipeline) ⭐ Recommended

The production-grade pipeline combining BERT extraction with LLM synthesis:

1. **Extract**: Parse document into segments with embeddings and salience scores
2. **Retrieve**: Dual-score ranking (semantic similarity + content salience)  
3. **Synthesize**: LLM generates fluent summary from top segments

```bash
docsummarizer -f document.pdf -m BertRag
docsummarizer -f large-manual.pdf -m BertRag --focus "security requirements"
```

**Properties:**
- Perfect citation grounding (every claim traceable to source)
- Deterministic extraction (reproducible, debuggable)
- LLM only at synthesis (cost-optimal: cheap CPU work first)
- Scales to any document size
- Best quality/traceability balance

### Bert (Fast, No LLM Required) ⚡

Pure extractive summarization using local ONNX models. **No Ollama or any LLM needed.**

```bash
# Basic usage - works completely offline
docsummarizer -f document.pdf -m Bert

# Batch processing without LLM
docsummarizer -d ./documents -m Bert -o Json --output-dir ./summaries

# With templates (still no LLM)
docsummarizer -f report.pdf -m Bert -t bullets
```

**How it works:**
1. Parses document into sentences using ONNX tokenizer
2. Generates sentence embeddings locally (ONNX Runtime)
3. Ranks sentences using MMR (Maximal Marginal Relevance) for diversity
4. Extracts top sentences based on relevance + position weighting
5. Groups by topic headings for structured output

**Properties:**
- ⚡ **~3-5 seconds** for most documents
- 🔒 **100% offline** - no network calls after first model download
- 🎯 **Deterministic** - same input = same output every time
- 💾 **~23MB model** - auto-downloads on first use from HuggingFace
- 📊 **No hallucination** - only extracts actual sentences from the document

**Use when:**
- No Ollama/LLM installed or available
- Air-gapped or offline environments
- Need instant summaries for triage
- CI/CD pipelines where speed matters
- Want reproducible, auditable results
- Processing large batches where LLM cost/time is prohibitive

### BertHybrid (Best of Both)

BERT extracts key sentences, LLM polishes into fluent prose.

```bash
docsummarizer -f document.pdf -m BertHybrid
```

Combines extraction grounding (no hallucination) with LLM fluency.

### MapReduce (Legacy - Comprehensive)

Parallel chunk summarization with hierarchical reduction. Handles any document size.

```bash
docsummarizer -f huge-document.pdf -m MapReduce
```

**When to use:** Full coverage needed, every section matters (contracts, legal docs).

### Rag (Legacy - Focused Queries)

Vector search + focused summarization. Use when you need specific information.

```bash
docsummarizer -f manual.pdf -m Rag --focus "installation steps"
```

### Iterative (Small Documents)

Sequential chunk processing. Best for documents <10 pages.

```bash
docsummarizer -f short-report.pdf -m Iterative
```

### Mode Selection Guide

| Scenario | Recommended Mode | Why |
|----------|-----------------|-----|
| **Just getting started** | `Auto` | Smart selection, works everywhere |
| **Production system** | `BertRag` | Best quality + traceability |
| **Need speed, offline** | `Bert` | No LLM needed, ~3-5s |
| **Large doc, full coverage** | `BertRag` or `MapReduce` | BertRag for quality, MapReduce for 100% coverage |
| **Specific question** | `BertRag --focus "query"` | Focused retrieval + synthesis |
| **Legal/compliance** | `MapReduce` | Every clause matters |
| **Quick summary** | `Bert` | Instant, deterministic |

## Configuration

Generate default config:

```bash
docsummarizer config --output docsummarizer.json
```

Auto-discovery order:
1. `--config` option
2. `docsummarizer.json` in current directory
3. `.docsummarizer.json`
4. `~/.docsummarizer.json`

### Complete Configuration Reference

```json
{
  "embeddingBackend": "Onnx",
  "onnx": {
    "embeddingModel": "AllMiniLmL6V2",
    "useQuantized": true,
    "maxEmbeddingSequenceLength": 256
  },
  "ollama": {
    "model": "llama3.2:3b",
    "embedModel": "mxbai-embed-large",
    "classifierModel": "tinyllama",
    "baseUrl": "http://localhost:11434",
    "temperature": 0.3,
    "timeoutSeconds": 1200
  },
  "bert": {
    "lambda": 0.7,
    "extractionRatio": 0.15,
    "minSentences": 3,
    "maxSentences": 30,
    "usePositionWeighting": true
  },
  "docling": {
    "baseUrl": "http://localhost:5001",
    "timeoutSeconds": 1200,
    "pdfBackend": "pypdfium2",
    "pagesPerChunk": 10,
    "maxConcurrentChunks": 4,
    "enableSplitProcessing": true
  },
  "qdrant": {
    "host": "localhost",
    "port": 6333,
    "collectionName": "documents",
    "vectorSize": 384,
    "deleteCollectionAfterSummarization": true
  },
  "processing": {
    "maxHeadingLevel": 2,
    "targetChunkTokens": 1500,
    "minChunkTokens": 200,
    "maxLlmParallelism": 2,
    "memory": {
      "enableDiskStorage": true,
      "diskStorageThreshold": 100,
      "streamingThresholdBytes": 5242880,
      "embeddingBatchSize": 10,
      "gcIntervalChunks": 50,
      "maxMemoryMB": 0
    }
  },
  "output": {
    "format": "Console",
    "verbose": false,
    "includeTrace": false,
    "includeTopics": true,
    "includeOpenQuestions": false
  },
  "webFetch": {
    "enabled": false,
    "mode": "Simple",
    "timeoutSeconds": 30,
    "userAgent": "Mozilla/5.0 DocSummarizer/3.0"
  },
  "batch": {
    "fileExtensions": [".pdf", ".docx", ".md", ".txt", ".html"],
    "recursive": false,
    "maxConcurrentFiles": 4,
    "continueOnError": true
  },
  "embedding": {
    "requestsPerSecond": 2.0,
    "maxRetries": 5,
    "initialRetryDelayMs": 1000,
    "maxRetryDelayMs": 30000,
    "delayBetweenRequestsMs": 100,
    "enableCircuitBreaker": true,
    "circuitBreakerThreshold": 3,
    "circuitBreakerDurationSeconds": 30
  },
  "bertRag": {
    "vectorStore": 0,
    "collectionName": "docsummarizer",
    "persistVectors": true,
    "reuseExistingEmbeddings": true
  }
}
```

### Configuration Options Explained

#### Embedding Settings

| Option | Default | Description |
|--------|---------|-------------|
| `embeddingBackend` | `Onnx` | Embedding backend: `Onnx` (local, zero-config) or `Ollama` |
| `onnx.embeddingModel` | `AllMiniLmL6V2` | ONNX model to use |
| `onnx.useQuantized` | `true` | Use quantized models (smaller, faster) |

#### Ollama Settings

| Option | Default | Description |
|--------|---------|-------------|
| `model` | `llama3.2:3b` | LLM model for summarization |
| `embedModel` | `mxbai-embed-large` | Embedding model (only used if `embeddingBackend` is `Ollama`) |
| `baseUrl` | `http://localhost:11434` | Ollama API endpoint |
| `temperature` | `0.3` | Lower = more deterministic |
| `timeoutSeconds` | `1200` | Timeout for LLM requests |

#### Processing Settings

| Option | Default | Description |
|--------|---------|-------------|
| `maxLlmParallelism` | `2` | Concurrent LLM requests (Ollama queues internally) |
| `maxHeadingLevel` | `2` | Split on H1/H2. Set to 3 for finer chunks |
| `targetChunkTokens` | `0` (auto) | Target chunk size. 0 = ~25% of context window |
| `minChunkTokens` | `0` (auto) | Minimum before merging. 0 = 1/8 of target |

#### Docling Settings

| Option | Default | Description |
|--------|---------|-------------|
| `baseUrl` | `http://localhost:5001` | Docling service URL |
| `pdfBackend` | `pypdfium2` | PDF parsing backend |
| `pagesPerChunk` | `10` | Pages per processing chunk for large PDFs |
| `enableSplitProcessing` | `true` | Split large PDFs for parallel processing |

#### Web Fetch Settings

| Option | Default | Description |
|--------|---------|-------------|
| `enabled` | `false` | Must be true for `--url` to work |
| `mode` | `Simple` | `Simple` (HTTP) or `Playwright` (headless browser) |
| `timeoutSeconds` | `30` | HTTP/browser request timeout |
| `userAgent` | `Mozilla/5.0...` | User agent for web requests |

#### BERT Extraction Settings

| Option | Default | Description |
|--------|---------|-------------|
| `lambda` | `0.7` | Balance between relevance (1.0) and diversity (0.0) |
| `extractionRatio` | `0.15` | Target ~15% of sentences for extraction |
| `minSentences` | `3` | Minimum sentences to extract |
| `maxSentences` | `30` | Maximum sentences to extract |
| `usePositionWeighting` | `true` | Weight sentences by position (intro/conclusion bias) |

#### Memory Management Settings

| Option | Default | Description |
|--------|---------|-------------|
| `enableDiskStorage` | `true` | Spill embeddings to disk for large documents |
| `diskStorageThreshold` | `100` | Chunks before spilling to disk |
| `streamingThresholdBytes` | `5242880` | 5MB threshold for streaming mode |
| `embeddingBatchSize` | `10` | Batch size for embedding generation |
| `gcIntervalChunks` | `50` | Force GC every N chunks |
| `maxMemoryMB` | `0` | Memory limit (0 = unlimited) |

#### Embedding Resilience Settings

| Option | Default | Description |
|--------|---------|-------------|
| `requestsPerSecond` | `2.0` | Rate limit for embedding requests |
| `maxRetries` | `5` | Maximum retry attempts |
| `initialRetryDelayMs` | `1000` | Initial retry delay |
| `maxRetryDelayMs` | `30000` | Maximum retry delay |
| `enableCircuitBreaker` | `true` | Enable circuit breaker for failures |
| `circuitBreakerThreshold` | `3` | Failures before circuit opens |
| `circuitBreakerDurationSeconds` | `30` | Duration circuit stays open |

#### BertRag Persistence Settings (Learning Summarizer)

| Option | Default | Description |
|--------|---------|-------------|
| `vectorStore` | `0` (InMemory) | `0` = InMemory (no persistence), `1` = Qdrant (persistent) |
| `collectionName` | `docsummarizer` | Name of vector collection for segments |
| `persistVectors` | `true` | Keep vectors between runs (Qdrant only) |
| `reuseExistingEmbeddings` | `true` | Skip re-embedding unchanged segments |

When using Qdrant persistence (`vectorStore: 1`):
- First run embeds all segments (~15-20s)
- Subsequent runs load from cache (~0.5s for segment lookup)
- Changed content is detected and re-embedded
- Requires Qdrant server on port 6334 (gRPC)

See [LEARNING_SUMMARIZER.md](LEARNING_SUMMARIZER.md) for detailed architecture documentation.

## Model Recommendations

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `qwen2.5:1.5b` | 986MB | ~3s | Good | Speed-optimized |
| `llama3.2:3b` | 2GB | ~15s | Very Good | **Default** - good balance |
| `ministral-3:3b` | 2.9GB | ~20s | Very Good | Quality-focused |
| `llama3.1:8b` | 4.7GB | ~45s | Excellent | Critical documents |

## Installation

### Pre-built Binaries

Download from [GitHub Releases](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer):

| Platform | Download |
|----------|----------|
| Windows x64 | `docsummarizer-win-x64.zip` |
| Linux x64 | `docsummarizer-linux-x64.tar.gz` |
| macOS ARM64 | `docsummarizer-osx-arm64.tar.gz` |

### Build from Source

```bash
cd Mostlylucid.DocSummarizer
dotnet build
dotnet run -- --help
```

### Publish (Production)

```bash
dotnet publish -c Release -r win-x64 --self-contained
# Output: bin/Release/net10.0/win-x64/publish/
```

## Architecture

### Components

| Component | Purpose |
|-----------|---------|
| `DocumentSummarizer` | Main orchestrator |
| `WebFetcher` | Security-hardened URL fetching (Simple + Playwright modes) |
| `MapReduceSummarizer` | Parallel chunk processing with hierarchical reduction |
| `RagSummarizer` | Vector-based retrieval and synthesis |
| `OnnxEmbeddingService` | Local ONNX-based embeddings (default) |
| `OllamaEmbeddingService` | Ollama-based embeddings (optional) |
| `OllamaService` | LLM client with Polly resilience |
| `QdrantHttpClient` | Vector search client |
| `DoclingClient` | Document conversion |
| `QualityAnalyzer` | Hallucination detection, entity extraction |

### Embedding Architecture

```
Text -> IEmbeddingService -> float[]
              |
              +-- OnnxEmbeddingService (default, zero-config)
              |       |
              |       +-- Auto-downloads model from HuggingFace
              |       +-- BertTokenizer for text preprocessing
              |       +-- ONNX Runtime for inference
              |
              +-- OllamaEmbeddingService (optional)
                      |
                      +-- Requires Ollama server
                      +-- Polly resilience for reliability
```

### LLM Resilience

The `OllamaService` uses Polly v8 for robust LLM operations:

| Feature | Implementation |
|---------|----------------|
| **Retry Policy** | Exponential backoff with decorrelated jitter |
| **Circuit Breaker** | Opens after repeated failures, auto-recovers |
| **Long Text Handling** | Splits into 1000-char chunks, averages vectors |
| **Connection Recovery** | Fresh connections per request (Windows wsarecv fix) |
| **Rate Limiting** | Jittered delays between requests |

### Web Fetch Security

The `WebFetcher` implements comprehensive security controls:

| Protection | Implementation |
|------------|----------------|
| **SSRF** | Blocks private IPs (10.x, 172.16.x, 192.168.x, localhost, link-local) |
| **Cloud Metadata** | Blocks 169.254.169.254 and other metadata endpoints |
| **DNS Rebinding** | Re-validates IPs after redirects |
| **Redirects** | Max 5, blocks HTTPS->HTTP downgrade |
| **Content-Type** | Allowlist: HTML, PDF, Markdown, images, Office docs |
| **Size Limits** | 10MB response, 5MB HTML |
| **Decompression** | Max 20x expansion ratio |
| **HTML** | Removes scripts, event handlers, dangerous URLs |
| **Images** | Max 50 per doc, 1920px, 4K pixel area, hash dedup |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Could not connect to Ollama" | Run `ollama serve` |
| "Docling unavailable" | Only needed for PDF/DOCX, not Markdown |
| "Qdrant connection failed" | Only needed for RAG mode |
| "Security blocked: private IP" | URL resolved to internal address (SSRF protection) |
| "Circuit breaker is open" | Ollama is overloaded/crashed - wait 30s or restart Ollama |
| "wsarecv" connection errors | Windows-specific issue - the tool auto-retries with backoff |
| Repetitive summaries | Use `--model llama3.2:3b` for better quality |
| Missing citations | Larger models follow citation instructions better |
| Playwright not working | First run downloads Chromium (~150MB) - ensure internet access |

## Credits

- [Ollama](https://ollama.ai/) - Local LLM inference
- [ONNX Runtime](https://onnxruntime.ai/) - Local ML inference for embeddings
- [Playwright](https://playwright.dev/) - Headless browser automation
- [Polly](https://github.com/App-vNext/Polly) - .NET resilience and transient-fault-handling
- [Docling](https://github.com/docling-project/docling) - Document conversion
- [Qdrant](https://qdrant.tech/) - Vector database
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI
- [AngleSharp](https://anglesharp.github.io/) - HTML parsing
- [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/) - Image processing
