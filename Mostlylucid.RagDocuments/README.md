# lucidRAG

A standalone multi-document RAG (Retrieval-Augmented Generation) web application with GraphRAG entity extraction and knowledge graph visualization.

**Website:** [lucidrag.com](https://lucidrag.com)

## Features

- **Multi-Document Upload**: Support for PDF, DOCX, Markdown, TXT, and HTML files
- **Agentic RAG**: Deterministic query planning with bounded LLM steps (decomposition → retrieval → synthesis), including clarification loops when confidence is low
- **GraphRAG Entity Extraction**: Automatic extraction of entities and relationships using IDF-based heuristics and BERT embeddings
- **Knowledge Graph Visualization**: Interactive exploration with depth-limited subgraphs (max 2 hops, entity-type filtering) to prevent visual overload on large corpora
- **Evidence View**: Sentence-level grounding showing exactly which parts of source documents support each answer
- **Conversation Memory**: Chat sessions maintain context across multiple questions
- **Standalone Deployment**: Single executable with SQLite for portable use, or PostgreSQL for production

**What the LLM does NOT do**: The LLM is never used for entity extraction, indexing, or storage — only for reasoning over retrieved, evidence-backed context. All preprocessing is deterministic and inspectable.

## Quick Start

### Standalone Mode (No Dependencies)

```bash
dotnet run --project Mostlylucid.RagDocuments -- --standalone
```

This starts the app on `http://localhost:5080` with:
- SQLite database (stored in `data/ragdocs.db`)
- DuckDB vector store (stored in `data/`)
- Local file uploads (stored in `uploads/`)

### Docker Deployment

```bash
# Full production deployment (PostgreSQL, Qdrant, Docling)
docker-compose -f docker-compose.production.yml up -d
```

This starts:
- **LucidRAG** on `http://localhost:5080`
- **PostgreSQL** for metadata storage
- **Qdrant** for persistent vector storage
- **Docling** for PDF/DOCX conversion

**Note:** Ollama is NOT included in the compose file. Install it on your host machine:
```bash
# Install from https://ollama.ai, then:
ollama pull llama3.2:3b
ollama serve
```

LucidRAG will connect to Ollama at `host.docker.internal:11434`.

### Standalone vs Production

| Mode | Intended Use |
|------|--------------|
| Standalone | Local research, audits, air-gapped environments, single-user |
| PostgreSQL | Multi-user, long-lived corpora, production workloads |

## Design Principles

1. **Deterministic preprocessing** — Chunking, embedding, and entity extraction use fixed algorithms, not LLM calls. Results are reproducible.

2. **Evidence-first** — Every answer cites specific source segments. No hallucinated claims.

3. **Inspectable pipelines** — All intermediate state (chunks, embeddings, entities, relationships) is queryable and debuggable.

4. **Local-first execution** — ONNX embeddings, DuckDB storage, optional Ollama. No mandatory cloud dependencies.

5. **Bounded LLM usage** — The LLM synthesizes answers from retrieved context. It doesn't index, extract, or store anything.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        RagDocuments UI                          │
│                    (HTMX + Alpine.js + TailwindCSS)            │
├─────────────────────────────────────────────────────────────────┤
│                         REST API                                │
│   /api/documents  /api/chat  /api/graph  /api/collections      │
├─────────────────┬───────────────────┬───────────────────────────┤
│   DocSummarizer │   EntityGraph     │   ConversationService    │
│   (RAG Engine)  │   (GraphRAG)      │   (Chat Memory)          │
├─────────────────┼───────────────────┼───────────────────────────┤
│   DuckDB        │   DuckDB          │   PostgreSQL/SQLite      │
│   (Vectors)     │   (Entity Graph)  │   (Metadata)             │
└─────────────────┴───────────────────┴───────────────────────────┘
```

**Why DuckDB?** DuckDB is used for vector storage to keep indexing local, fast, and inspectable without introducing an external vector database dependency. It's ephemeral by design — you can always rebuild it from source documents.

### Key Components

| Component | Description |
|-----------|-------------|
| `DocumentProcessingService` | Handles file upload, validation, and queue management |
| `DocumentQueueProcessor` | Background service that processes documents through DocSummarizer |
| `EntityGraphService` | Extracts entities using GraphRag's IDF + BERT heuristics |
| `AgenticSearchService` | Multi-step RAG with query decomposition and self-correction |
| `ConversationService` | Manages chat sessions with memory and context |

## API Endpoints

### Documents

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/documents/upload` | Upload a single document |
| POST | `/api/documents/upload-batch` | Upload multiple documents |
| GET | `/api/documents` | List all documents |
| GET | `/api/documents/{id}` | Get document details |
| GET | `/api/documents/{id}/status` | SSE stream of processing progress |
| DELETE | `/api/documents/{id}` | Delete a document (with vector cleanup) |
| GET | `/api/documents/demo-status` | Check if demo mode is enabled |

### Search (Standalone)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/search` | Hybrid search (BM25 + BERT), returns segments |
| POST | `/api/search/answer` | Search with LLM-synthesized answer (stateless) |

### Chat (Conversational)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/chat` | Send a message (creates new conversation) |
| POST | `/api/chat/stream` | Stream response via SSE |
| GET | `/api/chat/conversations` | List all conversations |
| GET | `/api/chat/conversations/{id}` | Get conversation history |
| DELETE | `/api/chat/conversations/{id}` | Delete conversation |

### Graph

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/graph` | Get full graph data (D3.js format) |
| GET | `/api/graph/stats` | Get graph statistics |
| GET | `/api/graph/subgraph/{entityId}` | Get entity-centered subgraph (max 2 hops) |
| GET | `/api/graph/entities` | Search entities by name/type |
| GET | `/api/graph/entities/{id}` | Get entity details with relationships |
| GET | `/api/graph/paths` | Find paths between two entities |

### Collections

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/collections` | List collections with stats |
| POST | `/api/collections` | Create collection |
| GET | `/api/collections/{id}` | Get collection with documents |
| PUT | `/api/collections/{id}` | Update collection name/description/settings |
| DELETE | `/api/collections/{id}` | Delete collection (cascades to documents) |
| POST | `/api/collections/{id}/documents` | Add documents to collection |
| DELETE | `/api/collections/{id}/documents` | Remove documents from collection |

### Config (Capabilities & Modes)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/config/capabilities` | Get detected services and available features |
| GET | `/api/config/extraction-modes` | Get available extraction modes for UI dropdown |
| PUT | `/api/config/extraction-mode` | Set extraction mode (Heuristic/Hybrid/LLM) |

**Extraction Modes:**
- **Heuristic** (default): Fast, no LLM calls - uses IDF + structural signals
- **Hybrid**: Heuristic candidates + LLM enhancement per document
- **LLM**: Full MSFT GraphRAG style - 2 LLM calls per chunk (requires Ollama)

## Configuration

### appsettings.json

```json
{
  "RagDocuments": {
    "UploadPath": "./uploads",
    "MaxFileSizeMB": 100,
    "AllowedExtensions": [".pdf", ".docx", ".md", ".txt", ".html"],
    "ExtractionMode": "Heuristic"  // Heuristic, Hybrid, or Llm
  },
  "DocSummarizer": {
    "EmbeddingBackend": "Onnx",
    "BertRag": {
      "VectorStore": "DuckDB",
      "PersistVectors": true
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.2:3b"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ragdocs;Username=postgres;Password=..."
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `DocSummarizer__Ollama__BaseUrl` | Ollama API URL |
| `DocSummarizer__Ollama__Model` | LLM model to use |

## Document Processing Pipeline

1. **Upload**: File is validated, hashed, and stored
2. **Queue**: Document is added to background processing queue
3. **Chunking**: DocSummarizer splits document into semantic segments
4. **Embedding**: ONNX BERT model generates embeddings for each segment
5. **Indexing**: Segments stored in DuckDB with HNSW vector index
6. **Entity Extraction**: GraphRag extracts entities using:
   - IDF-based term importance (rare terms = likely entities)
   - Structural signals (headings, code blocks, links)
   - BERT embedding deduplication
   - Co-occurrence relationship detection

## Entity Extraction

The GraphRAG integration uses a hybrid approach:

1. **Heuristic Candidate Detection**: Fast, deterministic extraction using:
   - IDF scores (terms rare across corpus)
   - Markdown structure (headings, inline code)
   - Link text and targets
   - PascalCase identifiers

2. **BERT Deduplication**: Merges similar entities using embedding similarity

3. **Relationship Building**:
   - Co-occurrence (entities in same segment)
   - Explicit links (markdown links between documents)
   - Structural hierarchy (heading → content relationships)

## UI Features

### Chat Interface
- Real-time streaming responses
- Source citations with confidence scores
- Three view modes: Answer, Evidence, Graph

### Evidence View
- Side-by-side answer and sources
- Sentence-level highlighting
- Click to expand source context

### Graph View
- D3.js force-directed visualization
- Color-coded entity types
- Interactive exploration

## Development

### Prerequisites

- .NET 9.0 SDK
- Node.js 18+ (for frontend build)
- PostgreSQL 16 (optional, SQLite works for development)
- Ollama (optional, for LLM features)

### Build

```bash
# Backend
dotnet build

# Frontend (TailwindCSS + Alpine.js)
cd Mostlylucid.RagDocuments
npm install
npm run build
```

### Run Tests

```bash
dotnet test Mostlylucid.RagDocuments.Tests
```

### Puppeteer UI Tests

```bash
cd Mostlylucid.RagDocuments
node puppeteer-screenshot.js
```

## Publishing

### Single Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Supported runtimes: `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`

### Docker

```bash
docker build -t ragdocuments .
```

## Project Structure

```
Mostlylucid.RagDocuments/
├── Controllers/
│   ├── Api/                    # REST API controllers
│   │   ├── DocumentsController.cs
│   │   ├── ChatController.cs
│   │   ├── GraphController.cs
│   │   └── CollectionsController.cs
│   └── UI/
│       └── HomeController.cs   # Main UI controller
├── Services/
│   ├── DocumentProcessingService.cs
│   ├── ConversationService.cs
│   ├── AgenticSearchService.cs
│   ├── EntityGraphService.cs   # GraphRag integration
│   └── Background/
│       └── DocumentQueueProcessor.cs
├── Data/
│   └── RagDocumentsDbContext.cs
├── Entities/                   # EF Core entities
├── Views/                      # Razor views + HTMX
├── wwwroot/
│   ├── css/
│   └── js/
├── Program.cs
├── appsettings.json
├── Dockerfile
└── docker-compose.production.yml
```

## Dependencies

| Package | Purpose |
|---------|---------|
| Mostlylucid.DocSummarizer.Core | RAG pipeline, embeddings, vector store |
| Mostlylucid.GraphRag | Entity extraction, knowledge graph |
| Entity Framework Core | Database access (PostgreSQL/SQLite) |
| Serilog | Structured logging |
| HTMX | Server-driven UI interactions |
| Alpine.js | Lightweight reactive UI |
| TailwindCSS + DaisyUI | Styling |

## License

MIT License - See LICENSE file for details.
