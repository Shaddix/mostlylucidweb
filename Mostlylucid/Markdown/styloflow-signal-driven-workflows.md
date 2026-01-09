# StyloFlow: Signal-Driven Workflow Orchestration

<!--category-- Architecture, AI, Workflows, C#, Signals, RAG -->
<datetime class="hidden">2026-01-11T14:00</datetime>

I built StyloFlow because I kept writing the same pattern over and over: components that react to what happened before, emit confidence scores, and sometimes need to escalate to more expensive analysis. Existing workflow engines wanted me to think in terms of DAGs or state machines. I wanted to think in signals.

**StyloFlow is a signal-driven orchestration library that matches [how I think](https://www.mostlylucid.net/blog/thinking-in-systems) about[ AI pipelines](https://www.mostlylucid.net/blog/tencommandments):** components declare what they produce and what they need, confidence scores guide execution, and cheap operations run first with escalation to expensive ones only when needed.

This is the infrastructure powering **[*lucid*RAG](https://www.mostlylucid.net/blog/lucidrag-multi-document-rag-web-app)** - a cross-modal graph RAG tool that combines [DocSummarizer](/blog/building-a-document-summarizer-with-rag) (documents), [DataSummarizer](/blog/datasummarizer-how-it-works) (structured data), and [ImageSummarizer](/blog/constrained-fuzzy-image-intelligence) (images) into a unified question-answering system with knowledge graph visualization. It also powers [Stylobot](https://www.stylobot.net) (an advanced bot protection system) and implements the [Reduced RAG](/blog/reduced-rag) pattern.

![lucidRAG Interface](lucidrag_proto.png?width=500)

**Source:** [GitHub - StyloFlow](https://github.com/scottgal/styloflow)

> NOTE: StyloFlow is not YET a finished product; as I build lucidRAG and StyloBot I'm adding missing features and polishing the API. It's still in active development, but you can try it out and provide feedback.

[TOC]

---

## What This Is

**StyloFlow is a working prototype of a signal-driven orchestration model.** The API and shape will evolve as I build lucidRAG and Stylobot, but the execution semantics and patterns described here are the point: signals as first-class facts, confidence-driven branching, and escalation as a structural pattern.

This isn't a new DSL or workflow language. It's a set of **execution semantics** built around signals, confidence, and bounded escalation. Today it runs in-process with bounded concurrency. Tomorrow it will distribute lanes across machines while keeping signals as the stable boundary.

---

## The Problem with Traditional Workflows

Here's what most workflow engines look like:

```csharp
// ❌ Traditional: Hardcoded dependencies
public async Task ProcessDocumentAsync(string path)
{
    var text = await ExtractTextAsync(path);
    var chunks = await ChunkTextAsync(text);
    var embeddings = await GenerateEmbeddingsAsync(chunks);
    var entities = await ExtractEntitiesAsync(chunks);
    await StoreEverythingAsync(embeddings, entities);
}
```

This works until:
- You want to skip entity extraction for simple queries
- You need to run extraction and embedding in parallel
- You want to escalate to a better model based on confidence
- You need to add a new processing stage without touching existing code

You end up with either:
1. **Rigid pipelines** that can't adapt
2. **Massive if/else** trees for routing
3. **God classes** that know about everything

## The Foundation: Ephemeral Execution

StyloFlow builds on [mostlylucid.ephemeral](/blog/ephemeral-execution-library) - a library for bounded, trackable async execution.

Quick recap of what ephemeral provides:

```csharp
// Bounded concurrent processing with full visibility
var coordinator = new EphemeralWorkCoordinator<DocumentJob>(
    async (job, operation, ct) => {
        await ProcessAsync(job, ct);
        operation.Signal("document.processed");
    },
    new EphemeralOptions { MaxConcurrency = 4 });

// Enqueue work
await coordinator.EnqueueAsync(new DocumentJob(filePath));

// Full observability
Console.WriteLine($"Active: {coordinator.ActiveCount}");
Console.WriteLine($"Completed: {coordinator.TotalCompleted}");
```

**Key benefits from ephemeral:**
- Bounded concurrency (no runaway memory)
- [LRU eviction](/blog/learning-lrus-when-capacity-makes-systems-better) of old operations
- Signal publishing for cross-component coordination
- Operation pinning to prevent premature eviction

For details, see [Fire and Don't Quite Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution).

---

## What This Model Enables

This orchestration model extends ephemeral with:

1. **YAML-driven component manifests** - Declarative configuration
2. **Signal-based triggers** - Components run when signals appear
3. **Wave coordination** - Priority-based execution with concurrency lanes
4. **Escalation patterns** - Defer expensive analysis until needed
5. **Entity contracts** - Type-safe input/output specifications
6. **Budget management** - Token limits, cost caps, timeouts

Here's the key architectural shift:

```mermaid
graph TD
    subgraph Traditional["❌ Traditional: Hardcoded"]
        T1[Component A] -->|calls| T2[Component B]
        T2 -->|calls| T3[Component C]
        T3 -->|calls| T4[Component D]
    end

    subgraph StyloFlow["✅ StyloFlow: Signal-Driven"]
        S1[Component A]
        S2[Component B]
        S3[Component C]
        S4[Component D]
        SS[Signal Sink]

        S1 -.emits.-> SS
        S2 -.emits.-> SS
        S3 -.emits.-> SS
        SS -.triggers.-> S2
        SS -.triggers.-> S3
        SS -.triggers.-> S4
    end

    style T1 stroke:#ff6b6b
    style T2 stroke:#ff6b6b
    style T3 stroke:#ff6b6b
    style T4 stroke:#ff6b6b
    style S1 stroke:#51cf66
    style S2 stroke:#51cf66
    style S3 stroke:#51cf66
    style S4 stroke:#51cf66
    style SS stroke:#339af0
```

Components never call each other. They emit signals and react to signals.

---

## Core Concept: Signals and Ownership

**Signals are facts about what happened**, not commands or events. They're immutable, timestamped, and carry confidence scores. Each atom owns its signals - they're externally immutable. Nothing else can modify that list.

```csharp
public record Signal
{
    public required string Key { get; init; }           // "document.chunked"
    public object? Value { get; init; }                 // Optional payload
    public double Confidence { get; init; } = 1.0;      // 0.0 to 1.0
    public required string Source { get; init; }        // Which component
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

**Critical architectural point: SignalSink is NOT an event bus.**

A SignalSink is a **read-only view** across atoms in one or more coordinators. It doesn't publish or broadcast - it's a query interface. Components don't "subscribe" to signals; they READ signals from operations to decide if they should run.

```csharp
// Each operation owns its signals
var operation = coordinator.GetOperation(opId);
var signals = operation.GetSignals();  // Read-only view

// Signals can be escalated (copied to another operation)
operation.EscalateSignal("quality.low", targetOperationId);

// Or echoed (preserved when operation evicts)
operation.EmitEcho("final.state", value);

// But the original signal list is externally immutable
// Nothing can modify operation.GetSignals() from outside
```

**Why this matters:**

- **Ownership is clear** - Each atom owns its signals, period
- **No action at a distance** - Components can't modify each other's signals
- **Escalation is explicit** - When signals need to move, it's a deliberate copy
- **Observability without mutation** - You can read all signals without affecting them

Example coordination:

```csharp
// Wave checks if it should run by READING signals
public bool ShouldRun(string path, AnalysisContext ctx)
{
    var chunkSignal = ctx.GetSignal("document.chunked");
    return chunkSignal != null && (int)chunkSignal.Value > 0;
}

// It emits its own signals (adds to its owned list)
public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
{
    return new[]
    {
        new Signal
        {
            Key = "embeddings.generated",
            Confidence = 1.0,
            Source = Name
        }
    };
}
```

This structure exists because AI components naturally produce probabilistic outputs - the signal model makes confidence first-class while maintaining clear ownership boundaries.

For the theory behind this, see [Constrained Fuzzy Context Dragging](/blog/constrained-fuzzy-context-dragging).

---

## Core Concept: Component Manifests

**Manifests declare contracts (what triggers me, what I emit, what I cost)** separate from implementation. This separation exists so you can understand the workflow without reading code, and change execution order without recompiling.

```yaml
name: BotDetector
priority: 10              # Lower runs first
enabled: true

# What kind of component is this?
taxonomy:
  kind: analyzer          # sensor|analyzer|proposer|gatekeeper
  determinism: probabilistic
  persistence: ephemeral

# When should this run?
triggers:
  requires:
    - signal: http.request.received
      condition: exists

# What does it produce?
emits:
  on_complete:
    - key: bot.detected
      confidence_range: [0.0, 1.0]

  conditional:
    - key: bot.escalation.needed
      when: confidence < 0.7

# Resource limits
lane:
  name: fast              # fast|normal|slow|llm
  max_concurrency: 8

budget:
  max_duration: 100ms

# Configuration values
defaults:
  confidence:
    bot_detected: 0.6
  timing:
    timeout_ms: 100
```

**Benefits:**

1. **Runtime reconfiguration** - Change priority without recompiling
2. **Environment overrides** - Override via appsettings.json
3. **Clear contracts** - See what signals trigger what
4. **Self-documenting** - Manifest is the spec

### Visual Workflow Builder

While you can write YAML manifests by hand, StyloFlow includes a visual workflow builder that lets you design signal-driven workflows using modular-synth-style patching:

![StyloFlow Workflow Builder](styloflow_ui.png?width=800)

The UI provides:
- **Drag-and-drop components** from the taxonomy (sensors, analyzers, proposers, etc.)
- **Signal wire patching** - Connect outputs to inputs visually
- **Live manifest preview** - See the generated YAML as you build
- **Trigger visualization** - See which signals trigger which components
- **Lane assignment** - Drag components into fast/normal/slow/llm lanes
- **Real-time validation** - Catch invalid signal references immediately

This makes it easy to experiment with different workflow shapes without writing YAML by hand, while still giving you full control over the generated configuration.

---

## Core Concept: Waves

A wave is a composable analysis stage. **This interface exists to make "should we run?" a first-class decision**, not an implementation detail buried in conditional logic.

```csharp
public interface IContentAnalysisWave
{
    string Name { get; }
    int Priority { get; }               // Higher runs first
    bool Enabled { get; set; }

    // Quick filter - avoid expensive work
    bool ShouldRun(string contentPath, AnalysisContext context);

    // Do the analysis
    Task<IEnumerable<Signal>> AnalyzeAsync(
        string contentPath,
        AnalysisContext context,
        CancellationToken ct);
}
```

**Simple wave example:**

```csharp
public class FileTypeWave : IContentAnalysisWave
{
    public string Name => "FileType";
    public int Priority => 100;
    public bool Enabled { get; set; } = true;

    public bool ShouldRun(string path, AnalysisContext ctx)
    {
        // Skip if we already know the type
        return ctx.GetSignal("file.type") == null;
    }

    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string path,
        AnalysisContext ctx,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(path);
        var mimeType = GetMimeType(extension);

        return new[]
        {
            new Signal
            {
                Key = "file.type",
                Value = mimeType,
                Confidence = 1.0,
                Source = Name
            }
        };
    }
}
```

**Wave coordination:**

The `WaveCoordinator` runs waves in priority order:

```csharp
var coordinator = new WaveCoordinator(waves, profile);
var context = new AnalysisContext();

var results = await coordinator.ExecuteAsync(filePath, context, ct);

// All signals from all waves
foreach (var signal in context.GetAllSignals())
{
    Console.WriteLine($"{signal.Key}: {signal.Value}");
}
```

**Concurrency lanes:**

Waves run in lanes with different concurrency limits:

| Lane | Purpose | Concurrency |
|------|---------|-------------|
| `fast` | Quick checks (IP lookup, file type) | 16 |
| `normal` | Standard processing (parsing, chunking) | 8 |
| `io` | I/O bound (file reads, API calls) | 32 |
| `llm` | Expensive LLM calls | 2 |

This prevents expensive operations from blocking cheap ones.

---

## Architecture: How It Fits Together

Here's the complete picture:

```mermaid
graph TB
    subgraph Input["Input Layer"]
        REQ[HTTP Request]
        FILE[File Upload]
        JOB[Background Job]
    end

    subgraph Ephemeral["Ephemeral Layer"]
        COORD[Work Coordinator]
        OPS[Operations<br/>own signals]
        SINK[SignalSink<br/>read-only view]
    end

    subgraph StyloFlow["StyloFlow Layer"]
        MAN[Manifests]
        WAVE[Wave Coordinator]
        ATOMS[Atoms<br/>own signals]
    end

    subgraph Execution["Execution"]
        FAST[Fast Lane]
        NORM[Normal Lane]
        LLM[LLM Lane]
    end

    subgraph Output["Output"]
        RES[Results]
        ESCAL[Escalation]
        STORE[Persistence]
    end

    REQ --> COORD
    FILE --> COORD
    JOB --> COORD

    COORD --> OPS
    SINK -.queries.-> OPS

    WAVE -.reads.-> SINK
    MAN -.configures.-> WAVE
    WAVE --> ATOMS

    ATOMS --> FAST
    ATOMS --> NORM
    ATOMS --> LLM

    SINK -.queries.-> FAST
    SINK -.queries.-> NORM
    SINK -.queries.-> LLM

    SINK -.read for.-> RES
    SINK -.read for.-> ESCAL
    SINK -.read for.-> STORE

    style COORD stroke:#339af0
    style SINK stroke:#339af0
    style WAVE stroke:#51cf66
    style ATOMS stroke:#51cf66
```

**Flow:**

1. Input arrives (HTTP request, file, job)
2. Ephemeral coordinator creates operation (which owns an empty signal list)
3. Operation adds signals to its owned list
4. Wave coordinator READS signals via SignalSink view to check trigger conditions
5. Waves whose triggers match run in priority order within concurrency lanes
6. Each wave adds signals to its operation's owned list (externally immutable)
7. Wave coordinator continues reading signals to find newly satisfied triggers
8. Final output queries SignalSink to read signals and determine actions

**Ownership model:** Each operation/atom owns its signals. SignalSink provides a read-only view across all operations. Signals can be escalated (copied) or echoed (preserved when evicted), but the owned list is externally immutable.

**Current execution model:** Single-process, bounded concurrency, observable operations with LRU eviction.

**Future execution model:** Distributed lanes across machines, SignalSink queries remote operations, atoms execute on different hosts. **Signals remain the stable boundary** - they're already serializable, timestamped, and self-contained. The ownership model doesn't change.

The in-process implementation validates the semantics. Distribution is about scaling the execution substrate, not changing the orchestration model.

---

## Use Case: lucidRAG Document Processing

Let's see how [lucidRAG](/blog/lucidrag-multi-document-rag-web-app) uses StyloFlow:

**Stage 1: Initial Detection**

```csharp
public class FileTypeDetectorWave : IContentAnalysisWave
{
    public int Priority => 100;  // Run first

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var extension = Path.GetExtension(path);

        return new[]
        {
            new Signal
            {
                Key = "file.extension",
                Value = extension,
                Source = "FileTypeDetector"
            }
        };
    }
}
```

**Stage 2: Chunking (triggered by file.extension)**

```csharp
// In manifest:
// triggers:
//   requires:
//     - signal: file.extension
//       condition: in
//       value: [".pdf", ".docx", ".md"]

public class ChunkingWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 80;

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var chunks = await ChunkDocumentAsync(path);

        ctx.SetCached("chunks", chunks);  // Share with other waves

        return new[]
        {
            new Signal
            {
                Key = "document.chunked",
                Value = chunks.Count,
                Source = Name
            }
        };
    }
}
```

**Stage 3: Embedding (triggered by document.chunked)**

```csharp
public class EmbeddingWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 60;

    public bool ShouldRun(string path, AnalysisContext ctx)
    {
        // Only run if chunking succeeded
        return ctx.GetSignal("document.chunked") != null;
    }

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var chunks = ctx.GetCached<List<Chunk>>("chunks");
        var embeddings = await GenerateEmbeddingsAsync(chunks);

        ctx.SetCached("embeddings", embeddings);

        return new[]
        {
            new Signal
            {
                Key = "embeddings.generated",
                Value = embeddings.Count,
                Source = Name
            }
        };
    }
}
```

**Stage 4: Entity Extraction (parallel with embedding)**

```csharp
public class EntityExtractionWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 60;  // Same as embedding - runs in parallel

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var chunks = ctx.GetCached<List<Chunk>>("chunks");

        // Use deterministic IDF scoring, not LLM per chunk
        // (See Reduced RAG pattern)
        var entities = await ExtractEntitiesAsync(chunks);

        return new[]
        {
            new Signal
            {
                Key = "entities.extracted",
                Value = entities.Count,
                Confidence = CalculateConfidence(entities),
                Source = Name
            }
        };
    }
}
```

**Stage 5: Quality Check**

```csharp
public class QualityCheckWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 40;  // After embedding + entities

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var embeddingSignal = ctx.GetSignal("embeddings.generated");
        var entitySignal = ctx.GetSignal("entities.extracted");

        var embeddingCount = (int)embeddingSignal.Value;
        var entityConfidence = entitySignal.Confidence;

        var quality = CalculateQuality(embeddingCount, entityConfidence);

        var signals = new List<Signal>
        {
            new Signal
            {
                Key = "quality.score",
                Value = quality,
                Source = Name
            }
        };

        // Trigger escalation if quality is poor
        if (quality < GetParam<double>("quality_threshold", 0.7))
        {
            signals.Add(new Signal
            {
                Key = "escalation.needed",
                Value = "low_quality_document",
                Source = Name
            });
        }

        return signals;
    }
}
```

**Benefits of this approach:**

1. **Parallel execution** - Embedding and entity extraction run simultaneously
2. **Conditional branching** - Quality check decides if escalation is needed
3. **Shared context** - Waves access chunks without passing them explicitly
4. **Easy to extend** - Add a new wave without changing existing ones
5. **Observable** - Every stage emits signals you can monitor

This is the [Reduced RAG](/blog/reduced-rag) pattern in action: deterministic extraction up front, LLMs only for synthesis.

---

## Use Case: Stylobot Chat Pipeline

Stylobot (the chatbot on this site) uses StyloFlow for message processing:

**Stage 1: Intent Detection**

```csharp
// Fast pattern matching - no LLM
public class IntentDetectorWave : IContentAnalysisWave
{
    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var message = await File.ReadAllTextAsync(path);

        var intents = new List<string>();

        if (ContainsCodeRequest(message))
            intents.Add("code_example");

        if (ContainsSearchTerms(message))
            intents.Add("search");

        if (IsGreeting(message))
            intents.Add("greeting");

        return new[]
        {
            new Signal
            {
                Key = "intent.detected",
                Value = intents,
                Confidence = intents.Any() ? 0.8 : 0.3,
                Source = Name
            }
        };
    }
}
```

**Stage 2: Document Retrieval (triggered by "search" intent)**

```csharp
// Manifest configures this to run only if intent includes "search"
public class DocumentRetrievalWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var message = await File.ReadAllTextAsync(path);

        // Hybrid search: BM25 + vector similarity
        var results = await _searchService.SearchAsync(message, topK: 10);

        ctx.SetCached("search_results", results);

        return new[]
        {
            new Signal
            {
                Key = "documents.retrieved",
                Value = results.Count,
                Confidence = results.Any() ? 0.9 : 0.1,
                Source = Name
            }
        };
    }
}
```

**Stage 3: Response Generation**

```csharp
public class ResponseGenerationWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var message = await File.ReadAllTextAsync(path);
        var intents = ctx.GetSignal("intent.detected")?.Value as List<string>;
        var searchResults = ctx.GetCached<List<SearchResult>>("search_results");

        string response;

        if (intents?.Contains("greeting") == true)
        {
            // No LLM needed for greetings
            response = GetGreetingResponse();
        }
        else if (searchResults?.Any() == true)
        {
            // LLM synthesis over retrieved docs
            response = await GenerateResponseAsync(message, searchResults);
        }
        else
        {
            response = "I don't have enough information to answer that.";
        }

        await File.WriteAllTextAsync(
            Path.Combine(Path.GetDirectoryName(path), "response.txt"),
            response);

        return new[]
        {
            new Signal
            {
                Key = "response.generated",
                Value = response.Length,
                Source = Name
            }
        };
    }
}
```

**Escalation pattern:**

If initial response has low confidence, escalate to a better model:

```yaml
# In ResponseGenerationWave manifest:
emits:
  conditional:
    - key: escalation.needed
      when: confidence < 0.7

escalation:
  targets:
    better_model:
      when:
        - signal: escalation.needed
          condition: exists
      skip_when:
        - signal: budget.exhausted
```

This implements the wave escalation pattern from [Semantic Intelligence](/blog/semanticintelligence-part10).

---

## Signal-Driven Orchestration Patterns

**Pattern 1: Fan-Out**

One signal triggers multiple waves:

```mermaid
graph LR
    S1[document.uploaded] --> W1[ChunkingWave]
    S1 --> W2[MetadataWave]
    S1 --> W3[LanguageDetectionWave]

    W1 -.signal.-> S2[document.chunked]
    W2 -.signal.-> S3[metadata.extracted]
    W3 -.signal.-> S4[language.detected]

    style S1 stroke:#339af0
    style S2 stroke:#339af0
    style S3 stroke:#339af0
    style S4 stroke:#339af0
    style W1 stroke:#51cf66
    style W2 stroke:#51cf66
    style W3 stroke:#51cf66
```

**Pattern 2: Sequential Dependency**

Waves wait for previous signals:

```mermaid
graph LR
    W1[ExtractWave] -.signal.-> S1[text.extracted]
    S1 --> W2[ChunkWave]
    W2 -.signal.-> S2[text.chunked]
    S2 --> W3[EmbedWave]
    W3 -.signal.-> S3[embeddings.generated]

    style S1 stroke:#339af0
    style S2 stroke:#339af0
    style S3 stroke:#339af0
    style W1 stroke:#51cf66
    style W2 stroke:#51cf66
    style W3 stroke:#51cf66
```

**Pattern 3: Conditional Branching**

Different waves run based on signals:

```mermaid
graph TD
    W1[DetectorWave] -.signal.-> S1{confidence}

    S1 -->|< 0.4| W2[RejectWave]
    S1 -->|0.4-0.7| W3[EscalateWave]
    S1 -->|> 0.7| W4[AcceptWave]

    W2 -.signal.-> S2[rejected]
    W3 -.signal.-> S3[escalated]
    W4 -.signal.-> S4[accepted]

    style S1 stroke:#ffd43b
    style S2 stroke:#ff6b6b
    style S3 stroke:#ff922b
    style S4 stroke:#51cf66
    style W1 stroke:#339af0
    style W2 stroke:#ff6b6b
    style W3 stroke:#ff922b
    style W4 stroke:#51cf66
```

**Pattern 4: Aggregation**

Multiple signals trigger one wave:

```mermaid
graph LR
    W1[Wave A] -.signal.-> S1[a.complete]
    W2[Wave B] -.signal.-> S2[b.complete]
    W3[Wave C] -.signal.-> S3[c.complete]

    S1 --> T{All Ready?}
    S2 --> T
    S3 --> T

    T -->|Yes| W4[AggregatorWave]
    W4 -.signal.-> S4[aggregation.complete]

    style S1 stroke:#339af0
    style S2 stroke:#339af0
    style S3 stroke:#339af0
    style S4 stroke:#51cf66
    style T stroke:#ffd43b
    style W4 stroke:#51cf66
```

---

## Escalation: From Fast to Thorough

The key innovation is **bounded escalation** - cheap detectors run first, expensive analysis only when needed.

**Example: Bot detection**

```csharp
// Stage 1: Fast IP check (< 10ms)
public class IpReputationWave : IContentAnalysisWave
{
    public int Priority => 100;

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var ip = ctx.GetCached<string>("client_ip");
        var reputation = await _ipService.CheckAsync(ip);

        double confidence;

        if (reputation == IpReputation.KnownBot)
            confidence = 0.95;
        else if (reputation == IpReputation.Suspicious)
            confidence = 0.5;
        else
            confidence = 0.1;

        var signals = new List<Signal>
        {
            new Signal
            {
                Key = "bot.detected",
                Value = reputation != IpReputation.Clean,
                Confidence = confidence,
                Source = Name
            }
        };

        // Trigger escalation if unsure
        if (confidence > 0.4 && confidence < 0.7)
        {
            signals.Add(new Signal
            {
                Key = "bot.escalation.needed",
                Source = Name
            });
        }

        return signals;
    }
}
```

**Stage 2: Behavioral analysis (triggered by escalation, ~100ms)**

```csharp
// Only runs if bot.escalation.needed signal exists
public class BehaviorAnalysisWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 80;

    public bool ShouldRun(string path, AnalysisContext ctx)
    {
        // Skip if already confident
        var botSignal = ctx.GetBestSignal("bot.detected");
        if (botSignal?.Confidence > 0.7 || botSignal?.Confidence < 0.4)
            return false;

        // Only run if escalation requested
        return ctx.GetSignal("bot.escalation.needed") != null;
    }

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var userAgent = ctx.GetCached<string>("user_agent");
        var clickPattern = ctx.GetCached<List<Click>>("clicks");

        var behaviorScore = AnalyzeBehavior(userAgent, clickPattern);

        return new[]
        {
            new Signal
            {
                Key = "bot.detected",
                Value = behaviorScore > 0.6,
                Confidence = behaviorScore,
                Source = Name
            }
        };
    }
}
```

**Stage 3: LLM analysis (only if still unsure, ~2s)**

```csharp
public class LlmBotAnalysisWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public int Priority => 60;

    public bool ShouldRun(string path, AnalysisContext ctx)
    {
        var botSignal = ctx.GetBestSignal("bot.detected");

        // Only use LLM if still in ambiguous range
        return botSignal?.Confidence > 0.4 &&
               botSignal?.Confidence < 0.7;
    }

    public async Task<IEnumerable<Signal>> AnalyzeAsync(...)
    {
        var messages = ctx.GetCached<List<Message>>("conversation");

        var prompt = $@"Analyze if this conversation is from a bot:
{string.Join("\n", messages.Select(m => m.Text))}

Reply with JSON: {{""is_bot"": bool, ""confidence"": 0.0-1.0, ""reasoning"": string}}";

        var response = await _llm.CompleteAsync(prompt);
        var result = JsonSerializer.Deserialize<BotAnalysisResult>(response);

        return new[]
        {
            new Signal
            {
                Key = "bot.detected",
                Value = result.IsBot,
                Confidence = result.Confidence,
                Source = Name,
                Metadata = new() { ["reasoning"] = result.Reasoning }
            }
        };
    }
}
```

**Cost breakdown:**

| Stage | Latency | Cost | Hit Rate |
|-------|---------|------|----------|
| IP check | 5ms | $0 | 100% of requests |
| Behavioral | 100ms | $0 | 30% (ambiguous cases) |
| LLM | 2s | $0.002 | 5% (still ambiguous) |

**Total cost:** $0.002 * 0.05 = **$0.0001 per request**

Compare to naive "LLM everything" approach: $0.002 * 100% = **$0.002 per request** (20x more expensive)

This is the power of wave-based escalation.

---

## Minimal Shape

> This is not a quick-start guide; it's the smallest example that shows how the model fits together.

**Installation:**

```bash
dotnet add package StyloFlow.Complete
```

**Conceptual entry point:**

```csharp
// 1. Define a wave
public class MyAnalysisWave : IContentAnalysisWave
{
    public string Name => "MyAnalysis";
    public int Priority => 50;
    public bool Enabled { get; set; } = true;

    public bool ShouldRun(string path, AnalysisContext ctx) => true;

    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string path,
        AnalysisContext ctx,
        CancellationToken ct)
    {
        // Your analysis logic here
        var result = await AnalyzeAsync(path);

        return new[]
        {
            new Signal
            {
                Key = "my.signal",
                Value = result,
                Confidence = 1.0,
                Source = Name
            }
        };
    }
}

// 2. Register waves
var waves = new List<IContentAnalysisWave>
{
    new MyAnalysisWave(),
    new AnotherWave(),
};

// 3. Create coordinator
var coordinator = new WaveCoordinator(
    waves,
    CoordinatorProfile.Default);

// 4. Execute
var context = new AnalysisContext();
var results = await coordinator.ExecuteAsync(filePath, context);

// 5. Read signals
foreach (var signal in context.GetAllSignals())
{
    Console.WriteLine($"{signal.Key}: {signal.Value} ({signal.Confidence})");
}
```

**With manifests:**

```csharp
// Load manifests from directory
var loader = new FileSystemManifestLoader("./manifests");
var manifests = await loader.LoadAllAsync();

// Build waves from manifests
var waves = manifests
    .Where(m => m.Enabled)
    .OrderBy(m => m.Priority)
    .Select(m => WaveFactory.Create(m))
    .ToList();

var coordinator = new WaveCoordinator(waves, profile);
```

For complete examples, see the [StyloFlow GitHub repository](https://github.com/scottgal/styloflow).

---

## Workflow Discoverability: YAML Manifests

One of StyloFlow's key features is **workflow discoverability** - you can understand the entire pipeline just by reading the manifests. No code diving required.

### Complete lucidRAG Document Pipeline

Here's the actual manifest directory structure for lucidRAG:

```
manifests/
├── 01-file-type-detector.yaml
├── 02-chunking.yaml
├── 03-embedding.yaml
├── 04-entity-extraction.yaml
├── 05-quality-check.yaml
└── 06-escalation.yaml
```

**01-file-type-detector.yaml:**

```yaml
name: FileTypeDetector
priority: 100
enabled: true
description: Detects file type from extension

taxonomy:
  kind: sensor
  determinism: deterministic
  persistence: ephemeral

triggers:
  requires:
    - signal: document.uploaded
      condition: exists

emits:
  on_start:
    - file.detection.started
  on_complete:
    - key: file.extension
      type: string
      confidence_range: [1.0, 1.0]
    - key: file.mime_type
      type: string
      confidence_range: [1.0, 1.0]

lane:
  name: fast
  max_concurrency: 16

budget:
  max_duration: 10ms
```

**02-chunking.yaml:**

```yaml
name: ChunkingWave
priority: 80
enabled: true
description: Splits documents into semantic chunks

taxonomy:
  kind: extractor
  determinism: deterministic
  persistence: ephemeral

input:
  accepts:
    - document.pdf
    - document.docx
    - document.markdown
  required_signals:
    - file.extension

triggers:
  requires:
    - signal: file.extension
      condition: in
      value: [".pdf", ".docx", ".md", ".txt"]

emits:
  on_complete:
    - key: document.chunked
      type: integer
      confidence_range: [1.0, 1.0]
    - key: chunks.cached
      type: boolean

lane:
  name: normal
  max_concurrency: 8

budget:
  max_duration: 30s

defaults:
  chunking:
    max_chunk_size: 512
    overlap: 50
    respect_boundaries: true
```

**03-embedding.yaml:**

```yaml
name: EmbeddingWave
priority: 60
enabled: true
description: Generates ONNX embeddings for chunks

taxonomy:
  kind: embedder
  determinism: deterministic
  persistence: cached

input:
  required_signals:
    - document.chunked
    - chunks.cached

triggers:
  requires:
    - signal: document.chunked
      condition: ">"
      value: 0

emits:
  on_complete:
    - key: embeddings.generated
      type: integer
      confidence_range: [1.0, 1.0]

lane:
  name: normal
  max_concurrency: 4

budget:
  max_duration: 2m
  max_cost: 0.0  # Local ONNX model

defaults:
  embedding:
    model: all-MiniLM-L6-v2
    batch_size: 32
```

**04-entity-extraction.yaml:**

```yaml
name: EntityExtractionWave
priority: 60  # Same as embedding - runs in parallel
enabled: true
description: Extracts entities using IDF scoring

taxonomy:
  kind: extractor
  determinism: deterministic
  persistence: persisted

input:
  required_signals:
    - document.chunked

triggers:
  requires:
    - signal: document.chunked
      condition: ">"
      value: 0

emits:
  on_complete:
    - key: entities.extracted
      type: integer
      confidence_range: [0.0, 1.0]  # Confidence varies

lane:
  name: normal
  max_concurrency: 8

budget:
  max_duration: 1m

defaults:
  entity:
    min_idf_score: 2.5
    min_frequency: 2
    max_entities: 100
```

**05-quality-check.yaml:**

```yaml
name: QualityCheckWave
priority: 40
enabled: true
description: Validates extraction quality

taxonomy:
  kind: gatekeeper
  determinism: deterministic
  persistence: ephemeral

input:
  required_signals:
    - embeddings.generated
    - entities.extracted

triggers:
  requires:
    - signal: embeddings.generated
      condition: ">"
      value: 0
    - signal: entities.extracted
      condition: exists

emits:
  on_complete:
    - key: quality.score
      type: double
      confidence_range: [0.0, 1.0]

  conditional:
    - key: escalation.needed
      when: quality.score < 0.7

lane:
  name: fast
  max_concurrency: 16

defaults:
  quality:
    min_embeddings: 5
    min_entity_confidence: 0.5
    threshold: 0.7
```

**06-escalation.yaml:**

```yaml
name: EscalationWave
priority: 20
enabled: true
description: Improves low-quality extractions using LLM

taxonomy:
  kind: proposer
  determinism: probabilistic
  persistence: persisted

input:
  required_signals:
    - escalation.needed

triggers:
  requires:
    - signal: escalation.needed
      condition: exists
  skip_when:
    - signal: budget.exhausted

emits:
  on_complete:
    - key: escalation.complete
      type: boolean
    - key: entities.improved
      type: integer
      confidence_range: [0.7, 1.0]

lane:
  name: llm
  max_concurrency: 2  # Expensive

budget:
  max_duration: 30s
  max_tokens: 4000
  max_cost: 0.05

defaults:
  llm:
    model: gpt-4o-mini
    temperature: 0.1
    prompt_template: entity_extraction
```

### Benefits of Declarative Manifests

Looking at these files, you immediately know:

1. **Execution order** - Priority numbers (100 → 80 → 60 → 40 → 20)
2. **Dependencies** - What signals each wave needs to run
3. **Parallelism** - Embedding (60) and EntityExtraction (60) run together
4. **Resource limits** - Different lanes and concurrency per wave
5. **Conditional logic** - Escalation only runs if quality < 0.7
6. **Cost boundaries** - Escalation has token and cost limits

**No code reading required.** The workflow is self-documenting.

### Code-Based Atoms Referenced in Manifests

While the examples above show fully declarative YAML, waves can also be **code-based atoms** referenced in manifests:

```yaml
name: CustomAnalyzer
priority: 50
enabled: true
description: Custom analysis logic

# Reference a code-based atom implementation
implementation:
  assembly: MyProject.Analyzers
  type: MyProject.Analyzers.CustomAnalyzerWave
  method: AnalyzeAsync

# The manifest still declares the contract
taxonomy:
  kind: analyzer
  determinism: probabilistic

triggers:
  requires:
    - signal: data.ready

emits:
  on_complete:
    - key: analysis.complete
      confidence_range: [0.0, 1.0]

lane:
  name: normal
  max_concurrency: 4

# Configuration values passed to the atom
defaults:
  threshold: 0.75
  max_iterations: 10
```

The C# implementation:

```csharp
public class CustomAnalyzerWave : ConfiguredComponentBase, IContentAnalysisWave
{
    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string path,
        AnalysisContext ctx,
        CancellationToken ct)
    {
        // Access manifest config
        var threshold = GetParam<double>("threshold", 0.75);
        var maxIterations = GetParam<int>("max_iterations", 10);

        // Custom logic here
        var result = await PerformComplexAnalysis(path, threshold, maxIterations);

        return new[]
        {
            new Signal
            {
                Key = "analysis.complete",
                Value = result.Score,
                Confidence = result.Confidence,
                Source = Name
            }
        };
    }
}
```

**Benefits:**
- **Manifest declares the contract** - triggers, signals, budget, lanes
- **Code provides implementation** - complex logic, external dependencies
- **Configuration flows through** - manifest defaults override code defaults
- **Testing independence** - Test code without manifests, validate manifests without running code

This hybrid approach gives you declarative workflow discovery while keeping complex logic in maintainable C#.

### Pipeline Visualization from Manifests

The manifest structure makes it trivial to generate visualizations:

```mermaid
graph TD
    DOC[document.uploaded] --> FT[FileTypeDetector<br/>Priority: 100<br/>Lane: fast]
    FT --> EXT[file.extension]

    EXT --> CH[ChunkingWave<br/>Priority: 80<br/>Lane: normal]
    CH --> CHUNKED[document.chunked]

    CHUNKED --> EMB[EmbeddingWave<br/>Priority: 60<br/>Lane: normal]
    CHUNKED --> ENT[EntityExtractionWave<br/>Priority: 60<br/>Lane: normal]

    EMB --> EMBGEN[embeddings.generated]
    ENT --> ENTEX[entities.extracted]

    EMBGEN --> QC[QualityCheckWave<br/>Priority: 40<br/>Lane: fast]
    ENTEX --> QC

    QC --> QSCORE[quality.score]
    QC -.conditional.-> ESC_NEED[escalation.needed]

    ESC_NEED -.-> ESC[EscalationWave<br/>Priority: 20<br/>Lane: llm]
    ESC --> ESC_DONE[escalation.complete]

    style DOC stroke:#339af0
    style FT stroke:#51cf66
    style CH stroke:#51cf66
    style EMB stroke:#51cf66
    style ENT stroke:#51cf66
    style QC stroke:#ffd43b
    style ESC stroke:#ff922b
```

This diagram was generated programmatically from the YAML manifests - no manual drawing.

---

## Emergent Properties

**1. Declarative composition**

Components declare their contracts (triggers, signals, budget), not their dependencies. The system figures out execution order. This isn't a feature - it's what happens when you make signals first-class.

**2. Observable by default**

Every action is a signal. You don't add observability - it's inherent. Full execution trace, confidence tracking, escalation paths, and budget consumption fall out naturally.

**3. Adaptive execution**

Confidence scores drive branching without explicit routing logic. Skip expensive stages when unnecessary, escalate when unsure, abort early on high-confidence failures. The control flow emerges from signal patterns.

**4. Testability without mocking frameworks**

Mock signals, not components:

```csharp
var context = new AnalysisContext();
context.AddSignal(new Signal
{
    Key = "document.chunked",
    Value = 10,
    Confidence = 1.0,
    Source = "Test"
});

var wave = new EmbeddingWave();
var results = await wave.AnalyzeAsync(path, context, ct);

Assert.Single(results);
Assert.Equal("embeddings.generated", results.First().Key);
```

**5. Incremental complexity**

Start simple:
```csharp
var coordinator = new EphemeralWorkCoordinator<Job>(ProcessAsync);
```

Add signals when needed:
```csharp
new EphemeralOptions { Signals = signalSink }
```

Add waves for multi-stage:
```csharp
var waveCoordinator = new WaveCoordinator(waves, profile);
```

Add manifests for declarative config:
```yaml
name: MyWave
triggers: [...]
emits: [...]
```

---

## Complete Pipeline: lucidRAG Document Ingestion

Here's the complete [lucidRAG](/blog/lucidrag-multi-document-rag-web-app) pipeline at a glance (see the detailed code examples in the "Use Case: lucidRAG Document Processing" section above):

1. **Upload** → `document.uploaded` signal
2. **FileTypeWave** detects PDF/DOCX/Markdown → `file.extension` signal
3. **ExtractionWave** pulls text and structure → `text.extracted` signal
4. **ChunkingWave** splits semantically → `document.chunked` signal
5. **EmbeddingWave** generates vectors (parallel) → `embeddings.generated` signal
6. **EntityWave** extracts entities (parallel) → `entities.extracted` signal
7. **QualityWave** checks completeness → `quality.score` signal
8. **EscalationWave** (conditional if quality < 0.7) → `escalation.complete` signal
9. **StorageWave** persists to Qdrant + PostgreSQL → `storage.complete` signal

The UI polls the SignalSink view for progress updates (SignalSink provides a read-only view across all document operations):

```csharp
// Background service polls for document progress
var documentOps = coordinator.GetOperations()
    .Where(op => op.State.ContainsKey("documentId"));

foreach (var op in documentOps)
{
    var signals = op.GetSignals()
        .Where(s => s.Key.StartsWith("document."));

    foreach (var signal in signals)
    {
        await _hub.Clients.User(userId)
            .SendAsync("DocumentProgress", new
            {
                stage = signal.Key,
                progress = CalculateProgress(signal)
            });
    }
}
```

Or using the SignalSink view directly:

```csharp
// SignalSink is a view across all operations
var documentSignals = signalSink.GetSignals()
    .Where(s => s.Key.StartsWith("document.") &&
                s.Timestamp > lastCheck);

// Read signals, don't subscribe - signals are owned by operations
```

This is how [lucidRAG](/blog/lucidrag-multi-document-rag-web-app) processes documents, data, and images through a unified signal-driven pipeline - combining [DocSummarizer](/blog/building-a-document-summarizer-with-rag), [DataSummarizer](/blog/datasummarizer-how-it-works), and [ImageSummarizer](/blog/constrained-fuzzy-image-intelligence) under one orchestration layer.

---

## Complete Pipeline: Stylobot Message Routing

Here's the complete Stylobot pipeline at a glance (see the detailed code examples in the "Use Case: Stylobot Chat Pipeline" section above):

1. **IntentWave** detects user intent (fast pattern matching) → `intent.detected` signal
2. **SearchWave** (conditional if intent includes "search") → `documents.retrieved` signal
3. **ResponseWave** generates answer → `response.generated` signal
4. **EscalationWave** (conditional if confidence < 0.7) → `response.improved` signal

The key benefit: conditional execution based on signals.

```csharp
// ❌ Traditional: Every message goes through everything
var intent = await DetectIntentAsync(message);
var docs = await SearchAsync(message);  // Even if not needed
var response = await GenerateAsync(message, docs);

// ✅ StyloFlow: Waves run conditionally based on signals
// SearchWave only runs if IntentWave emits "search" intent
// EscalationWave only runs if ResponseWave confidence < 0.7
```

Simple greetings skip search and LLM generation entirely. Complex questions trigger the full pipeline with escalation. This saves ~90% of LLM costs on typical chat traffic.

---

## Comparison to Other Workflow Engines

| Feature | StyloFlow | Temporal | Airflow | Step Functions |
|---------|-----------|----------|---------|----------------|
| **Coordination** | Signal-driven | RPC-based | DAG-based | State machine |
| **Declarative** | ✅ YAML manifests | ❌ Code-first | ✅ DAGs | ✅ JSON/YAML |
| **Conditional** | ✅ Signal triggers | ✅ Conditions | ✅ Branching | ✅ Choice states |
| **Escalation** | ✅ Built-in | ❌ Manual | ❌ Manual | ❌ Manual |
| **Observability** | ✅ Signal trace | ✅ Workflow history | ✅ Task logs | ✅ Execution history |
| **Budget control** | ✅ Token/cost limits | ❌ Manual | ❌ Manual | ❌ Manual |
| **Local execution** | ✅ In-process | ❌ Requires cluster | ❌ Requires cluster | ❌ AWS only |
| **Concurrency lanes** | ✅ Fast/Normal/LLM | ❌ Manual | ✅ Pools | ❌ Service limits |

**Where this model fits naturally:**

- AI/ML pipelines with escalation (cheap → expensive)
- Document processing with conditional stages
- Bot detection with multi-stage analysis
- RAG systems with hybrid search + generation
- Any workflow where components react to confidence scores

**Where it doesn't (and won't):**

- Simple sequential jobs (ephemeral coordinator is enough)
- Mature distributed workflows across data centers (Temporal solves this)
- Long-running workflows with human approvals (Temporal/Airflow)
- Workflow versioning with schema migration (Temporal)

---

## Where This Model Leads

> These are natural extensions of the model, not commitments to a specific implementation.

As the semantics stabilize through lucidRAG and Stylobot development, these patterns become viable:

**1. Learning from signals**

Track which escalation paths work best:
```csharp
// Did the LLM escalation improve accuracy?
// Learn to skip it if behavioral analysis is sufficient
```

**2. Cost optimization**

Automatic lane assignment based on historical performance:
```csharp
// If a "slow" wave completes quickly, promote to "normal"
```

**3. Signal replay**

Debug by replaying signal sequences:
```csharp
var replay = SignalReplay.FromFile("trace.jsonl");
await coordinator.ReplayAsync(replay);
```

**4. Multi-machine coordination**

Distribute lanes across machines while keeping signals centralized.

---

## Why Signals Matter

The core insight is this: **in AI systems, every component has confidence**.

Traditional workflows assume success/failure. AI workflows need:
- **Confidence scores** - How sure are we?
- **Conditional execution** - Skip expensive stages if confident
- **Escalation** - Try harder when unsure
- **Aggregation** - Combine multiple signals

Signals provide this naturally:

```csharp
// Multiple detectors vote
var signals = context.GetSignals("bot.detected");

// Aggregate by confidence
var verdict = signals
    .OrderByDescending(s => s.Confidence)
    .First();

// Or majority vote
var isBot = signals
    .Count(s => (bool)s.Value) > signals.Count() / 2;

// Or weighted average
var score = signals
    .Sum(s => (bool)s.Value ? s.Confidence : -s.Confidence)
    / signals.Count();
```

This is why StyloFlow works well for [Reduced RAG](/blog/reduced-rag) - every extraction stage produces a confidence score, and synthesis only happens when confidence is high enough.

---

## Summary

**The execution model:**

- Signals as first-class facts (not events or messages)
- Confidence scores drive control flow
- Waves coordinate via triggers, not direct calls
- Lanes enforce resource boundaries
- Built on [mostlylucid.ephemeral](/blog/ephemeral-execution-library)

**Why signals matter:**

1. **Stable distribution boundary** - Serializable, timestamped, self-contained
2. **Declarative composition** - Manifests define contracts, runtime figures out execution
3. **Adaptive routing** - Confidence enables escalation without hardcoded branching
4. **Inherent observability** - Every action is already a signal
5. **Clear ownership** - Atoms own signals, external immutability prevents action-at-a-distance

**Working implementations:**

- [lucidRAG](/blog/lucidrag-multi-document-rag-web-app) - Cross-modal document Q&A with conditional entity extraction
- Stylobot - Message routing with intent-based search triggering
- [Reduced RAG](/blog/reduced-rag) - Deterministic extraction + bounded LLM synthesis

**Related articles:**

- [Ephemeral Execution Library](/blog/ephemeral-execution-library) - The foundation
- [Reduced RAG](/blog/reduced-rag) - Why deterministic extraction matters
- [LRU and Capacity](/blog/learning-lrus-when-capacity-makes-systems-better) - Memory-bounded windows
- [Constrained Fuzzy Context Dragging](/blog/constrained-fuzzy-context-dragging) - The theory

**Source code:** [GitHub - StyloFlow](https://github.com/scottgal/styloflow)

---

## The Core Insight

Traditional workflow engines ask you to declare **what happens next**. This model asks components to declare **what they produce** and **what they need**, then lets signals coordinate execution.

The key shift: **signals decouple, confidence guides, lanes protect**.

This isn't about choosing StyloFlow over Temporal or Airflow - those solve different problems (durable execution, workflow versioning, distributed coordination across data centers). This is about articulating a different orchestration model: one where control flow emerges from signal patterns rather than being explicitly programmed.

If you're building AI/ML pipelines where:
- Confidence matters (not just success/failure)
- Escalation is structural (not a special case)
- Components shouldn't know about each other
- Observability should be inherent (not bolted on)

...then these execution semantics might fit how you think.

The [ephemeral library](/blog/ephemeral-execution-library) is the stable foundation. StyloFlow adds the signal-driven orchestration layer on top. Both are evolving through real use in lucidRAG and Stylobot.

For questions or feedback, see the [GitHub repository](https://github.com/scottgal/styloflow) or reach out via the chatbot on this site (which uses these patterns).
