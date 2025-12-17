# Mostlylucid.DocSummarizer

A local-first document summarization tool that uses LLMs (via Ollama), vector search (Qdrant), and document conversion (Docling) to create intelligent summaries of DOCX, PDF, and Markdown files.

**Now with Native AOT support for fast startup and minimal memory footprint!**

## Features

- **Multiple Summarization Modes**:
  - **MapReduce**: Parallel chunk summarization with citation tracking
  - **RAG (Retrieval-Augmented Generation)**: Topic-based summarization with vector search
  - **Iterative**: Sequential summarization with context building

- **Native AOT Compilation**: Compile to a single native executable (~18MB) for instant startup
- **Rich Progress Feedback**: Beautiful terminal UI with [Spectre.Console](https://spectreconsole.net/)
- **Batch Processing**: Process entire directories of documents
- **Configurable Timeouts**: 10-minute default timeout for large documents/slow models
- **Multiple Output Formats**: Console, Text, Markdown, JSON
- **Configuration Files**: JSON-based configuration with auto-discovery
- **Document Support**: DOCX, PDF, and Markdown files
- **Citation Tracking**: All summaries include `[chunk-N]` citations for traceability
- **Local Processing**: Everything runs on your machine - no cloud APIs required
- **Quality Metrics**: Coverage scores, citation rates, and processing traces

## Prerequisites

### Required: Ollama

Ollama is the **only requirement** for summarizing Markdown files:

```bash
# Install Ollama from https://ollama.ai
ollama pull qwen2.5:1.5b       # Default model - fast (~3s per doc)
ollama pull mxbai-embed-large  # For RAG mode only (1024 dims)
ollama serve
```

> **Tip**: The default `qwen2.5:1.5b` is optimized for speed. For higher quality summaries, use `--model llama3.2:3b` or `--model ministral-3:3b`.

### Optional: Docling (PDF/DOCX only)

Only needed when summarizing PDF or DOCX files. **Markdown files are read directly - no Docling required.**

```bash
docker run -p 5001:5001 quay.io/docling-project/docling-serve
```

### Optional: Qdrant (RAG mode only)

Only needed for `--mode Rag`. The default MapReduce mode doesn't require Qdrant.

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### Quick Start (Markdown Only)

For Markdown files, you only need Ollama:

```bash
# Minimal setup
ollama pull qwen2.5:1.5b && ollama serve

# Summarize README.md in current directory (auto-saves to README_summary.md)
docsummarizer

# Summarize a specific file
docsummarizer -f mydoc.md
```

### Check Dependencies

Run the built-in check command to verify services are available:

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

Some dependencies are not available.
```

> **Note**: Docling and Qdrant showing ✗ is fine for Markdown-only workflows.

## Installation

### Download Pre-built Binaries

Pre-built native executables are available for all major platforms from the [GitHub Releases](https://github.com/scottgal/mostlylucidweb/releases?q=docsummarizer):

| Platform | Architecture | Download |
|----------|--------------|----------|
| Windows | x64 | `docsummarizer-win-x64.zip` |
| Windows | ARM64 | `docsummarizer-win-arm64.zip` |
| Linux | x64 | `docsummarizer-linux-x64.tar.gz` |
| Linux | ARM64 | `docsummarizer-linux-arm64.tar.gz` |
| macOS | x64 (Intel) | `docsummarizer-osx-x64.tar.gz` |
| macOS | ARM64 (Apple Silicon) | `docsummarizer-osx-arm64.tar.gz` |

```bash
# Extract and run (Linux/macOS)
tar -xzf docsummarizer-linux-x64.tar.gz
chmod +x Mostlylucid.DocSummarizer
./Mostlylucid.DocSummarizer --help

# Extract and run (Windows)
# Unzip docsummarizer-win-x64.zip
.\Mostlylucid.DocSummarizer.exe --help
```

### Build from Source

```bash
cd Mostlylucid.DocSummarizer
dotnet build
dotnet run -- --help
```

### Native AOT Compilation

For instant startup and minimal memory usage, compile to a native executable:

```bash
# Build native executable (Windows x64)
dotnet publish -c Release -r win-x64 --self-contained

# Build for Linux
dotnet publish -c Release -r linux-x64 --self-contained

# Build for macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

The native executable will be in `bin/Release/net10.0/<runtime>/publish/` (~18MB).

### Run as CLI Tool

```bash
# Summarize a document using MapReduce mode
dotnet run -- --file document.pdf --mode MapReduce

# Summarize with RAG mode and focus query
dotnet run -- --file contract.docx --mode Rag --focus "pricing terms"

# Use a higher quality model for important documents
dotnet run -- --file document.pdf --model llama3.1:8b --verbose

# Query a document
dotnet run -- --file manual.pdf --query "How do I install the software?"
```

## Usage

### Default Behavior

Running `docsummarizer` with no arguments will:
1. Look for `README.md` in the current directory
2. Summarize it using MapReduce mode with `qwen2.5:1.5b`
3. Print the summary to console
4. Auto-save to `README_summary.md`

```bash
# Just run it - summarizes README.md and saves to README_summary.md
docsummarizer

# Output:
# No file specified, using README.md in current directory
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

# Use RAG mode with focus (needs Ollama + Qdrant)
docsummarizer -f document.pdf -m Rag --focus "security requirements"

# Use a higher quality model for important documents
docsummarizer -f document.pdf --model llama3.2:3b -v
```

### Batch Processing

Process entire directories of documents:

```bash
# Process all supported files in a directory
dotnet run -- -d ./documents --mode MapReduce -v

# Process only PDFs recursively
dotnet run -- -d ./documents -e .pdf --recursive -v

# Output to Markdown files
dotnet run -- -d ./documents -o Markdown --output-dir ./summaries
```

### Configuration Files

Generate a default configuration file:

```bash
dotnet run -- config --output myconfig.json
```

Use a configuration file:

```bash
dotnet run -- -c myconfig.json -f document.pdf
```

Configuration is auto-discovered from:
1. `--config` option
2. `docsummarizer.json` in current directory
3. `.docsummarizer.json` (hidden file)
4. `~/.docsummarizer.json` (user home)

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

## Timeouts

The tool uses generous timeouts to handle large documents and slow models:

- **LLM Generation**: 10 minutes per operation (configurable)
- **Document Conversion**: 5 minutes for large PDFs

If you encounter timeout errors with large documents:
1. Use a faster model (`gemma3:1b` is very fast)
2. Split very large documents into smaller files
3. Increase timeouts in your configuration file

## Summarization Modes

### MapReduce (Recommended)
Best for comprehensive summaries with full document coverage.

```bash
dotnet run -- -f document.pdf -m MapReduce -v
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
dotnet run -- -f document.pdf -m Rag --focus "pricing and payment terms" -v
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
dotnet run -- -f story.pdf -m Iterative -v
```

**Warning**: Slower and may lose context on long documents (>10 chunks).

## Model Recommendations

| Model | Size | Speed | Quality | Use Case |
|-------|------|-------|---------|----------|
| `gemma3:1b` | 815MB | Very Fast | Poor | Testing only - not recommended |
| `ministral-3:3b` | 2.9GB | Fast | Very Good | **Default - best balance** |
| `llama3.2:3b` | 2GB | Fast | Good | General purpose |
| `qwen2.5:3b` | 1.9GB | Fast | Good | Multilingual documents |
| `llama3.1:8b` | 4.7GB | Medium | Excellent | High-quality summaries |

### Performance Example

Summarizing "The Sign of the Four" (120KB DOCX, 12 chapters):

| Model | Time | Quality |
|-------|------|---------|
| `gemma3:1b` | 21s | Good |
| `ministral-3:3b` | 107s | Very Good |
| `llama3.1:8b` | ~180s | Excellent |

## Output

### Summary Structure

With verbose mode (`-v`), you'll see a live progress table:

```
╭───────┬─────────────────────────────────────────┬───────────────╮
│ Chunk │ Section                                 │    Status     │
├───────┼─────────────────────────────────────────┼───────────────┤
│   0   │ 1 The Science of Deduction              │     Done      │
│   1   │ 2 The Statement of the Case             │ Processing... │
│   2   │ 3 In Quest of a Solution                │    Pending    │
...
```

Final output:
```
═══════════════════════════════════════════════════════════════
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
═══════════════════════════════════════════════════════════════

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

## Architecture

### Native AOT Support

The project supports .NET Native AOT compilation for:
- **Instant startup**: No JIT compilation delay
- **Smaller memory footprint**: ~18MB native executable
- **Single-file deployment**: No runtime dependencies

AOT compatibility is achieved through:
- Source-generated JSON serialization (`DocSummarizerJsonContext`)
- Custom `JsonSerializerContext` for OllamaSharp integration
- Trimming-safe code patterns

### Components

1. **DoclingClient**: Converts DOCX/PDF to Markdown
2. **DocumentChunker**: Context-aware splitting (merges small sections to target chunk size)
3. **OllamaService**: LLM inference and embeddings with configurable timeout
4. **MapReduceSummarizer**: Parallel chunk processing with progress feedback
5. **RagSummarizer**: Vector-based retrieval and synthesis
6. **CitationValidator**: Ensures all citations are valid
7. **ProgressService**: Spectre.Console-based progress UI
8. **BatchProcessor**: Directory-level batch processing
9. **OutputFormatter**: Multiple output format support

### Document Processing Pipeline

```
Document (PDF/DOCX/MD)
  ↓
[Docling] Convert to Markdown (5 min timeout)
  ↓
[Chunker] Split by structure
  ↓
[Summarizer] Process chunks (10 min timeout per chunk)
  ↓
[Validator] Check citations
  ↓
DocumentSummary
```

## Configuration

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
    "deleteCollectionAfterSummarization": true
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

## Testing

### Run Unit Tests

```bash
dotnet test Mostlylucid.DocSummarizer.Tests
```

### Integration Tests

Integration tests require all services running and are skipped by default:

```bash
# Start all services first
ollama serve
docker run -p 5001:5001 quay.io/docling-project/docling-serve
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# Run all tests including integration tests
dotnet test Mostlylucid.DocSummarizer.Tests --filter Category!=IntegrationTest
```

## PDF Processing Configuration

The tool uses [Docling](https://github.com/docling-project/docling-serve) for PDF conversion. For large PDFs, split processing provides better progress feedback and resilience.

### PDF Backend Options

| Backend | Description | Recommendation |
|---------|-------------|----------------|
| `pypdfium2` | Pure Python PDF parser | **Default** - Most compatible with various PDFs |
| `dlparse_v4` | Docling's latest parser | Faster but may fail on some PDFs with non-standard page dimensions |
| `dlparse_v2` | Older Docling parser | Fallback option |
| `dlparse_v1` | Legacy parser | Not recommended |

If you encounter `could not find the page-dimensions` errors, ensure you're using `pypdfium2`:

```json
{
  "docling": {
    "pdfBackend": "pypdfium2"
  }
}
```

### Split Processing

For large PDFs (>50 pages), the tool splits the document into chunks for parallel processing:

- **`pagesPerChunk`**: Number of pages per chunk (default: 50)
- **`maxConcurrentChunks`**: Maximum parallel conversions (default: 4)
- **`enableSplitProcessing`**: Enable/disable split processing (default: true)

Progress output shows each chunk being processed:
```
PDF has 367 pages
Split processing: 8 chunks (50 pages each, max 4 concurrent)
Wave 1: [0:p1-50][1:p51-100][2:p101-150][3:p151-200]...[0:ok][1:ok][2:ok][3:ok]
Wave 2: [4:p201-250][5:p251-300][6:p301-350][7:p351-367]...[4:ok][5:ok][6:ok][7:ok]
Conversion complete: 8/8 chunks in 45s
```

To disable split processing (process entire PDF at once):

```json
{
  "docling": {
    "enableSplitProcessing": false
  }
}
```

### LLM Parallelism

When processing large documents with many chunks, the tool limits concurrent LLM requests to avoid overwhelming Ollama and prevent 500 errors. The default limit is 8 concurrent requests.

- **`maxLlmParallelism`**: Maximum concurrent LLM requests (default: 8)

Ollama processes one request at a time per model, so high values just queue requests. The default of 8 provides a good balance between throughput and memory usage.

To adjust for your system:

```json
{
  "processing": {
    "maxLlmParallelism": 4
  }
}
```

Setting a lower value (e.g., 4) can help if you're running on a system with limited memory or experiencing timeouts. Setting it higher (e.g., 16) may help throughput on systems with more resources, but benefits are limited since Ollama serializes requests.

### Chunking Configuration

The chunker intelligently combines small sections to create optimal chunk sizes for the LLM context window:

```json
{
  "processing": {
    "maxHeadingLevel": 2,
    "targetChunkTokens": 0,
    "minChunkTokens": 0
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `maxHeadingLevel` | 2 | Split on H1 and H2 only. Set to 3 for finer granularity. |
| `targetChunkTokens` | 0 (auto) | Target chunk size. 0 = auto-calculate from model context window (~25% of context). |
| `minChunkTokens` | 0 (auto) | Minimum size before merging. 0 = 1/8 of target. |

**Auto-calculation**: When set to 0, the tool queries the model's context window and calculates:
- Target = context_window / 4 (clamped to 2000-16000 tokens)
- Min = target / 8 (minimum 500 tokens)

For example, with `ministral-3:3b` (128K context):
- Target = 128000 / 4 = 32000 → clamped to 16000 tokens
- Min = 16000 / 8 = 2000 tokens

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

## Project Structure

```
Mostlylucid.DocSummarizer/
├── Config/
│   ├── ConfigurationLoader.cs     # Auto-discovery configuration
│   ├── DocSummarizerConfig.cs     # Configuration models
│   └── DocSummarizerJsonContext.cs # AOT JSON serialization
├── Models/
│   └── DocumentModels.cs          # Domain models and records
├── Services/
│   ├── DocumentSummarizer.cs      # Main orchestrator
│   ├── DocumentChunker.cs         # Markdown chunking
│   ├── OllamaService.cs           # LLM integration with timeout
│   ├── DoclingClient.cs           # Document conversion
│   ├── MapReduceSummarizer.cs     # Parallel summarization
│   ├── RagSummarizer.cs           # Vector-based summarization
│   ├── ProgressService.cs         # Spectre.Console progress UI
│   ├── BatchProcessor.cs          # Directory batch processing
│   └── OutputFormatter.cs         # Multiple output formats
├── Program.cs                      # CLI interface
├── docsummarizer.example.json     # Example configuration
└── Mostlylucid.DocSummarizer.csproj

Mostlylucid.DocSummarizer.Tests/
├── Models/
│   ├── CitationValidatorTests.cs
│   └── HashHelperTests.cs
├── Services/
│   ├── DocumentChunkerTests.cs
│   ├── DocumentSummarizerTests.cs
│   └── OllamaServiceTests.cs
└── Mostlylucid.DocSummarizer.Tests.csproj
```

## License

This project is part of the Mostlylucid blog project.

## Contributing

Contributions welcome! Please ensure:
- Tests pass: `dotnet test`
- Code follows existing patterns
- XML documentation for public APIs

## Credits

- **Ollama**: Local LLM inference
- **Docling**: Document conversion
- **Qdrant**: Vector database
- **OllamaSharp**: .NET Ollama client library
- **Spectre.Console**: Beautiful terminal UI
