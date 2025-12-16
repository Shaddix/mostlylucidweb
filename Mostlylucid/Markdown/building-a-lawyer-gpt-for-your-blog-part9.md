# Building a "Lawyer GPT" for Your Blog - Part 9: Document Ingestion with Docling

<!--category-- AI, LLM, Docling, RAG, C#, AI-Article, mostlylucid.blogllm -->
<datetime class="hidden">2025-12-15T22:45</datetime>

Welcome to Part 9! In previous parts, we've built a robust RAG system that processes markdown blog posts and makes them searchable through semantic embeddings. Now it's time to expand our capabilities with **[Docling](https://github.com/docling-project/docling)** - a powerful document processing library that can ingest PDFs, DOCX, and other formats, making our Lawyer GPT truly comprehensive.

> NOTE: This is part of my experiments with AI (assisted drafting) + my own editing. Same voice, same pragmatism; just faster fingers.

Not that I NEED this for the blog (it's all markdown), but for completion I thought I'd show how to easily add document ingestion capabilities to your RAG pipeline. This is particularly useful if you're building a system that needs to process legal documents, contracts, or other business documents.

[TOC]

## Why Document Ingestion Matters

So far our Lawyer GPT can only understand markdown files. That's fine for a blog, but real-world knowledge bases contain:

- **PDF contracts** - the lifeblood of legal work
- **Word documents** - briefs, motions, correspondence
- **PowerPoint presentations** - client pitches, case summaries
- **Scanned documents** - older records that need OCR
- **HTML pages** - web content, online resources

If we want our system to act like a real legal AI assistant, it needs to consume all of these. That's where Docling comes in.

## What is Docling?

[Docling](https://github.com/docling-project/docling) is an open-source document processing toolkit from IBM. Think of it as a universal translator for documents - you feed it a PDF, Word doc, or PowerPoint, and it spits out clean, structured markdown.

Why markdown? Because that's what our existing pipeline already understands! We've already built chunking, embedding generation, and vector storage for markdown content. By converting everything to markdown first, we can reuse all that infrastructure.

**What Docling handles:**
- PDF extraction (both native and scanned via OCR)
- DOCX and DOC files
- PPTX presentations
- HTML pages
- Images with text (via OCR)

**What makes it special:**
- Preserves document structure (headings, lists, tables)
- Handles complex layouts (multi-column, mixed content)
- Table extraction actually works (unlike many PDF parsers)
- Runs locally - no data sent to external services

## Running Docling as a Service

Docling itself is a Python library, but the team provides [Docling Serve](https://github.com/docling-project/docling-serve) - a REST API wrapper that we can call from C#. This is the cleanest integration path.

### Starting the Service

The easiest way to run Docling is via Docker:

```bash
docker run -p 5001:5001 quay.io/docling-project/docling-serve
```

That's it. You now have a document conversion API running at `http://localhost:5001`.

Want to play with it interactively? Enable the UI:

```bash
docker run -p 5001:5001 -e DOCLING_SERVE_ENABLE_UI=1 quay.io/docling-project/docling-serve
```

Then visit `http://localhost:5001/ui` to drag-and-drop documents and see the results.

### Choosing the Right Image

Docling offers several container images depending on your hardware:

| Image | Use Case | Size |
|-------|----------|------|
| `docling-serve` | General purpose, works everywhere | ~8.7GB |
| `docling-serve-cpu` | Smaller, CPU-only | ~4.4GB |
| `docling-serve-cu126` | NVIDIA GPU with CUDA 12.6 | ~10GB |
| `docling-serve-cu128` | NVIDIA GPU with CUDA 12.8 | ~11.4GB |

If you're doing heavy OCR work (scanned documents), the GPU images are worth the extra size. For digital-native PDFs and Word docs, CPU is fine.

### Adding to Docker Compose

For production, add Docling to your existing stack:

```yaml
services:
  docling:
    image: quay.io/docling-project/docling-serve:latest
    ports:
      - "5001:5001"
    volumes:
      - docling_cache:/root/.cache
    restart: unless-stopped
```

The cache volume is important - Docling downloads ML models on first run, and you don't want to re-download them every container restart.

## The Docling API

Before diving into C# code, let's understand what we're working with. Docling Serve exposes a simple REST API.

### Converting a Document from URL

```bash
curl -X POST 'http://localhost:5001/v1/convert/source' \
  -H 'Content-Type: application/json' \
  -d '{
    "sources": [{"kind": "http", "url": "https://example.com/contract.pdf"}],
    "options": {"to_formats": ["md"]}
  }'
```

### Uploading a Local File

```bash
curl -X POST 'http://localhost:5001/v1/convert/file' \
  -F 'files=@contract.pdf'
```

Both return JSON containing the converted markdown, plus metadata like page count and detected file type.

### Key Options

- **`to_formats`**: What output you want - `md` (markdown), `json` (structured), or `text` (plain)
- **`ocr`**: Enable/disable OCR (default: enabled)
- **`table_mode`**: `fast` or `accurate` - trade speed for table quality

## Integrating with C#

Since Docling is a REST service, integration is straightforward HTTP calls. Here's the approach:

### 1. Create a Docling Client

We need a service that handles the HTTP communication:

```csharp
public class DoclingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public DoclingClient(HttpClient httpClient, string baseUrl = "http://localhost:5001")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<string?> ConvertFromUrlAsync(string documentUrl)
    {
        var request = new
        {
            sources = new[] { new { kind = "http", url = documentUrl } },
            options = new { to_formats = new[] { "md" } }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/v1/convert/source", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<DoclingResponse>();
        return result?.Document?.MarkdownContent;
    }

    public async Task<string?> ConvertFileAsync(string filePath)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);
        content.Add(new StreamContent(fileStream), "files", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/v1/convert/file", content);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<DoclingResponse>();
        return result?.Document?.MarkdownContent;
    }
}
```

The key insight: we get markdown back, which slots directly into our existing pipeline.

### 2. Feed into the Existing Pipeline

Remember our ingestion pipeline from Part 4? It takes markdown, chunks it, generates embeddings, and stores in Qdrant. We just need to add Docling as a new input source:

```csharp
public async Task ProcessDocumentAsync(string filePath)
{
    // Step 1: Convert to markdown with Docling
    var markdown = await _doclingClient.ConvertFileAsync(filePath);
    
    if (string.IsNullOrEmpty(markdown))
    {
        _logger.LogError("Failed to convert {File}", filePath);
        return;
    }

    // Step 2: Parse the markdown (reuse existing code!)
    var document = _markdownParser.ParseFromContent(markdown, Path.GetFileName(filePath));

    // Step 3: Chunk it (reuse existing code!)
    var chunks = _chunker.ChunkDocument(document);

    // Step 4: Generate embeddings (reuse existing code!)
    await _embedder.GenerateEmbeddingsAsync(chunks);

    // Step 5: Store in vector database (reuse existing code!)
    await _vectorStore.UpsertChunksAsync(chunks);
}
```

See how little new code we need? The beauty of building a pipeline around markdown is that any new input format just needs a converter - everything downstream stays the same.

### 3. Handle Different Document Types

Different documents might need different treatment:

```csharp
public async Task ProcessDocumentAsync(string filePath, string[] categories)
{
    var extension = Path.GetExtension(filePath).ToLower();
    
    // Scanned PDFs need OCR, which is slower
    var useOcr = extension == ".pdf" && await IsScannedPdfAsync(filePath);
    
    var markdown = await _doclingClient.ConvertFileAsync(filePath, new ConvertOptions
    {
        EnableOcr = useOcr,
        TableMode = extension == ".xlsx" ? "accurate" : "fast"
    });

    // ... rest of pipeline
}
```

## Performance Considerations

Document conversion isn't instant. Here's what to expect:

| Document Type | Typical Time |
|--------------|--------------|
| Simple PDF (1-5 pages) | 2-5 seconds |
| Complex PDF (20+ pages) | 10-30 seconds |
| Scanned PDF with OCR | 30-120 seconds |
| Word document | 1-3 seconds |
| PowerPoint | 5-30 seconds |

**Tips for production:**

1. **Process asynchronously** - Don't block on document conversion. Queue documents and process in the background.

2. **Cache results** - Store the converted markdown. If a document hasn't changed (check file hash), don't reconvert.

3. **Use GPU for OCR** - If you're processing lots of scanned documents, the CUDA images are 5-10x faster.

4. **Skip OCR when possible** - Digital-native PDFs don't need OCR. Docling auto-detects, but you can force it off for known digital sources.

## Automatic Document Watching

For a hands-off experience, watch a folder for new documents:

```csharp
public class DocumentWatcher : BackgroundService
{
    private FileSystemWatcher _watcher;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _watcher = new FileSystemWatcher("/documents/incoming")
        {
            Filters = { "*.pdf", "*.docx", "*.pptx" }
        };

        _watcher.Created += async (s, e) => 
        {
            await Task.Delay(1000); // Wait for file to finish writing
            await _ingestionService.ProcessDocumentAsync(e.FullPath);
        };

        _watcher.EnableRaisingEvents = true;
        return Task.CompletedTask;
    }
}
```

Drop a PDF in the folder, and it automatically gets converted, chunked, embedded, and made searchable. 

## What This Enables

With Docling integrated, our Lawyer GPT can now:

- **Ingest client contracts** - Upload a PDF, ask questions about specific clauses
- **Search across briefs** - Find that argument you made in a similar case
- **Reference research papers** - Add academic PDFs to your knowledge base
- **Process legacy documents** - OCR those old scanned records

The key insight is that document format is just an input problem. Once everything is markdown, our semantic search doesn't care where the content came from.

## Summary

We've added document ingestion to our Lawyer GPT:

1. **Docling** converts PDFs, DOCX, PPTX to clean markdown
2. **Docling Serve** wraps it in a REST API we can call from C#
3. **Our existing pipeline** handles everything from there - no changes needed

This is the power of building around a common format. New input types are easy to add; the core intelligence stays the same.

## Series Navigation

- [Part 1: Introduction & Architecture](/blog/building-a-lawyer-gpt-for-your-blog-part1)
- [Part 2: GPU Setup & CUDA in C#](/blog/building-a-lawyer-gpt-for-your-blog-part2)
- [Part 3: Understanding Embeddings & Vector Databases](/blog/building-a-lawyer-gpt-for-your-blog-part3)
- [Part 4: Building the Ingestion Pipeline](/blog/building-a-lawyer-gpt-for-your-blog-part4)
- [Part 5: The Windows Client](/blog/building-a-lawyer-gpt-for-your-blog-part5)
- [Part 6: Local LLM Integration](/blog/building-a-lawyer-gpt-for-your-blog-part6)
- [Part 7: Content Generation & Prompt Engineering](/blog/building-a-lawyer-gpt-for-your-blog-part7)
- [Part 8: Advanced Features & Production Deployment](/blog/building-a-lawyer-gpt-for-your-blog-part8)
- **Part 9: Document Ingestion with Docling** (this post)

## Resources

- [Docling GitHub](https://github.com/docling-project/docling)
- [Docling Serve GitHub](https://github.com/docling-project/docling-serve)
- [Docling Paper (arXiv)](https://arxiv.org/abs/2501.17887)
- [API Documentation](http://localhost:5001/docs) (when running locally)
