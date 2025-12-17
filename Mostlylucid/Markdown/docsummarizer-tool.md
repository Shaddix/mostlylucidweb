# docsummarizer - Local Document Intelligence Tool

<!--category-- AI, LLM, RAG, C#, Docling, Ollama, Qdrant, Tools -->
<datetime class="hidden">2025-12-21T11:00</datetime>

[![GitHub release](https://img.shields.io/github/v/release/scottgal/mostlylucidweb?filter=docsummarizer*&label=docsummarizer)](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Enabled-brightgreen)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
[![Version](https://img.shields.io/badge/version-3.0.0-blue)](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer)

> **Turn documents or URLs into evidence-grounded summaries — for humans or AI agents — without sending anything to the cloud.**

Every claim is traceable. Every fact cites its source. ~18MB binary, runs entirely on your machine.

```bash
# Human-readable summary
docsummarizer -f contract.pdf

# JSON for agents/pipelines
docsummarizer tool -u "https://docs.example.com"
```

For the architecture behind this tool, see [Building a Document Summarizer with RAG](/blog/building-a-document-summarizer-with-rag).

[TOC]

## Why This Exists

Most summarizers give you text. This gives you *evidence*.

- **Every claim includes `[chunk-N]` citations** back to source material
- **Confidence levels** (high/medium/low) based on supporting evidence  
- **Structured JSON output** for agent integration, CI pipelines, or MCP servers
- **Quality metrics** catch hallucinations before they escape

If you need to *trust* a summary — or feed it to another system — that matters.

## Features

- **Evidence-Grounded Output**: Citations, confidence levels, traceable claims
- **Multiple Modes**: MapReduce (parallel), RAG (topic-driven), Iterative
- **Tool Mode**: Clean JSON for LLM agents, MCP servers, CI checks
- **11 Templates**: brief, bullets, executive, technical, academic, bookreport, meeting...
- **Large Documents**: Hierarchical reduction handles 500+ pages
- **Web Fetching**: Security-hardened (SSRF protection, HTML sanitization)
- **Quality Analysis**: Hallucination detection, entity extraction
- **Resilient Embeddings**: Polly-based retry with jitter backoff + circuit breaker
- **Long Text Support**: Automatic chunking with vector averaging for texts >1000 chars
- **Native AOT**: ~18MB binary, instant startup
- **Local Only**: Nothing leaves your machine

## Using as an LLM Tool

The `tool` command is designed specifically for integration with AI agents, MCP servers, and other automated systems. It outputs structured JSON to stdout with evidence-grounded claims - perfect for building RAG pipelines or agent tools.

### Basic Tool Usage

```bash
# Summarize a URL and get JSON output
docsummarizer tool --url "https://example.com/docs.html"

# Summarize a local file
docsummarizer tool -f document.pdf

# With a focus query
docsummarizer tool -f contract.pdf -q "payment terms and conditions"

# Pipe to jq for processing
docsummarizer tool -f doc.pdf | jq '.summary.keyFacts'
```

### Tool Output Structure

The tool command returns structured JSON with evidence tracking:

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

### Tool Command Options

```bash
docsummarizer tool [options]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--url` | `-u` | URL to fetch and summarize |
| `--file` | `-f` | File to summarize |
| `--query` | `-q` | Optional focus query |
| `--mode` | `-m` | Summarization mode (MapReduce, Rag, Iterative) |
| `--model` | | Ollama model to use |
| `--config` | `-c` | Configuration file path |

### Key Design Principles

- **Evidence Grounding**: Every claim includes `evidence` IDs referencing source chunks
- **Confidence Levels**: Claims are rated `high`, `medium`, or `low` based on supporting evidence
- **Clean Output**: The `executive` summary has no citation markers for easy display
- **Metadata**: Processing stats help with debugging and quality assessment
- **Error Handling**: Failures return `success: false` with an `error` message

### Integration Examples

**Python script:**

```python
import subprocess
import json

result = subprocess.run(
    ["docsummarizer", "tool", "-u", "https://example.com/api-docs"],
    capture_output=True, text=True
)
data = json.loads(result.stdout)

if data["success"]:
    for fact in data["summary"]["keyFacts"]:
        if fact["confidence"] == "high":
            print(f"- {fact['claim']}")
```

**Shell pipeline:**

```bash
# Extract high-confidence facts only
docsummarizer tool -f doc.pdf | jq '[.summary.keyFacts[] | select(.confidence == "high")]'

# Get just the executive summary
docsummarizer tool -u "https://example.com" | jq -r '.summary.executive'
```

## Quick Start

### Download Pre-built Binaries

Pre-built native executables are available from [GitHub Releases](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer):

| Platform | Architecture | Download |
|----------|--------------|----------|
| Windows | x64 | `docsummarizer-win-x64.zip` |
| Windows | ARM64 | `docsummarizer-win-arm64.zip` |
| Linux | x64 | `docsummarizer-linux-x64.tar.gz` |
| Linux | ARM64 | `docsummarizer-linux-arm64.tar.gz` |
| macOS | x64 (Intel) | `docsummarizer-osx-x64.tar.gz` |
| macOS | ARM64 (Apple Silicon) | `docsummarizer-osx-arm64.tar.gz` |

```bash
# Download and extract (Linux/macOS)
curl -L -o docsummarizer.tar.gz https://github.com/scottgal/mostlylucidweb/releases/download/docsummarizer-v1.0.0/docsummarizer-linux-x64.tar.gz
tar -xzf docsummarizer.tar.gz
chmod +x docsummarizer

# Download and extract (Windows PowerShell)
Invoke-WebRequest -Uri "https://github.com/scottgal/mostlylucidweb/releases/download/docsummarizer-v1.0.0/docsummarizer-win-x64.zip" -OutFile "docsummarizer.zip"
Expand-Archive -Path "docsummarizer.zip" -DestinationPath "."
```

### Prerequisites

#### Required: Ollama

Ollama is the **only requirement** for summarizing Markdown files:

```bash
# Install Ollama from https://ollama.ai
ollama pull qwen2.5:1.5b       # Default model - fast (recommended for speed)
ollama pull mxbai-embed-large  # For RAG mode only
ollama serve
```

> **Tip**: The default `qwen2.5:1.5b` is optimized for speed (~3s per document). For higher quality summaries, use `--model llama3.2:3b` or `--model ministral-3:3b`.

#### Optional: Docling (PDF/DOCX only)

Only needed when summarizing PDF or DOCX files. **Markdown files are read directly - no Docling required.**

```bash
docker run -d -p 5001:5001 quay.io/docling-project/docling-serve
```

#### Optional: Qdrant (RAG mode only)

Only needed for `--mode Rag`. The default MapReduce mode doesn't require Qdrant.

```bash
docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### Verify Dependencies

```bash
docsummarizer check --verbose
```

Expected output:
```
Checking dependencies...

  Ollama: ✓ (http://localhost:11434)

  Available models:
    - qwen2.5:1.5b
    - mxbai-embed-large
    ... and more

  Default model info:
    Name: qwen2.5:1.5b
    Family: qwen2
    Parameters: 1.5B
    Context Window: 8,192 tokens

  Docling: ✗ (http://localhost:5001)   # Optional - only for PDF/DOCX
  Qdrant: ✗ (localhost:6334)           # Optional - only for RAG mode
```

> **Note**: Docling and Qdrant showing ✗ is fine for Markdown-only workflows.

## Usage

### Default Behavior

Running `docsummarizer` with no arguments will:
1. Look for `README.md` in the current directory
2. Summarize it using MapReduce mode
3. Print the summary to console
4. Auto-save to `readme.summary.md`

```bash
# Summarize README.md in current directory
docsummarizer

# Output:
# Summarizing: README.md
# Mode: MapReduce
# Model: qwen2.5:1.5b
# ...
# Saved: README_summary.md
```

### Basic Summarization

```bash
# Summarize a Markdown file (only needs Ollama)
docsummarizer -f document.md

# Summarize a PDF (needs Ollama + Docling)
docsummarizer -f document.pdf

# Summarize with verbose output (shows progress)
docsummarizer -f document.pdf -v

# Use RAG mode with focus query (needs Ollama + Qdrant)
docsummarizer -f document.pdf -m Rag --focus "security requirements"

# Use a higher quality model for important documents
docsummarizer -f document.pdf --model llama3.2:3b -v
```

### Query Mode

Instead of summarizing, ask questions about a document:

```bash
docsummarizer -f manual.pdf --query "How do I install the software?"
```

### Web URL Fetching

Summarize web pages directly without downloading:

```bash
# Summarize a web article
docsummarizer --url "https://example.com/article.html" --web-enabled

# Summarize a remote PDF
docsummarizer --url "https://example.com/document.pdf" --web-enabled

# With structured JSON extraction
docsummarizer --url "https://example.com/api-docs.html" --web-enabled --structured
```

**Supported content**: HTML (sanitized), PDF, Markdown, images (OCR), Office documents. Large images automatically resized.

**Security**: SSRF protection, DNS rebinding protection, content-type gating, decompression bomb protection, HTML sanitization.

> **Note: JavaScript-rendered pages** — The AOT binary uses simple HTTP fetching which doesn't execute JavaScript. SPAs and client-rendered pages will return empty or partial content. For dynamic pages, either use a pre-rendered URL or build without AOT to enable Playwright support (see README for details).

### Structured Mode

Extract machine-readable JSON instead of prose:

```bash
docsummarizer -f document.pdf --structured -o Json
```

Extracts: entities, functions, key flows, facts (with confidence levels), uncertainties, quotable passages.

### Summary Templates

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
| `bookreport` | ~400 | Book report style with themes |
| `meeting` | ~200 | Meeting notes with actions |

### Model Benchmarking

Compare models on the same document:

```bash
docsummarizer -f doc.pdf --benchmark "qwen2.5:1.5b,llama3.2:3b,ministral-3:3b"
```

### Batch Processing

Process entire directories of documents:

```bash
# Process all supported files in a directory
docsummarizer -d ./documents --mode MapReduce -v

# Process only PDFs recursively
docsummarizer -d ./documents -e .pdf --recursive -v

# Output to Markdown files
docsummarizer -d ./documents -o Markdown --output-dir ./summaries
```

### Command-Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--file` | `-f` | Path to document (DOCX, PDF, MD) | - |
| `--directory` | `-d` | Path to directory for batch processing | - |
| `--url` | `-u` | Web URL to fetch and summarize | - |
| `--web-enabled` | | Enable web fetching (required for --url) | `false` |
| `--mode` | `-m` | Summarization mode: MapReduce, Rag, Iterative | `MapReduce` |
| `--structured` | `-s` | Use structured JSON extraction mode | `false` |
| `--focus` | | Focus query for RAG mode | None |
| `--query` | `-q` | Query mode instead of summarization | None |
| `--model` | | Ollama model to use | `llama3.2:3b` |
| `--verbose` | `-v` | Show detailed progress with live UI | `false` |
| `--config` | `-c` | Path to configuration file | Auto-discover |
| `--output-format` | `-o` | Output format: Console, Text, Markdown, Json | `Console` |
| `--output-dir` | | Output directory for file outputs | Current dir |
| `--extensions` | `-e` | File extensions for batch mode | All Docling formats |
| `--recursive` | `-r` | Process directories recursively | `false` |
| `--template` | `-t` | Summary template (default, brief, bullets, executive, etc.) | `default` |
| `--words` | `-w` | Target word count (overrides template) | Template default |
| `--benchmark` | `-b` | Comma-separated models to compare | - |
| `--analyze` | `-a` | Run quality analysis on summary | `false` |

## Summarization Modes

### MapReduce (Recommended)

Best for comprehensive summaries with full document coverage.

```bash
docsummarizer -f document.pdf -m MapReduce -v
```

**How it works**:
1. Splits document into structural chunks (by headings)
2. Summarizes each chunk in parallel using LLM
3. Reduces summaries into executive summary with citations
4. Validates all citations reference real chunks

**Hierarchical Reduction for Long Documents**:

For very long documents where the combined chunk summaries exceed the model's context window, MapReduce automatically uses hierarchical reduction:

1. **Batching**: Groups summaries into batches that fit in context
2. **Intermediate reduction**: Reduces each batch to a condensed summary
3. **Final reduction**: Merges intermediate summaries into the final output
4. **Recursive**: If intermediates are still too large, adds more levels

```
100 chunks → 100 summaries → 5 batches → 5 intermediate summaries → final
```

This preserves full document coverage regardless of length - every chunk contributes to the final summary. The tool estimates tokens (~4 chars/token) and targets 60% context window utilization per reduction pass.

**Pros**: Fast, complete coverage, parallel processing, handles any document length
**Cons**: May miss cross-section connections, slower for very long documents

### RAG (Best for Focused Queries)

Best when you need to focus on specific topics or have a targeted question.

```bash
docsummarizer -f document.pdf -m Rag --focus "pricing and payment terms" -v
```

**How it works**:
1. Indexes document chunks as vector embeddings in Qdrant
2. Extracts key topics from document headings
3. Retrieves relevant chunks per topic using semantic search
4. Synthesizes focused summary with citations

**When to use RAG over MapReduce**:

| Scenario | Best Mode |
|----------|-----------|
| "Summarize this whole document" | MapReduce |
| "What does this say about security?" | RAG |
| 500-page manual, need everything | MapReduce (hierarchical) |
| 500-page manual, need specific section | RAG |
| Need fast results, don't have Qdrant | MapReduce |

RAG is **not** about handling long documents - MapReduce handles that with hierarchical reduction. RAG is about **relevance filtering**: when you want to ignore 90% of a document and focus on what matters to your specific question.

**Pros**: Topic-focused, semantic understanding, reuses index, faster for focused queries
**Cons**: May miss content outside focus area, requires Qdrant, slower initial indexing

### Iterative

Best for narrative documents where context flows sequentially.

```bash
docsummarizer -f story.pdf -m Iterative -v
```

**Warning**: Slower and may lose context on long documents (>10 chunks).

## Large Document Guide

### Choosing the Right Mode

| Document Type | Goal | Mode | Why |
|---------------|------|------|-----|
| Technical spec (50+ pages) | Full summary | MapReduce | Complete coverage |
| Novel/Narrative | Full summary | MapReduce | Needs temporal context |
| Legal contract | Full summary | MapReduce | Can't miss clauses |
| Legal contract | "Payment terms?" | RAG | Focus on specific section |
| API docs (200 pages) | "How does auth work?" | RAG | Query specific topic |
| Research paper | Full summary | MapReduce | Structured, need everything |

### Fiction vs Non-Fiction

| Content Type | Best Mode | Notes |
|--------------|-----------|-------|
| **Fiction/Narrative** | MapReduce | Plot requires sequential context |
| **Technical docs** | Both | MapReduce for overview, RAG for specifics |
| **Legal/Contracts** | MapReduce | Every clause matters |
| **Manuals** | RAG | Usually querying for specifics |

### Performance

| Document Size | MapReduce | RAG | Notes |
|---------------|-----------|-----|-------|
| 10 pages | 15s | 20s | Both fast |
| 50 pages | 45s | 30s | RAG faster if focused |
| 200 pages | 3-5 min | 1-2 min | Hierarchical reduction |
| 500+ pages | 10-15 min | 2-3 min | Consider multiple RAG queries |

## Configuration

### Generate Default Configuration

```bash
docsummarizer config --output myconfig.json
```

### Configuration File

Configuration is auto-discovered from:
1. `--config` option
2. `docsummarizer.json` in current directory
3. `.docsummarizer.json` (hidden file)
4. `~/.docsummarizer.json` (user home)

Example `docsummarizer.json`:

```json
{
  "ollama": {
    "model": "llama3.2:3b",
    "embedModel": "nomic-embed-text",
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

### Processing Options

| Option | Default | Description |
|--------|---------|-------------|
| `maxLlmParallelism` | 8 | Concurrent LLM requests (Ollama queues, so higher values just queue) |
| `maxHeadingLevel` | 2 | Split on H1/H2 only. Set to 3 for finer granularity |
| `targetChunkTokens` | 0 (auto) | Target chunk size. 0 = auto-calculate (~25% of context window) |
| `minChunkTokens` | 0 (auto) | Minimum before merging. 0 = 1/8 of target |

## Output Format

### Summary Structure

```C:\Blog\mostlylucidweb\Mostlylucid\Markdown\docsummarizer-tool.md
## Executive Summary
- Key finding 1 with specific details [chunk-0]
- Important point 2 with numbers and dates [chunk-3]
- Critical requirement 3 [chunk-5]

## Section Highlights
- Introduction: Overview of the system architecture [chunk-0]
- Requirements: Technical specifications detailed [chunk-3]
...

## Open Questions
- What is the timeline for Phase 2?
- How does the fallback mechanism work?

### Trace

- Document: document.pdf
- Chunks: 12 total, 12 processed
- Topics: 5
- Time: 21.4s
- Coverage: 100%
- Citation rate: 1.20
```

**Trace metrics**: Coverage (% sections included), Citation rate (citations/bullet), Chunks processed (RAG may skip some).

## Model Recommendations

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `qwen2.5:1.5b` | 986MB | Very Fast (~3s) | Good | **Default** - speed optimized |
| `gemma3:1b` | 815MB | Fast (~10s) | Fair | Alternative small model |
| `llama3.2:3b` | 2GB | Medium (~15s) | Very Good | Balance of speed/quality |
| `ministral-3:3b` | 2.9GB | Medium (~20s) | Very Good | Quality-focused |
| `llama3.1:8b` | 4.7GB | Slow (~45s) | Excellent | High-quality summaries |

> **Tip**: The default `qwen2.5:1.5b` provides good summaries in ~3 seconds. For critical documents where quality matters more than speed, use `--model llama3.2:3b` or larger.

## Build from Source

```bash
# Clone the repository
git clone https://github.com/scottgal/mostlylucidweb.git
cd mostlylucidweb/Mostlylucid.DocSummarizer

# Build
dotnet build

# Run
dotnet run -- --help
```

### Native AOT Compilation

For production deployment with instant startup:

```bash
# Build native executable (Windows x64)
dotnet publish -c Release -r win-x64 --self-contained

# Build for Linux
dotnet publish -c Release -r linux-x64 --self-contained

# Build for macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

Output: `bin/Release/net10.0/<runtime>/publish/docsummarizer` (~24MB)

## Troubleshooting

### "Could not connect to Ollama"
- Ensure Ollama is running: `ollama serve`
- Check models are pulled: `ollama list`

### "Docling service unavailable"
- This is **only required for PDF/DOCX files**
- For Markdown files, you can ignore this error
- To fix: `docker run -p 5001:5001 quay.io/docling-project/docling-serve`

### "Qdrant connection failed"
- This is **only required for RAG mode** (`--mode Rag`)
- For MapReduce mode (default), you can ignore this error
- To fix: `docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant`

### "Circuit breaker is open"
- Ollama is overloaded or has crashed
- Wait 30 seconds for the circuit breaker to reset, or restart Ollama
- The tool uses Polly resilience policies and will auto-retry

### "wsarecv" or Connection Errors (Windows)
- This is a known Ollama issue on Windows (GitHub #13340)
- The tool auto-handles this with retry logic and connection recovery
- If persistent, restart Ollama and try again

### "LLM generation timed out"
- Increase timeout in configuration
- Split very large documents
- Check Ollama is not overloaded with other requests

### Repetitive or Low-Quality Summaries

**Symptoms**: Bullet points echo the prompt ("Return only bullet points", "The rule is...") instead of summarizing content.

**Cause**: Model struggling with the prompt or content too long.

**Fix**: The default `qwen2.5:1.5b` handles most documents well. For problematic documents, try `--model llama3.2:3b`. See [Model Recommendations](#model-recommendations).

### Summary Ignores Document Content

If the summary seems generic or doesn't reference specific content:
- The model may be hallucinating - check `Citation rate` in trace output
- Try RAG mode (`--mode Rag`) which grounds summaries in retrieved chunks
- Use `--verbose` to see which chunks are being processed

### Citations Missing or Invalid

If summaries lack `[chunk-N]` citations:
- Small models prioritize content over citation formatting
- The prompts are optimized for speed, not strict citation compliance
- For strict citations, use larger models like `llama3.2:3b`
- Check `Citation rate` in trace - higher values indicate better traceability

## Performance Tips

- **MapReduce** for speed (parallel chunks)
- **`ministral-3:3b`** for balance, **`llama3.1:8b`** for quality
- **Native AOT** for production (instant startup)
- Lower **`maxLlmParallelism`** if experiencing timeouts

## Resources

- [Source Code](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.DocSummarizer)
- [GitHub Releases](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer)
- [Docling](https://github.com/docling-project/docling) / [Docling Serve](https://github.com/docling-project/docling-serve)
- [Qdrant](https://qdrant.tech/) - Local vector database
- [Ollama](https://ollama.ai/) / [OllamaSharp](https://github.com/awaescher/OllamaSharp)
- [Polly](https://github.com/App-vNext/Polly) - .NET resilience and transient-fault-handling
- [Spectre.Console](https://spectreconsole.net/) - Beautiful terminal UI

### Related
- [Building a Document Summarizer with RAG](/blog/building-a-document-summarizer-with-rag) - The architecture behind this tool
- [CSV Analysis with Local LLMs](/blog/analysing-large-csv-files-with-local-llms)
- [Web Content with LLMs](/blog/fetching-and-analysing-web-content-with-llms)
