# Mostlylucid.DocSummarizer

Local-first document summarization library using BERT embeddings, RAG retrieval, and optional LLM synthesis.

## Features

- **Local-first**: Runs entirely offline using ONNX models - no API keys required
- **Citation grounding**: Every claim is traceable to source segments
- **Multiple modes**: Pure BERT extraction, hybrid BERT+LLM, full RAG pipeline
- **Format support**: Markdown, PDF, DOCX, HTML, URLs
- **Vector storage**: In-memory, DuckDB (embedded), or Qdrant (external)

## Quick Start

```csharp
// Register in DI
builder.Services.AddDocSummarizer();

// Inject and use
public class MyService(IDocumentSummarizer summarizer)
{
    public async Task<string> GetSummaryAsync(string markdown)
    {
        var result = await summarizer.SummarizeMarkdownAsync(markdown);
        return result.ExecutiveSummary;
    }
}
```

## Configuration

### Basic Configuration

```csharp
builder.Services.AddDocSummarizer(options =>
{
    // Use local ONNX embeddings (default, no external services)
    options.EmbeddingBackend = EmbeddingBackend.Onnx;
    
    // Or use Ollama for embeddings
    options.EmbeddingBackend = EmbeddingBackend.Ollama;
    options.Ollama.BaseUrl = "http://localhost:11434";
    options.Ollama.EmbedModel = "nomic-embed-text";
});
```

### From Configuration File

```json
{
  "DocSummarizer": {
    "EmbeddingBackend": "Onnx",
    "BertRag": {
      "VectorStore": "DuckDB",
      "ReindexOnStartup": false,
      "CollectionName": "my-documents"
    },
    "Onnx": {
      "ModelName": "all-MiniLM-L6-v2"
    }
  }
}
```

```csharp
builder.Services.AddDocSummarizer(
    builder.Configuration.GetSection("DocSummarizer"));
```

## Summarization Modes

| Mode | LLM Required | Best For |
|------|-------------|----------|
| `Bert` | No | Fast extraction, offline use |
| `BertHybrid` | Yes | Balance of speed and fluency |
| `BertRag` | Yes | Production systems, large documents |
| `Auto` | Varies | Automatic mode selection |

```csharp
// Pure BERT - no LLM needed, fastest
var summary = await summarizer.SummarizeMarkdownAsync(
    markdown, 
    mode: SummarizationMode.Bert);

// BertRag - full pipeline with LLM synthesis
var summary = await summarizer.SummarizeMarkdownAsync(
    markdown,
    focusQuery: "What are the key architectural decisions?",
    mode: SummarizationMode.BertRag);
```

## Key Configuration Options

### `BertRag.ReindexOnStartup`

Controls whether to clear and rebuild the vector index on application startup.

```csharp
options.BertRag.ReindexOnStartup = true;  // Development (default)
options.BertRag.ReindexOnStartup = false; // Production
```

When `true`:
- All existing embeddings are deleted on startup
- Documents are re-indexed on first access
- Useful when embedding models or extraction logic changes

When `false`:
- Existing embeddings are preserved
- Faster startup
- Only new/changed documents are re-indexed

### Vector Store Backends

```csharp
// In-memory (no persistence, fastest)
options.BertRag.VectorStore = VectorStoreBackend.InMemory;

// DuckDB (embedded file-based, default)
options.BertRag.VectorStore = VectorStoreBackend.DuckDB;

// Qdrant (external server, best for production)
options.BertRag.VectorStore = VectorStoreBackend.Qdrant;
options.Qdrant.Host = "localhost";
options.Qdrant.Port = 6334;
```

## Output Models

### DocumentSummary

```csharp
record DocumentSummary(
    string ExecutiveSummary,           // Main summary text
    List<TopicSummary> TopicSummaries, // Topic-by-topic breakdown
    List<string> OpenQuestions,        // Questions that couldn't be answered
    SummarizationTrace Trace,          // Processing metadata
    ExtractedEntities? Entities);      // Named entities (people, places, etc.)
```

### TopicSummary

```csharp
record TopicSummary(
    string Topic,              // Topic name
    string Summary,            // Summary for this topic
    List<string> SourceChunks); // Citations to source segments
```

## Query Mode

Ask questions about documents with evidence-grounded answers:

```csharp
var answer = await summarizer.QueryAsync(
    markdown: documentContent,
    question: "What database technology is recommended?");

Console.WriteLine(answer.Answer);
Console.WriteLine($"Confidence: {answer.Confidence}");

foreach (var evidence in answer.Evidence)
{
    Console.WriteLine($"  [{evidence.SegmentId}] {evidence.Text}");
}
```

## Segment Extraction

Extract segments without summarizing - useful for building search indexes:

```csharp
var extraction = await summarizer.ExtractSegmentsAsync(markdown);

foreach (var segment in extraction.TopBySalience)
{
    Console.WriteLine($"[{segment.Type}] {segment.Text}");
    Console.WriteLine($"  Salience: {segment.SalienceScore:F2}");
}
```

## Dependencies

- **Required**: .NET 9.0+
- **Included**: ONNX Runtime, Markdig, PdfPig, OpenXml, AngleSharp
- **Optional**: Ollama (for LLM synthesis), Docling (for complex PDF conversion)

## License

MIT
