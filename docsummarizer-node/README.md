# @mostlylucid/docsummarizer

A local-first RAG engine for documents: semantic segmentation, embeddings, salience-aware retrieval, and citation-grounded Q&A — without requiring cloud APIs.

```typescript
import { DocSummarizer } from "@mostlylucid/docsummarizer";

const doc = new DocSummarizer();
const { summary } = await doc.summarizeFile("report.pdf");
const { answer, evidence } = await doc.askFile("report.pdf", "What are the key findings?");
```

## Install

```bash
npm install @mostlylucid/docsummarizer
```

The CLI binary is automatically downloaded during installation. Verify it works:

```bash
npx @mostlylucid/docsummarizer check
```

## What It Does

- **RAG Q&A** — Ask questions with source citations (single doc or folders)
- **Salience-aware retrieval** — Better chunks, less noise
- **Deterministic ingestion** — Same document = same segments = reproducible results
- **Local-first embeddings** — ONNX runtime, no API keys required
- **Composable storage** — Built-in stores or export embeddings to your own

## How It Works

1. **Segment** — Splits documents into semantic units (sentences, headings, lists, code blocks)
2. **Embed** — Generates 384-dim vectors using BERT (runs locally via ONNX)
3. **Score** — Computes salience scores based on position, structure, and content
4. **Retrieve** — Finds relevant segments using cosine similarity
5. **Synthesize** — Optionally uses an LLM to generate coherent answers

No API keys required for basic usage. Add [Ollama](https://ollama.com) for LLM-enhanced synthesis.

---

## API

### Summarize

```typescript
// File (supports .md, .pdf, .docx, .txt)
const result = await doc.summarizeFile("./document.md");

// URL
const result = await doc.summarizeUrl("https://example.com/article");

// Raw markdown
const result = await doc.summarizeMarkdown("# My Doc\n\nContent here...");

// With options
const result = await doc.summarizeFile("./doc.md", {
  query: "focus on security concerns",  // Focus summary on specific topic
  mode: "BertRag",                       // Summarization mode
});

console.log(result.summary);      // The summary text
console.log(result.wordCount);    // Word count
console.log(result.topics);       // Extracted topics with sources
```

### Question Answering

```typescript
const result = await doc.askFile("./contract.pdf", "What are the payment terms?");

console.log(result.answer);       // The answer
console.log(result.confidence);   // "High" | "Medium" | "Low"
console.log(result.evidence);     // Source segments with similarity scores
```

### Semantic Search

Search finds relevant segments within a single document (not a global index).

```typescript
const result = await doc.search("./document.md", "machine learning", {
  topK: 5,  // Max results (default: 10)
});

result.results.forEach(r => {
  console.log(`[${r.score.toFixed(2)}] ${r.section}: ${r.preview}`);
});
```

### Diagnostics

```typescript
// Quick check
const ok = await doc.check();

// Detailed diagnostics
const info = await doc.diagnose();
console.log(info.available);       // true/false
console.log(info.executablePath);  // Resolved CLI path
console.log(info.output);          // Raw diagnostic output
```

---

## CLI Usage

The package includes a CLI passthrough:

```bash
# Check installation
npx @mostlylucid/docsummarizer check

# Run diagnostics
npx @mostlylucid/docsummarizer doctor

# Summarize (JSON output)
npx @mostlylucid/docsummarizer tool --file doc.md

# Search
npx @mostlylucid/docsummarizer search --file doc.md --query "topic" --json

# All CLI commands
npx @mostlylucid/docsummarizer --help
```

---

## Modes

| Mode | Description | Requires LLM |
|------|-------------|--------------|
| `Auto` | Auto-select based on document | Maybe |
| `BertRag` | BERT embeddings + retrieval | No |
| `Bert` | Pure extractive (BERT only) | No |
| `BertHybrid` | BERT + LLM synthesis | Yes |
| `Rag` | Full RAG pipeline | Yes |

```typescript
// No LLM needed
await doc.summarizeFile("doc.md", { mode: "BertRag" });

// LLM-enhanced (requires Ollama)
await doc.summarizeFile("doc.md", { mode: "BertHybrid" });
```

---

## Configuration

### Constructor Options

```typescript
const doc = new DocSummarizer({
  executable: "/path/to/docsummarizer",  // Custom CLI path
  configPath: "./config.json",            // Config file
  model: "llama3.2",                      // Ollama model
  timeout: 300000,                        // Timeout (ms)
});
```

### Config File

Create `docsummarizer.json`:

```json
{
  "ollama": {
    "baseUrl": "http://localhost:11434",
    "model": "llama3.2"
  },
  "onnx": {
    "embeddingModel": "AllMiniLmL6V2"
  }
}
```

### Environment Variables

```bash
DOCSUMMARIZER_PATH=/path/to/cli         # Direct path to CLI
DOCSUMMARIZER_PROJECT=/path/to/proj.csproj  # Use dotnet run (dev mode)
```

---

## Response Types

### SummaryResult

```typescript
interface SummaryResult {
  schemaVersion: string;  // "1.0.0"
  success: true;
  source: string;
  type: "summary";
  summary: string;
  wordCount: number;
  topics: Array<{
    topic: string;
    summary: string;
    sourceChunks: string[];
  }>;
  entities?: {
    people?: string[];
    organizations?: string[];
    locations?: string[];
    dates?: string[];
  };
  metadata: {
    documentId: string;
    totalChunks: number;
    chunksProcessed: number;
    coverageScore: number;   // 0-1
    processingTimeMs: number;
    mode: string;
    model: string;
  };
}
```

### QAResult

```typescript
interface QAResult {
  schemaVersion: string;
  success: true;
  source: string;
  type: "qa";
  question: string;
  answer: string;
  confidence: "High" | "Medium" | "Low";
  evidence: Array<{
    segmentId: string;
    text: string;
    similarity: number;  // 0-1
    section: string;
  }>;
  metadata: {
    processingTimeMs: number;
    model: string;
  };
}
```

### SearchResult

```typescript
interface SearchResult {
  schemaVersion: string;
  query: string;
  results: Array<{
    section: string;
    score: number;    // 0-1 (cosine similarity)
    preview: string;
  }>;
}
```

---

## Error Handling

```typescript
import { DocSummarizer, DocSummarizerError } from "@mostlylucid/docsummarizer";

try {
  await doc.summarizeFile("missing.md");
} catch (err) {
  if (err instanceof DocSummarizerError) {
    console.error(err.message);  // Error description
    console.error(err.output);   // Raw CLI output
  }
}
```

---

## Events

```typescript
doc.on("stderr", (data: string) => {
  console.log("Progress:", data);
});
```

---

## Requirements

- **Node.js** 18+
- **.NET** 8+ runtime
- **Ollama** (optional, for LLM modes)

---

## Troubleshooting

### CLI not found

```bash
# Run diagnostics
npx @mostlylucid/docsummarizer doctor

# Re-run postinstall to download CLI
node node_modules/@mostlylucid/docsummarizer/scripts/postinstall.js

# Verify
npx @mostlylucid/docsummarizer check
```

### PDF/DOCX not working

PDF and DOCX conversion requires the Docling service. See [CLI docs](https://www.mostlylucid.net/blog/docsummarizer-tool).

### Slow first run

First run downloads ONNX models (~50MB). Subsequent runs are fast.

---

---

## Why This Isn't a RAG Framework

- **No agent orchestration** — You control the flow
- **No opinionated prompts** — Bring your own LLM prompts
- **No cloud dependency** — Runs entirely local

## Why It Still Is RAG

This is a RAG *engine*, not a RAG *framework*. It handles:

- Semantic chunking with structure preservation
- Vector embeddings (local ONNX)
- Similarity-based retrieval with salience scoring
- Citation-grounded answers

What it doesn't handle (by design): conversational memory, agent loops, eval dashboards. Those are layers you add if you need them.

---

## Links

- [NuGet Package](https://www.nuget.org/packages/Mostlylucid.DocSummarizer)
- [CLI Documentation](https://www.mostlylucid.net/blog/docsummarizer-tool)
- [GitHub](https://github.com/scottgal/mostlylucidweb)

## License

MIT
