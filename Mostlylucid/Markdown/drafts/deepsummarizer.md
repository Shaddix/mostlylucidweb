# Feature Specification

## Evidence-Grounded Deep Research Engine (Local-First)

### Overview

The Deep Research Engine is a local-first system for conducting evidence-grounded research using small or medium LLMs supported by deterministic decomposition tooling.

The system separates **planning**, **evidence extraction**, **statistical reduction**, and **synthesis** into explicit stages. LLMs are used only where they add value and are never allowed to invent facts or fill gaps beyond the provided corpus.

The core goal is to demonstrate that deep research does not require frontier models when architecture, tooling, and constraints are designed correctly.

---

## Design Goals

* Evidence over vibes
* Deterministic decomposition before synthesis
* Model-agnostic architecture
* Small models should perform competitively
* Outputs must be auditable, reproducible, and inspectable
* System must degrade gracefully when data is sparse

---

## Non-Goals

* No end-to-end “agent does everything” abstraction
* No implicit background knowledge injection
* No cloud dependency or vendor lock-in
* No training or fine tuning required

---

## High-Level Architecture

```
User Question
     ↓
(Optional) Research Planner (LLM)
     ↓
Source Discovery (Search Tools)
     ↓
Corpus Acquisition
     ↓
Deterministic Decomposition
  - DocSummarizer
  - DataSummarizer
     ↓
Research Corpus
     ↓
Topic Profiling
     ↓
Constrained Synthesis (LLM)
     ↓
Evidence-Grounded Output
```

---

## Core Components

### 1. Research Planner (Optional)

**Purpose**
Generate a structured research plan from a user question.

**Responsibilities**

* Decompose the question into sub-questions
* Identify likely source types (papers, datasets, standards, reports)
* Propose search queries
* Define success criteria and gaps to watch for

**Notes**

* Uses a larger LLM if available
* No data ingestion
* Output is advisory, not authoritative

**Output**

```json
{
  "researchQuestions": [],
  "searchQueries": [],
  "expectedEvidenceTypes": [],
  "knownUnknowns": []
}
```

---

### 2. Source Discovery

**Purpose**
Locate candidate sources for research.

**Inputs**

* Planner output or direct user queries

**Tools**

* DuckDuckGo
* Domain-restricted search
* File system or URL lists

**Constraints**

* No summarization
* No interpretation
* Sources treated as untrusted input

**Output**

* List of source URLs or files with metadata

---

### 3. Corpus Acquisition

**Purpose**
Fetch and normalize sources into analyzable artifacts.

**Features**

* SSRF-safe fetching
* Content-type validation
* HTML sanitization
* PDF, DOCX, Markdown, CSV, XLSX support
* Optional Playwright rendering for JS-heavy pages

**Output**

* Raw documents and datasets ready for decomposition

---

### 4. Deterministic Decomposition

#### 4.1 DocSummarizer Integration

**Purpose**
Extract structure and claims from textual sources without LLM usage.

**Extracts**

* Document hierarchy
* Atomic claims
* Quotable passages
* Entity mentions
* Section salience

**Guarantees**

* Same input → same output
* No hallucination possible

---

#### 4.2 DataSummarizer Integration

**Purpose**
Reduce large datasets into statistical signatures.

**Extracts**

* Schema
* Distributions
* Aggregates
* Outliers
* Drift markers

**Guarantees**

* Scales to millions of rows
* No raw data passed to LLMs

---

### 5. Research Corpus

**Purpose**
Unified, structured representation of all extracted evidence.

**Contents**

* Claims with source references
* Statistical summaries
* Dataset metadata
* Confidence indicators
* Temporal markers

**Properties**

* Fully inspectable
* Serializable
* Model-independent

---

### 6. Topic Profiles

**Purpose**
Create reusable, compressed representations of a research topic.

**Each Topic Profile Includes**

* Definition and scope
* Canonical sources
* Claim graph
* Consensus vs disagreement
* Key statistics and measures
* Open questions
* Freshness indicators

**Benefits**

* Reusable across questions
* Supports drift detection
* Enables fast follow-up queries

---

### 7. Constrained Synthesis

**Purpose**
Generate human- or machine-readable outputs strictly bounded to the research corpus.

**LLM Rules**

* May only reference provided claims
* Must cite evidence identifiers
* Must surface uncertainty explicitly
* Must not introduce external knowledge

**Supported Outputs**

* Executive summary
* Technical summary
* Claim validation report
* Fact-check verdicts
* Structured JSON

---

## Fact Checking Mode

**Input**

* Claims or documents to verify

**Process**

1. Decompose claims into atomic assertions
2. Retrieve relevant topic profiles and sources
3. Compare supporting vs contradicting evidence
4. Assign confidence levels

**Output**

```json
{
  "claim": "...",
  "verdict": "supported | disputed | insufficient",
  "confidence": "high | medium | low",
  "evidence": []
}
```

---

## Key Constraints (Hard Rules)

* LLMs never see raw documents or datasets
* No synthesis without a populated research corpus
* Every claim must reference evidence
* Absence of evidence must be surfaced
* Model choice must not affect correctness

---

## Model Strategy

* Large models: planning only
* Small models (1.5B–3B): synthesis
* Swappable at runtime
* Offline-first by default

---

## Success Criteria

* Small models produce credible, cited research outputs
* Outputs are reproducible across runs
* Users can trace every claim to a source
* System remains useful even with LLM disabled

---

## Summary Statement

> This system demonstrates that deep research is not a property of large models.
> It is a property of architecture, decomposition, and evidence discipline.

