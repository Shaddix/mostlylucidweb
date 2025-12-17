# docsummarizer - Local Document Summarization CLI Tool

<!--category-- AI, LLM, RAG, C#, Docling, Ollama, Qdrant, Tools -->
<datetime class="hidden">2025-12-21T11:00</datetime>

[![GitHub release](https://img.shields.io/github/v/release/scottgal/mostlylucidweb?filter=docsummarizer*&label=docsummarizer)](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Enabled-brightgreen)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

A local-first document summarization tool that uses LLMs (via Ollama), vector search (Qdrant), and document conversion (Docling) to create intelligent summaries of DOCX, PDF, and Markdown files.

> **Note**: This is not a fast tool - even on a powerful machine (RTX A4000 + Ryzen 9950X), summarizing a moderately sized document takes several minutes. Local LLM inference is inherently slower than cloud APIs, but you get privacy, no API costs, and offline operation in exchange.

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

### Quick Start (Markdown Only)

For Markdown files, you only need Ollama:

```bash
# Minimal setup
ollama pull ministral-3:3b && ollama serve

# Summarize README.md in current directory (auto-saves to readme.summary.md)
docsummarizer

# Summarize a specific file
docsummarizer -f mydoc.md
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

**Pros**: Fast, complete coverage, parallel processing
**Cons**: May miss cross-section connections

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

**Pros**: Topic-focused, semantic understanding, reuses index
**Cons**: May miss some content, slower initial indexing

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

### LLM Parallelism

For large documents with many chunks, the tool limits concurrent LLM requests to avoid overwhelming Ollama:

```json
{
  "processing": {
    "maxLlmParallelism": 8
  }
}
```

Ollama processes one request at a time per model, so high values just queue requests. The default of 8 provides a good balance between throughput and memory usage.

### Chunking Configuration

The chunker intelligently combines small sections to create optimal chunk sizes based on the model's context window:

| Option | Default | Description |
|--------|---------|-------------|
| `maxHeadingLevel` | 2 | Split on H1 and H2 only. Set to 3 for finer granularity. |
| `targetChunkTokens` | 0 (auto) | Target chunk size. 0 = auto-calculate from model context window (~25% of context). |
| `minChunkTokens` | 0 (auto) | Minimum size before merging. 0 = 1/8 of target. |

When set to 0, the tool queries the model's context window and calculates optimal chunk sizes. For `ministral-3:3b` (128K context), this results in ~16000 token chunks.

## Progress Feedback

With verbose mode (`-v`), you get a live progress table:

```
Summarizing: contract.pdf
Mode: MapReduce
Model: ministral-3:3b

Map Phase: Summarizing 12 chunks (8 parallel)...
+-------+-----------------------------------------+---------------+
| Chunk | Section                                 |    Status     |
+-------+-----------------------------------------+---------------+
|   0   | 1 The Science of Deduction              |     Done      |
|   1   | 2 The Statement of the Case             | Processing... |
|   2   | 3 In Quest of a Solution                |    Pending    |
...
```

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

### Understanding the Trace

- **Coverage**: Percentage of document sections included in summary
- **Citation rate**: Average citations per bullet point (higher = more traceable)
- **Chunks processed**: In RAG mode, may be less than total chunks

## Model Recommendations

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `gemma3:1b` | 815MB | Very Fast | Poor | Testing only - not recommended |
| `ministral-3:3b` | 2.9GB | Fast | Very Good | **Default - best balance** |
| `llama3.2:3b` | 2GB | Fast | Good | General purpose |
| `qwen2.5:3b` | 1.9GB | Fast | Good | Multilingual documents |
| `llama3.1:8b` | 4.7GB | Medium | Excellent | High-quality summaries |

> **Warning**: Models under 3B parameters produce poor quality summaries. The output often contains repetitive bullet points that echo the prompt instructions rather than summarizing the document. Always use `ministral-3:3b` or larger for real work.

### Performance Example

Summarizing "The Sign of the Four" (120KB DOCX, 12 chapters):

| Model | Time | Quality |
|-------|------|---------|
| `gemma3:1b` | 21s | Poor (repetitive) |
| `ministral-3:3b` | 107s | Very Good |
| `llama3.1:8b` | ~180s | Excellent |

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

If your summaries contain repetitive bullet points, nonsensical content, or miss key information:

**Symptoms:**
```
- **Rule:** Return only bullet points.
- **Section:** Return bullets only.
- **Number:** 1. The rule is to provide bullet points.
```

**Cause**: Model is too small. Models under 3B parameters struggle with summarization instructions.

**Fix**: Use `ministral-3:3b` (default) or larger:
```bash
docsummarizer -f doc.md --model ministral-3:3b   # Recommended
docsummarizer -f doc.md --model llama3.1:8b      # Best quality
```

**Model size guidelines:**
| Size | Examples | Quality |
|------|----------|---------|
| < 1B | `gemma3:1b`, `tinyllama` | Poor - testing only |
| 3B | `ministral-3:3b`, `llama3.2:3b` | Good - recommended |
| 7-8B | `llama3.1:8b`, `mistral:7b` | Excellent |

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

1. **Use MapReduce for speed**: Processes chunks in parallel
2. **Use `ministral-3:3b` as default**: Best balance of speed and quality
3. **Use `llama3.1:8b` for important docs**: Higher quality, slower
4. **Native AOT for production**: Instant startup, smaller footprint
5. **Limit chunk size**: Documents with very large sections may need chunking adjustments
6. **Adjust parallelism**: Lower `maxLlmParallelism` if experiencing timeouts or memory issues

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
