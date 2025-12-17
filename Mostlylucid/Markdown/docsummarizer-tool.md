# docsummarizer - Local Document Summarization CLI Tool

<!--category-- AI, LLM, RAG, C#, Docling, Ollama, Qdrant, Tools -->
<datetime class="hidden">2025-12-21T11:00</datetime>

[![GitHub release](https://img.shields.io/github/v/release/scottgal/mostlylucidweb?filter=docsummarizer*&label=docsummarizer)](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Enabled-brightgreen)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

A local-first document summarization tool that uses LLMs (via Ollama), vector search (Qdrant), and document conversion (Docling) to create intelligent summaries of DOCX, PDF, and Markdown files.

> **Note**: This is not a fast tool - even on powerful hardware (RTX A4000 + Ryzen 9950X), summarizing takes several minutes. You trade speed for privacy, zero API costs, and offline operation. The smaller context windows of local models (8K-32K vs 128K+ for cloud) actually help here - they force better chunking and maintain coherence within each chunk.

For the architecture and approach behind this tool, see [Building a Document Summarizer with RAG](/blog/building-a-document-summarizer-with-rag).

[TOC]

## Features

- **Multiple Summarization Modes**: MapReduce (parallel), RAG (topic-driven), and Iterative
- **Native AOT Compilation**: ~24MB native executable for instant startup
- **Rich Progress Feedback**: Live terminal UI with [Spectre.Console](https://spectreconsole.net/)
- **Citation Tracking**: All summaries include `[chunk-N]` citations for traceability
- **Batch Processing**: Process entire directories of documents
- **Multiple Output Formats**: Console, Text, Markdown, JSON
- **Configuration Files**: JSON-based configuration with auto-discovery
- **Local Processing**: Everything runs on your machine - no cloud APIs required

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
ollama pull ministral-3:3b    # Default model - good quality (recommended)
ollama pull nomic-embed-text  # For RAG mode only
ollama serve
```

> **Warning**: Models under 3B parameters (e.g., `gemma3:1b`) produce poor quality summaries with repetitive or nonsensical output. Use `ministral-3:3b` or larger.

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
    - ministral-3:3b
    - nomic-embed-text
    ... and more

  Default model info:
    Name: ministral-3:3b
    Family: mistral
    Parameters: 3.2B
    Context Window: 128,000 tokens

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
# Model: ministral-3:3b
# ...
# Saved to: readme.summary.md
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
docsummarizer -f document.pdf --model llama3.1:8b -v
```

### Query Mode

Instead of summarizing, ask questions about a document:

```bash
docsummarizer -f manual.pdf --query "How do I install the software?"
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
| `--mode` | `-m` | Summarization mode: MapReduce, Rag, Iterative | `MapReduce` |
| `--focus` | | Focus query for RAG mode | None |
| `--query` | `-q` | Query mode instead of summarization | None |
| `--model` | | Ollama model to use | `ministral-3:3b` |
| `--verbose` | `-v` | Show detailed progress with live UI | `false` |
| `--config` | `-c` | Path to configuration file | Auto-discover |
| `--output-format` | `-o` | Output format: Console, Text, Markdown, Json | `Console` |
| `--output-dir` | | Output directory for file outputs | Current dir |
| `--extensions` | `-e` | File extensions for batch mode | `.pdf .docx .md` |
| `--recursive` | `-r` | Process directories recursively | `false` |

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
    "model": "ministral-3:3b",
    "embedModel": "nomic-embed-text",
    "baseUrl": "http://localhost:11434",
    "temperature": 0.3,
    "timeoutMinutes": 10
  },
  "docling": {
    "baseUrl": "http://localhost:5001",
    "timeoutMinutes": 10,
    "pdfBackend": "pypdfium2",
    "pagesPerChunk": 50,
    "maxConcurrentChunks": 4,
    "enableSplitProcessing": true
  },
  "qdrant": {
    "host": "localhost",
    "port": 6334,
    "collectionName": "documents"
  },
  "processing": {
    "maxHeadingLevel": 2,
    "targetChunkTokens": 0,
    "minChunkTokens": 0,
    "maxLlmParallelism": 8
  },
  "output": {
    "format": "Console",
    "verbose": false
  },
  "batch": {
    "fileExtensions": [".pdf", ".docx", ".md"],
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

```
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
| `gemma3:1b` | 815MB | Very Fast | Poor | Testing only |
| `ministral-3:3b` | 2.9GB | Fast | Very Good | **Default** |
| `llama3.2:3b` | 2GB | Fast | Good | General purpose |
| `llama3.1:8b` | 4.7GB | Medium | Excellent | High-quality summaries |

> **Warning**: Models under 3B parameters produce poor summaries - often repetitive bullet points echoing the prompt. Use `ministral-3:3b` or larger.

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

### "LLM generation timed out"
- Increase timeout in configuration
- Split very large documents
- Check Ollama is not overloaded with other requests

### Repetitive or Low-Quality Summaries

**Symptoms**: Bullet points echo the prompt ("Return only bullet points", "The rule is...") instead of summarizing content.

**Cause**: Model too small (<3B parameters).

**Fix**: Use `ministral-3:3b` or larger. See [Model Recommendations](#model-recommendations).

### Summary Ignores Document Content

If the summary seems generic or doesn't reference specific content:
- The model may be hallucinating - check `Citation rate` in trace output
- Try RAG mode (`--mode Rag`) which grounds summaries in retrieved chunks
- Use `--verbose` to see which chunks are being processed

### Citations Missing or Invalid

If summaries lack `[chunk-N]` citations:
- Some smaller models struggle to follow citation instructions
- The tool retries once with stronger prompting, but may still fail
- Use a 3B+ parameter model for reliable citations
- Check `Citation rate` in trace - should be > 0.5 for good traceability

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
- [Spectre.Console](https://spectreconsole.net/) - Beautiful terminal UI

### Related
- [Building a Document Summarizer with RAG](/blog/building-a-document-summarizer-with-rag) - The architecture behind this tool
- [CSV Analysis with Local LLMs](/blog/analysing-large-csv-files-with-local-llms)
- [Web Content with LLMs](/blog/fetching-and-analysing-web-content-with-llms)
