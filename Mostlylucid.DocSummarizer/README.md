# DocSummarizer

> **Turn documents or URLs into evidence-grounded summaries or structured JSON — usable by humans or AI agents — without sending anything to the cloud.**

Every claim is traceable. Every fact cites its source. Runs entirely on your machine.

```bash
# Human-readable summary
docsummarizer -f contract.pdf

# JSON for agents/pipelines  
docsummarizer tool -u "https://docs.example.com"
```

**~18MB native binary. No Docker required for Markdown. Just works.**

---

## Why This Exists

Most summarizers give you text. This gives you *evidence*.

- Every claim includes `[chunk-N]` citations back to source
- Confidence levels (high/medium/low) based on supporting evidence
- Structured JSON output for agent integration, CI pipelines, or MCP servers
- Quality metrics catch hallucinations before they escape

If you need to *trust* a summary — or feed it to another system — that's the difference.

## Quick Overview

| Use Case | Command |
|----------|---------|
| Summarize a PDF | `docsummarizer -f report.pdf` |
| Summarize a URL | `docsummarizer -u "https://..." --web-enabled` |
| JSON for agents | `docsummarizer tool -f doc.pdf` |
| Book report style | `docsummarizer -f novel.pdf -t bookreport` |
| Meeting notes | `docsummarizer -f transcript.md -t meeting` |
| Compare models | `docsummarizer -f doc.pdf --benchmark "qwen2.5:1.5b,llama3.2:3b"` |

## Features

| Category | What You Get |
|----------|--------------|
| **Grounded Output** | Citations, confidence levels, evidence IDs |
| **Multiple Modes** | MapReduce (parallel), RAG (focused), Iterative |
| **Tool Mode** | Clean JSON for LLM agents, MCP, CI checks |
| **11 Templates** | brief, bullets, executive, technical, academic, bookreport, meeting... |
| **Web Fetching** | Security-hardened (SSRF protection, HTML sanitization) |
| **Large Docs** | Hierarchical reduction handles 500+ pages |
| **Quality Analysis** | Hallucination detection, entity extraction |
| **Local Only** | Nothing leaves your machine |

## Quick Start

### Prerequisites

**Required: Ollama** (only requirement for Markdown files)

```bash
# Install from https://ollama.ai
ollama pull qwen2.5:1.5b       # Fast (~3s per doc)
ollama pull mxbai-embed-large  # For RAG mode only
ollama serve
```

**Optional: Docling** (for PDF/DOCX)

```bash
docker run -d -p 5001:5001 quay.io/docling-project/docling-serve
```

**Optional: Qdrant** (for RAG mode)

```bash
docker run -d -p 6333:6333 qdrant/qdrant
```

### Verify Setup

```bash
docsummarizer check --verbose
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
# Summarize a web page
docsummarizer --url "https://example.com/article.html" --web-enabled

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

**Limitation: JavaScript-rendered pages**

The AOT binary uses simple HTTP fetching which doesn't execute JavaScript. Pages that render content client-side (SPAs, React apps, etc.) will return empty or partial content.

For dynamic pages, you have two options:

1. **Use a pre-rendered URL** if the site offers one (e.g., `?_escaped_fragment_=` or server-side rendering)

2. **Build without AOT** to enable Playwright support:
   ```bash
   # Add Playwright package
   dotnet add package Microsoft.Playwright
   
   # Build without AOT (in csproj, set PublishAot=false)
   dotnet build -c Release
   
   # Install browsers
   pwsh bin/Release/net10.0/playwright.ps1 install chromium
   ```
   
   Then use `--web-mode Playwright` (not available in AOT builds).

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
| `default` | ~200 | General purpose |
| `brief` | ~50 | Quick scanning |
| `oneliner` | ~25 | Single sentence |
| `bullets` | auto | Key takeaways |
| `executive` | ~150 | C-suite reports |
| `detailed` | ~500 | Comprehensive analysis |
| `technical` | ~300 | Tech documentation |
| `academic` | ~250 | Research papers |
| `citations` | auto | Key quotes with sources |
| `bookreport` | ~400 | Book report style |
| `meeting` | ~200 | Meeting notes with actions |

### Benchmarking

Compare models on the same document:

```bash
docsummarizer -f doc.pdf --benchmark "qwen2.5:1.5b,llama3.2:3b,ministral-3:3b"
```

## Command Reference

### Main Command

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--file` | `-f` | Document path | - |
| `--directory` | `-d` | Batch directory | - |
| `--url` | `-u` | Web URL to fetch | - |
| `--web-enabled` | | Enable web fetching | `false` |
| `--mode` | `-m` | MapReduce, Rag, Iterative | `MapReduce` |
| `--focus` | | Focus query for RAG | - |
| `--query` | `-q` | Query mode | - |
| `--model` | | Ollama model | `llama3.2:3b` |
| `--verbose` | `-v` | Show progress | `false` |
| `--template` | `-t` | Summary template | `default` |
| `--output-format` | `-o` | Console, Text, Markdown, Json | `Console` |
| `--benchmark` | `-b` | Models to compare | - |

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
docsummarizer check [--verbose]    # Verify dependencies
docsummarizer config [-o file]     # Generate config file
docsummarizer templates            # List templates
```

## Summarization Modes

### MapReduce (Default)

Best for comprehensive summaries. Handles documents of any size via hierarchical reduction.

```
Document → Chunks → Parallel Summaries → Batch Reduction → Final Summary
```

### RAG (Focused Queries)

Best when you need specific information from a large document.

```bash
docsummarizer -f manual.pdf -m Rag --focus "installation steps"
```

### Mode Selection Guide

| Scenario | Mode |
|----------|------|
| Full document summary | MapReduce |
| Specific topic extraction | RAG |
| Legal/contracts (every clause matters) | MapReduce |
| Large manual, specific question | RAG |
| Narrative/fiction | MapReduce |

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
  "ollama": {
    "model": "llama3.2:3b",
    "embedModel": "mxbai-embed-large",
    "baseUrl": "http://localhost:11434",
    "temperature": 0.3,
    "timeoutSeconds": 1200
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
    "collectionName": "documents"
  },
  "processing": {
    "maxHeadingLevel": 2,
    "targetChunkTokens": 1500,
    "minChunkTokens": 200,
    "maxLlmParallelism": 2
  },
  "output": {
    "format": "Console",
    "verbose": false,
    "includeTrace": false
  },
  "webFetch": {
    "enabled": false,
    "timeoutSeconds": 30,
    "userAgent": "Mozilla/5.0 DocSummarizer/1.0"
  },
  "batch": {
    "fileExtensions": [".pdf", ".docx", ".md", ".txt", ".html"],
    "recursive": false,
    "continueOnError": true
  }
}
```

### Configuration Options Explained

#### Ollama Settings

| Option | Default | Description |
|--------|---------|-------------|
| `model` | `llama3.2:3b` | LLM model for summarization |
| `embedModel` | `mxbai-embed-large` | Embedding model for RAG mode |
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
| `timeoutSeconds` | `30` | HTTP request timeout |
| `userAgent` | `Mozilla/5.0...` | User agent for web requests |

## Model Recommendations

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `qwen2.5:1.5b` | 986MB | ~3s | Good | Speed-optimized (default) |
| `llama3.2:3b` | 2GB | ~15s | Very Good | Balance |
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

### Native AOT (Production)

```bash
dotnet publish -c Release -r win-x64 --self-contained
# Output: bin/Release/net10.0/win-x64/publish/docsummarizer.exe (~18MB)
```

## Architecture

### Components

| Component | Purpose |
|-----------|---------|
| `DocumentSummarizer` | Main orchestrator |
| `WebFetcher` | Security-hardened URL fetching |
| `MapReduceSummarizer` | Parallel chunk processing with hierarchical reduction |
| `RagSummarizer` | Vector-based retrieval and synthesis |
| `OllamaService` | AOT-compatible LLM client |
| `QdrantHttpClient` | AOT-compatible vector search client |
| `DoclingClient` | Document conversion |
| `QualityAnalyzer` | Hallucination detection, entity extraction |

### Web Fetch Security

The `WebFetcher` implements comprehensive security controls:

| Protection | Implementation |
|------------|----------------|
| **SSRF** | Blocks private IPs (10.x, 172.16.x, 192.168.x, localhost, link-local) |
| **Cloud Metadata** | Blocks 169.254.169.254 and other metadata endpoints |
| **DNS Rebinding** | Re-validates IPs after redirects |
| **Redirects** | Max 5, blocks HTTPS→HTTP downgrade |
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
| Repetitive summaries | Use `--model llama3.2:3b` for better quality |
| Missing citations | Larger models follow citation instructions better |

## Credits

- [Ollama](https://ollama.ai/) - Local LLM inference
- [Docling](https://github.com/docling-project/docling) - Document conversion
- [Qdrant](https://qdrant.tech/) - Vector database
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI
- [AngleSharp](https://anglesharp.github.io/) - HTML parsing
- [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/) - Image processing
