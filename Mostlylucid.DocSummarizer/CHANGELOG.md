# Changelog - DocSummarizer

## v2.6.0 - LLM Tool Mode, Web Fetching & Security Hardening (2025-12-17)

### New Features

#### LLM Tool Mode (`tool` command)

A new command designed for AI agent integration, MCP servers, and automated pipelines:

```bash
# Summarize and get structured JSON output
docsummarizer tool -f document.pdf
docsummarizer tool -u "https://example.com/docs"
```

Output includes:
- Evidence-grounded claims with confidence levels (high/medium/low)
- Chunk IDs for citation tracking
- Structured topics with evidence references
- Named entities (people, organizations, concepts)
- Processing metadata (time, model, mode)

#### Security-Hardened Web Fetching

New `WebFetcher` service with comprehensive security controls:

- **SSRF Protection**: Blocks private IPs (10.x, 172.16.x, 192.168.x), localhost, link-local, cloud metadata endpoints (169.254.169.254)
- **DNS Rebinding Protection**: Re-validates IP addresses after each redirect
- **Request Limits**: Max 5 redirects, 10MB response size, configurable timeouts
- **Content-Type Gating**: Only accepts safe document types (HTML, PDF, images, text)
- **Decompression Bomb Protection**: Limits expansion ratio to 20x
- **HTML Sanitization**: Removes scripts, event handlers, dangerous URLs via AngleSharp
- **Image Guardrails**: 50 images max, 1920px max dimension, hash deduplication
- **Protocol Downgrade Prevention**: Blocks HTTPS→HTTP redirects

```bash
# Fetch and summarize a web page
docsummarizer -f https://example.com/docs --web-enabled
```

#### Quality Analysis System

New `QualityAnalyzer` service for summary quality assessment:

- **Citation Metrics**: Coverage, density, orphan detection
- **Coherence Metrics**: Flow, redundancy, structure analysis
- **Entity Metrics**: Consistency checking across summary
- **Factuality Metrics**: Hallucination detection heuristics
- **Evidence Density**: Supported vs unsupported claims ratio
- **Quality Grades**: A-F scoring based on composite metrics

#### Text Analysis Service

New `TextAnalysisService` for advanced text processing:

- **Entity Normalization**: Fuzzy matching to deduplicate "John Smith" / "J. Smith"
- **Semantic Deduplication**: Jaro-Winkler similarity for near-duplicate detection
- **TF-IDF Weighting**: Keyword extraction and importance scoring
- **AOT Compatible**: All algorithms implemented inline (no reflection)

#### Structured MapReduce Summarizer

New `StructuredMapReduceSummarizer` for JSON extraction mode:

- Produces structured JSON in map phase for better merging
- Loss-aware reduce phase preserves important details
- Schema-driven output for consistent parsing

#### Rich Terminal UI

New `TuiProgressService` using Terminal.Gui:

- Real-time progress visualization with animated bars
- Chunk-by-chunk status tracking
- LLM activity monitoring
- Batch processing dashboard
- Works alongside existing console progress

#### 11 Summary Templates (was 9)

Two new templates added:

- **`bookreport`** (~400 words): Classic book report style with overview, characters/key players, plot/content, themes, and opinion sections
- **`meeting`** (~200 words): Meeting notes format with summary, key decisions, action items, and open questions

#### Template Word Count Syntax

Specify custom word count inline with template name:

```bash
# Use bookreport template with 500 word target
docsummarizer -f doc.pdf -t bookreport:500

# Executive summary limited to 100 words
docsummarizer -f doc.pdf -t executive:100

# --words flag still works and takes precedence
docsummarizer -f doc.pdf -t detailed --words 300
```

### Improvements

- **CLI**: Added `-t/--template` and `-w/--words` options to root command
- **JSON Context**: Added all Tool output types for AOT serialization
- **Progress Reporting**: New `IProgressReporter` interface for pluggable progress
- **Batch Processing**: Enhanced `TuiBatchProcessor` with Terminal.Gui dashboard

### Files Added

```
Services/
├── WebFetcher.cs                    # Security-hardened web content fetcher
├── QualityAnalyzer.cs               # Summary quality assessment
├── TextAnalysisService.cs           # Entity normalization, TF-IDF
├── StructuredMapReduceSummarizer.cs # JSON extraction mode
├── TuiProgressService.cs            # Terminal.Gui progress UI
├── TuiBatchProcessor.cs             # TUI batch processing
└── IProgressReporter.cs             # Progress abstraction

Models/
└── DocumentModels.cs                # ToolOutput, GroundedClaim, etc.
```

### Files Modified

```
Program.cs                           # tool command, template options
Config/SummaryTemplates.cs           # bookreport, meeting templates
Config/DocSummarizerJsonContext.cs   # Tool output serialization
README.md                            # Updated documentation
```

### Security Notes

The `WebFetcher` implements defense-in-depth:

1. **Pre-flight**: URL scheme validation (http/https only)
2. **DNS Resolution**: IP validation before connection
3. **Request**: Size limits, timeout, no cookies/auth
4. **Redirect**: Re-validate each hop, limit count
5. **Response**: Content-type check, size enforcement
6. **Processing**: HTML sanitization, image resizing

### Breaking Changes

None - all new features are additive.

### Usage Examples

```bash
# LLM Tool Mode - JSON output for agents
docsummarizer tool -f contract.pdf -q "payment terms"
docsummarizer tool -u "https://docs.example.com" | jq '.summary.keyFacts'

# Web fetching (requires explicit opt-in)
docsummarizer -u "https://example.com/whitepaper.pdf" --web-enabled

# New templates
docsummarizer -f novel.pdf -t bookreport
docsummarizer -f transcript.md -t meeting:300

# Template with custom word count
docsummarizer -f report.pdf -t executive:75
```

---

## v2.1.0 - Summary Templates & Expanded Format Support (2025-12-17)

### New Features

#### Expanded Input Format Support

All formats supported by Docling are now documented and supported:

- **Direct read** (no Docling needed): Plain text (.txt), Markdown (.md)
- **Documents**: PDF, DOCX, XLSX, PPTX
- **Web/Text**: HTML, XHTML, CSV, AsciiDoc
- **Images**: PNG, JPEG, TIFF, BMP, WEBP (with OCR)
- **Captions**: WebVTT (.vtt)
- **XML**: USPTO patents, JATS articles

#### Smart Plain Text Chunking

- Automatically detects plain text vs markdown
- Splits plain text by paragraphs (double newlines)
- Extracts titles from short first paragraphs
- Uses first sentence as chunk heading for better traceability

#### Summary Templates System

- **9 built-in templates** for different use cases:
    - `default` - Balanced prose with topic breakdowns
    - `brief` - Quick 2-3 sentence summary
    - `oneliner` - Single sentence (25 words max)
    - `bullets` - Key takeaways as bullet points
    - `executive` - Leadership briefing format
    - `detailed` - Comprehensive 500+ word analysis
    - `technical` - Implementation-focused for tech docs
    - `academic` - Formal abstract structure
    - `citations` - Key quotes with source references

- **Template properties** control:
    - Target word count and bullet limits
    - Output style (Prose, Bullets, Mixed, CitationsOnly)
    - Section visibility (topics, citations, questions, trace)
    - Tone (Professional, Casual, Academic, Technical)
    - Audience level (General, Executive, Technical)
    - Custom LLM prompts with placeholder substitution

- **CLI integration**:
    - `docsummarizer templates` - List available templates
    - `--template` / `-t` - Select template by name
    - `--words` / `-w` - Override target word count

### Improvements

- **Template-aware OutputFormatter**: Applies template settings to control output sections
- **Template aliases**: `exec` → `executive`, `tech` → `technical`, etc.
- **Prompt customization**: Override executive, topic, and chunk prompts per template

### Files Added/Modified

```
Config/
└── SummaryTemplates.cs           # Template system with 9 presets

Services/
├── MapReduceSummarizer.cs        # Template integration
├── RagSummarizer.cs              # Template integration
├── OutputFormatter.cs            # Template-aware formatting
└── DocumentSummarizer.cs         # Template parameter support

Program.cs                         # --template and --words options
```

### Usage Examples

```bash
# Quick one-liner for file preview
docsummarizer -f doc.pdf -t oneliner

# Executive briefing
docsummarizer -f report.pdf --template executive

# Technical documentation summary
docsummarizer -f api-spec.md -t tech --focus "authentication"

# Custom word count
docsummarizer -f doc.pdf --template brief --words 75
```

---

## v2.0.0 - Major Feature Release (2025-12-16)

### 🎉 New Features

#### 1. Configuration System

- **JSON-based configuration** with sensible defaults
- Automatic discovery from multiple locations:
    - Custom path via `--config` option
    - `docsummarizer.json` (current directory)
    - `.docsummarizer.json` (current directory)
    - `~/.docsummarizer.json` (user home)
- Comprehensive configuration sections for all aspects of the tool
- Generate default config with `dotnet run -- config`

#### 2. Batch Processing

- Process entire directories with `--directory` option
- File extension filtering (e.g., `--extensions .pdf .docx .md`)
- Recursive directory scanning with `--recursive`
- Glob pattern support for include/exclude
- Continue on error or fail-fast modes
- Comprehensive batch summary reports

#### 3. Multiple Output Formats

- **Console**: Formatted terminal output (default)
- **Text**: Plain text files for archival
- **Markdown**: Rich formatting with tables
- **JSON**: Machine-readable structured data
- Configurable output directory
- Automatic file naming based on source documents

#### 4. AOT Compilation Support

- Native AOT compilation enabled
- Full trimming support for smaller binaries
- JSON source generation for AOT compatibility
- Faster startup times (<100ms)
- Smaller memory footprint
- Platform-specific optimized builds

#### 5. Enhanced Model Support

- **Default model changed**: `ministral-3:3b` (Mistral-3 3B)
- **128K context window** (vs 8K in llama3.2:3b)
- Model information inspection with `check --verbose`
- Display context window, parameters, quantization
- List all available models

### 🔧 Improvements

#### OllamaService

- Added `GetModelInfoAsync()` for model inspection
- Added `GetAvailableModelsAsync()` for model listing
- Added `Model` and `EmbedModel` properties
- Known context window mappings for common models

#### CLI Enhancements

- Redesigned command-line interface
- More intuitive option names
- Better help text
- Enhanced `check` command with verbose mode

#### Code Quality

- Source-generated JSON serialization
- AOT-compatible throughout
- Better error handling
- Improved nullability annotations

### 📦 New Files

```
Config/
├── DocSummarizerConfig.cs        # Configuration models
├── ConfigurationLoader.cs        # Config loading logic
└── DocSummarizerJsonContext.cs   # Source-generated serialization

Services/
├── BatchProcessor.cs             # Batch processing
└── OutputFormatter.cs            # Output formatting

docsummarizer.example.json        # Example configuration
FEATURES.md                       # Feature documentation
CHANGELOG.md                      # This file
```

### 🔄 Modified Files

```
Services/
├── OllamaService.cs              # Added model inspection
├── DocumentSummarizer.cs         # Config support
└── DoclingClient.cs              # AOT-compatible JSON

Models/
└── DocumentModels.cs             # Added batch models

Program.cs                         # Complete redesign
Mostlylucid.DocSummarizer.csproj  # AOT enabled
```

### 📊 Breaking Changes

#### Default Model Change

- **Old**: `llama3.2:3b` (8K context)
- **New**: `ministral-3:3b` (128K context)

**Migration**: Pull the new model

```bash
ollama pull ministral-3:3b
```

#### Configuration

- Configuration now preferred over CLI options
- CLI options override config file settings
- Sensible defaults provided

### 🚀 Usage Examples

#### Single File (Simple)

```bash
# Uses defaults
dotnet run -- --file document.pdf
```

#### Batch Processing

```bash
# Process directory
dotnet run -- --directory ./docs --extensions .pdf .docx --recursive
```

#### Custom Configuration

```bash
# Use config file
dotnet run -- --config prod.json --file document.pdf
```

#### Output Formats

```bash
# Markdown output
dotnet run -- -f doc.pdf -o Markdown --output-dir ./summaries

# JSON for APIs
dotnet run -- -f doc.pdf -o Json --output-dir ./api-data
```

#### Model Information

```bash
# Check dependencies with model info
dotnet run -- check --verbose
```

Expected output:

```
Checking dependencies...

  Ollama: ✓ (http://localhost:11434)

  Available models:
    - ministral-3:3b
    - nomic-embed-text
    - llama3.2:3b
    ... and 5 more

  Default model info:
    Name: ministral-3:3b
    Family: mistral
    Parameters: 3B
    Quantization: Q4_0
    Context Window: 128,000 tokens
    Format: gguf

  Embed model info:
    Name: nomic-embed-text
    Family: nomic-bert
    Parameters: 137M
```

### 🏗️ AOT Publishing

#### Windows

```bash
dotnet publish -c Release -r win-x64
```

#### Linux

```bash
dotnet publish -c Release -r linux-x64
```

#### macOS (ARM)

```bash
dotnet publish -c Release -r osx-arm64
```

### 📈 Performance Improvements

| Metric         | Before    | After       | Improvement    |
|----------------|-----------|-------------|----------------|
| Startup time   | ~2s       | <100ms      | **20x faster** |
| Binary size    | ~150MB    | ~40MB       | **4x smaller** |
| Memory usage   | ~150MB    | ~50MB       | **3x less**    |
| Context window | 8K tokens | 128K tokens | **16x larger** |

### 🐛 Bug Fixes

- Fixed JSON serialization for AOT compatibility
- Fixed DocumentChunker heading logic for documents without headings
- Fixed test issues with integration test skipping
- Fixed nullable reference warnings

### 🔐 Security

- AOT compilation reduces attack surface
- No runtime code generation
- Trimming removes unused code
- Source-generated serialization (no reflection)

### 📚 Documentation

- New `FEATURES.md` with comprehensive feature documentation
- Updated `README.md` with new features
- Example configuration file with comments
- This changelog for tracking changes

### 🙏 Credits

- **Mistral AI** for the excellent ministral-3 model
- **Ollama team** for local LLM inference
- **.NET team** for Native AOT support

### ⬆️ Upgrade Guide

1. **Pull new model**:
   ```bash
   ollama pull ministral-3:3b
   ```

2. **Generate config** (optional):
   ```bash
   dotnet run -- config
   ```

3. **Update existing scripts** to use new CLI options (backward compatible)

4. **Test with new features**:
   ```bash
   dotnet run -- check --verbose
   ```

### 🔮 Future Roadmap

- [ ] Streaming output for real-time feedback
- [ ] Custom prompt templates
- [ ] Plugin system for custom processors
- [ ] Web API for remote access
- [ ] Docker container with all dependencies
- [ ] Kubernetes Helm chart
- [ ] Resume support for interrupted batch jobs
- [ ] Parallel batch processing

### 📞 Support

For issues, questions, or contributions:

- Check `README.md` for usage documentation
- See `FEATURES.md` for feature details
- Review `docsummarizer.example.json` for configuration options

---

## v1.0.0 - Initial Release

- Basic document summarization
- Three summarization modes (MapReduce, RAG, Iterative)
- Support for PDF, DOCX, and Markdown files
- Citation tracking
- Integration with Ollama, Docling, and Qdrant
