# No, Small Models Are Not the "Budget Option"

<!--category-- AI, LLM, Opinion, Ollama, ONNX -->
<datetime class="hidden">2025-12-28T16:00</datetime>

Small and local LLMs are often framed as the cheap alternative to frontier models. That framing is wrong. They are not a degraded version of the same thing. They are a different architectural choice, selected for **control**, **predictability**, and **survivable failure modes**.

I'm as guilty as anyone for pushing 'they're free' narrative...as if that were the only deciding factor. But like choosing a database / hosting platform for a system you need to understand **what trade-offs you are making**.

Using a small model via [Ollama](https://ollama.ai/), LM Studio, [ONNX Runtime](https://onnxruntime.ai/), or similar is not (just) about saving money. It is about choosing **where non-determinism is allowed to exist**.

[TOC]

## The Real Difference: Failure Modes

Large frontier models are broader and more fluent. They densify more of human expressed logic, span more domains, and produce more convincing reasoning traces. That also makes them **more dangerous** in systems that require guarantees.

Frontier models make sense when breadth is required and outputs are advisory by design - creative drafting, open-ended exploration, or synthesis across unfamiliar domains. But that's not most production systems.

Their failures are **semantic rather than structural**. This is the category error: treating a probabilistic component as if it were a system boundary. They generate valid-looking outputs that are wrong in subtle ways. Those failures are:

- Expensive to detect
- Expensive to debug
- Often only visible after damage is done

**Small models fail differently.**

When a small model is confused, it tends to:

- Break schemas
- Emit invalid JSON
- Truncate outputs
- Lose track of structure

These are **cheap failures**. They are detectable with simple validation. They trigger retries or fallbacks immediately. They do not silently advance state.

**This is not a weakness. It is a feature.**

## Where This Principle Comes From

This insight isn't abstract theory - it's the foundation of the [Ten Commandments of LLM Use](/blog/tencommandments). The core principle:

> **LLMs interpret reality. They must never be allowed to define it.**

When you follow this principle, you discover something surprising: **you stop needing expensive models**. A 7B parameter model running locally can classify, summarise, and generate hypotheses just fine - because the deterministic systems around it handle everything that actually needs to be correct.

Small models are not "weak" - they are often **sufficient** because the problem has already been reduced by the time it reaches them.

The frontier models are selling you reliability you should be building yourself.

## The Right Mental Model

Just as DuckDB is not "cheap SQL" and Postgres is not "worse Azure SQL", small LLMs occupy a **different point in the design space**. You choose them when:

| Concern | Small Model Advantage |
|---------|----------------------|
| **Locality** | Runs on your hardware, your network, your jurisdiction |
| **Auditability** | Every inference is logged, reproducible, inspectable |
| **Blast radius** | Failures are contained, not propagated through API chains |
| **Correctness enforcement** | Validation happens outside the model |
| **Bounded non-determinism** | Uncertainty is tightly constrained |

## How I Use This in Practice

This isn't hypothetical. My projects demonstrate this pattern repeatedly:

### GraphRAG with Three Extraction Modes

[My GraphRAG implementation](/blog/graphrag-minimum-viable-implementation) offers three modes:

| Mode | LLM Calls | Best For |
|------|-----------|----------|
| **Heuristic** | 0 per chunk | Pure determinism via IDF + structure |
| **Hybrid** | 1 per document | Small model validates candidates |
| **LLM** | 2 per chunk | Maximum quality when needed |

The **hybrid mode** is the sweet spot: heuristic extraction finds candidates (deterministic), then a small local model validates and enriches them. One LLM call per document, not per chunk.

With Ollama running locally, the cost is $0. But that's not why I use it - cost savings are a side-effect of correct abstraction, not the goal. I use it because **the failures are cheap and obvious**.

### ONNX Embeddings: No LLM Required

[Semantic search with ONNX and Qdrant](/blog/semantic-search-with-onnx-and-qdrant) shows another pattern: some tasks don't need an LLM at all. BERT embeddings via ONNX Runtime give you:

- **CPU-friendly inference** - no GPU required
- **Deterministic outputs** - same input always produces same embedding
- **Local execution** - no API calls, no latency, no rate limits
- **~90MB model** - runs anywhere

For [hybrid search](/blog/rag-hybrid-search-and-indexing), I combine these embeddings with BM25 scoring. The LLM only appears at synthesis time - and even then, a small local model works fine because it's **explaining** structure that deterministic systems have already validated.

### DocSummarizer: Structure First, LLM Second

[DocSummarizer](/blog/docsummarizer-tool) embodies this philosophy:

1. **Parse** documents with deterministic libraries (OpenXML, Markdig)
2. **Chunk** content using structural rules (headings, paragraphs, code blocks)
3. **Embed** chunks with ONNX BERT
4. **Retrieve** relevant chunks via vector search
5. **Synthesise** with Ollama - the only probabilistic step

The LLM is the **last step**, working on pre-validated, pre-structured content. It can fail - and when it does, the failure is obvious because the structure is already correct.


## The Three Questions

Frontier models are powerful tools when used deliberately. But they increase expressive power faster than they reduce risk. Small models, when embedded inside deterministic systems, give you just enough uncertainty to explore - **without obscuring truth or responsibility**.

The right question is not "which model is best?"

It is:

1. **Where does probability belong?**
2. **Where must determinism be absolute?**
3. **What failures can this system survive?**

If the answer involves state, side effects, money, policy, or guarantees - the model should never be in charge. And if the model is only there to classify, summarise, rank, or propose hypotheses, a small local model is often the **correct choice**, not the economical one.

## The Pattern: Boring Machinery + Small Model

This is the architecture that works:

```
┌─────────────────────────────────────────────────────┐
│                 DETERMINISTIC LAYER                 │
│  State machines, queues, validation, storage        │
│  (DuckDB, Postgres, Redis, file systems)           │
└─────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────┐
│                   INTERFACE LAYER                   │
│  Schema validation, retries, fallbacks             │
│  (Polly, FluentValidation, custom guards)          │
└─────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────┐
│                  PROBABILISTIC LAYER                │
│  Classification, summarisation, hypothesis gen     │
│  (Ollama, ONNX, small local models)                │
└─────────────────────────────────────────────────────┘
```

The LLM is at the **bottom**, not the top. It proposes; the deterministic layers dispose.

## Reliability Is Not About Avoiding Failure

All three perspectives - the questions, the pattern, and this final principle - reduce to the same rule:

**Reliability is about choosing failures you can survive.**

With LLMs, that means managing non-determinism through deterministic practices:

- [Commandment I](/blog/tencommandments): State lives outside the model
- [Commandment VII](/blog/tencommandments): Make failure loud and boring
- [Commandment IX](/blog/tencommandments): Build the boring machinery first

Small models make this easier because their failures are **loud**. Invalid JSON. Truncated output. Schema violations. These are gifts - they tell you immediately that something went wrong.

Frontier model failures are **quiet**. Plausible-sounding nonsense. Confident hallucinations. Semantic drift that only becomes visible when a customer complains or an audit fails.

**I'll take loud failures every time.**

## Related Reading

### The Philosophy
- [Ten Commandments of LLM Use](/blog/tencommandments) - The principles behind this approach
- [Why I Don't Use LangChain](/blog/why-i-dont-use-langchain) - Framework complexity vs. clarity
- [Why Commercial AI Projects Are Dumb](/blog/whycommercialaiprojectsaredumb) - The case for local-first AI

### The Implementation
- [GraphRAG: Minimum Viable Implementation](/blog/graphrag-minimum-viable-implementation) - Three extraction modes in practice
- [Semantic Search with ONNX and Qdrant](/blog/semantic-search-with-onnx-and-qdrant) - CPU-friendly embeddings
- [DocSummarizer Tool](/blog/docsummarizer-tool) - Structure first, LLM second
- [Hybrid Search and Auto-Indexing](/blog/rag-hybrid-search-and-indexing) - Production-ready search

### The Architecture
- [DiSE: Treating LLMs as Untrustworthy](/blog/blog-article-cooking-dise-part3-untrustworthy-gods) - The "untrustworthy gods" pattern
- [Bot Detection with LLM Advisors](/blog/botdetection-introduction) - LLM as advisor, not controller
- [Zero-PII Customer Intelligence](/blog/zero-pii-customer-intelligence-part1) - Semantic understanding with boundaries

## External Resources

- [Ollama](https://ollama.ai/) - Run LLMs locally with one command
- [ONNX Runtime](https://onnxruntime.ai/) - Cross-platform ML inference
- [LM Studio](https://lmstudio.ai/) - Desktop app for local LLMs
- [llama.cpp](https://github.com/ggerganov/llama.cpp) - Efficient C++ inference
