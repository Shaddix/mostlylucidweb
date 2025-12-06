# **mostlylucid.botdetection: Fighting Back Against Scrapers (Part 1)**

*Scrapers are about to start using AI to mimic real users - so I built a bot detector that learns, adapts, and fights back.*

**Key concept: Behavioural Routing.** This enables a new category — where transparent, adjustable "teams" of detectors and learning systems reflexively route traffic based on learned behaviour patterns, not static rules. With the [YARP Gateway](https://hub.docker.com/r/scottgal/mostlylucid.yarpgateway), bots never reach your backend. Or use the middleware to build behavioural routing directly into your app layer.

<pinned/>
<!--category-- ASP.NET, Bot Detection, Security -->

<datetime class="hidden">2025-12-08T07:00</datetime>

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.botdetection.svg)](https://www.nuget.org/packages/mostlylucid.botdetection/)
[![GitHub](https://img.shields.io/github/stars/scottgal/mostlylucid.nugetpackages?style=social)](https://github.com/scottgal/mostlylucid.nugetpackages/blob/main/Mostlylucid.BotDetection/README.md)
[![Docker](https://img.shields.io/docker/pulls/scottgal/mostlylucid.yarpgateway)](https://hub.docker.com/r/scottgal/mostlylucid.yarpgateway)

---

## **Why This Exists**

Bot detection has quietly become one of the hardest problems in modern web engineering.

Not because bots got smarter - but because **AI made it trivial to mimic genuine user behaviour**:

* Rotating residential proxiess
* Perfectly valid browser fingerprints
* Realistic mouse movement
* Executing JavaScript
* Adapting when blocked

Commercial solutions solve this, but they’re expensive (£3K+/month is typical), closed-source, and tied to specific CDNs. You never really know what’s happening under the hood.

I wanted something different:

* Open
* Local
* Understandable
* Easy to extend
* Cheap to run (yes, even on a Raspberry Pi)

So I built **mostlylucid.botdetection** - a modular, learning bot detection engine for .NET.

It started simple… and then grew into something far more interesting.

---

[TOC]

---

## **A Concrete Example (What This Actually Catches)**

Here’s a real-world scenario:

A scraper spoofs:

```
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120
```

Looks legitimate.

But it *forgets* a header Chrome always sends:

```
Sec-Fetch-Mode
```

And its `Accept-Language` header doesn’t match the claimed locale.

And the request rate is clearly automated.

**One signal is fine. Two is suspicious. Three is a pattern.**

The system flags it in under 100 milliseconds - no AI needed.

This is the foundation of the whole design:
**don’t rely on one big “bot or not” model. Accumulate evidence.**

---

## Philosophy of the System

At its core, this project isn’t really about bot detection at all - it’s about treating traffic as a living system rather than a stream of isolated requests. 

Modern scrapers behave more like adaptive organisms: they learn, mutate, probe for weaknesses, and respond to pressure. So the defence must evolve too. 

The philosophy here is simple: observe signals, combine them, let them interact, and adapt over time. Instead of a single "bot/not-bot" check, the engine becomes a network of small detectors, each contributing evidence into a shared blackboard, where higher-order behaviours can emerge. Policies become composable flows, not hard-coded rules. Reputation shifts gradually instead of flipping states. AI is just another contributor, not a monolith.

The system is built to be transparent, explainable, extensible, and self-correcting, with the long-term goal of behaving less like a firewall and more like an immune system: fast at the edge, intelligent in the core, and always learning.

---

## **How It Works (The Short Version)**

Requests flow through several small detectors, each contributing a little piece of evidence.

Think of it as an airport security checkpoint… but fast.

```mermaid
flowchart TB
    subgraph Request["Incoming Request"]
        R[HTTP Request]
    end

    subgraph FastPath["Fast Path (< 100ms)"]
        UA[User-Agent Check]
        HD[Header Analysis]
        IP[Datacenter IP Lookup]
        RT[Rate Anomalies]
        HE[Heuristic Model]
    end

    subgraph SlowPath["Slow Path (Async Learning)"]
        LLM[LLM Reasoning]
        Learn[Weight Learning]
    end

    subgraph Output["Decision"]
        Score[Risk Score 0-1]
        Action[Allow / Throttle / Block]
    end

    R --> UA & HD & IP & RT
    UA & HD & IP & RT --> HE
    HE --> Score --> Action

    HE -.-> LLM -.-> Learn
    Learn -.->|updates weights| HE

    style FastPath stroke:#10b981,stroke-width:2px
    style SlowPath stroke:#6366f1,stroke-width:2px
    style Output stroke:#f59e0b,stroke-width:2px
```

### **Fast Path (< 100ms)**

Runs synchronously. Doesn’t slow your app.

* Known bot patterns
* Missing headers real browsers always send
* Datacenter IPs (AWS/Azure/GCP)
* Rate spikes
* Inconsistencies between UA + headers

This catches **80%** of bots instantly.

### **Slow Path (Async)**

Runs in the background.

* Heuristic model with learned weights
* LLM reasoning via [Ollama](https://ollama.com/)
* Updating pattern reputation
* Forgetting stale signals

This catches the adaptive bots - the ones most people *think* they're catching with "regex on User-Agent".

---

## **Try It in 10 Seconds**

```bash
dotnet add package Mostlylucid.BotDetection
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBotDetection();

var app = builder.Build();
app.UseBotDetection();
app.Run();
```

That’s it. Everything works out of the box.

---

## **What It Detects (At a Glance)**

| Check           | What It Finds                                     |
| --------------- | ------------------------------------------------- |
| **User-Agent**  | Known bots, libraries, scrapers                   |
| **Headers**     | Missing security headers, impossible combinations |
| **IP**          | Cloud hosts pretending to be “home users”         |
| **Rate**        | Automation bursts, distributed scraping           |
| **Consistency** | “Chrome/120” without Chrome’s actual header set   |

Consistency is the sleeper feature - modern bots can spoof *one* signal but usually fail at cross-signal coherence.

---

## **A Real Detection Result (Broken Down)**

Here's what a real detection looks like — this is from the demo running the `fastpath` policy (all detectors in parallel). In production, most requests exit early after just 2-3 detectors agree.

### Summary

```json
{
  "policy": "fastpath",
  "isBot": false,
  "isHuman": true,
  "humanProbability": 0.8,
  "botProbability": 0.2,
  "confidence": 0.76,
  "riskBand": "Low",
  "recommendedAction": { "action": "Allow", "reason": "Low risk (probability: 20%)" },
  "processingTimeMs": 50.7,
  "detectorsRan": ["UserAgent", "Ip", "Header", "ClientSide", "Behavioral", "Heuristic", "VersionAge", "Inconsistency"],
  "detectorCount": 8,
  "earlyExit": false
}
```

**8 detectors ran in 51ms** — that's parallel execution across multiple evidence sources.

### Detector Contributions

Each detector contributes a weighted impact. Negative = human signal. Positive = bot signal.

| Detector | Impact | Weight | Weighted | Reason |
|----------|--------|--------|----------|--------|
| **UserAgent** | -0.20 | 1.0 | -0.20 | User-Agent appears normal |
| **Header** | -0.15 | 1.0 | -0.15 | Headers appear normal |
| **Behavioral** | -0.10 | 1.0 | -0.10 | Request patterns appear normal |
| **Heuristic** | -0.77 | 2.0 | **-1.54** | 88% human likelihood (16 features) |
| **ClientSide** | -0.05 | 0.8 | -0.04 | Fingerprint appears legitimate |
| **VersionAge** | -0.05 | 0.8 | -0.04 | Browser/OS versions appear current |
| **Inconsistency** | -0.05 | 0.8 | -0.04 | No header/UA inconsistencies |
| **IP** | 0.00 | 0.5 | 0.00 | Localhost (neutral in dev) |

The **Heuristic detector** dominates here — it's weighted 2x and used 16 features to reach 88% human confidence.

### Signals Collected

Each detector emits signals that feed into the heuristic model:

```json
{
  "ua.is_bot": false,
  "ua.raw": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36...",
  "ip.is_local": true,
  "ip.address": "::1",
  "header.has_accept_language": true,
  "header.has_accept_encoding": true,
  "header.count": 16,
  "fingerprint.integrity_score": 1,
  "behavioral.anomaly": false,
  "heuristic.prediction": "human",
  "heuristic.confidence": 0.77,
  "versionage.analyzed": true
}
```

These signals persist and train the learning system over time.

### Category Breakdown

Scores aggregate by category for the final decision:

| Category | Score | Weight | Notes |
|----------|-------|--------|-------|
| Heuristic | -1.54 | 2.0 | Strongest human signal |
| UserAgent | -0.20 | 1.0 | Normal browser UA |
| Header | -0.15 | 1.0 | All expected headers present |
| Behavioral | -0.10 | 1.0 | No rate anomalies |
| ClientSide | -0.04 | 0.8 | Valid fingerprint received |
| VersionAge | -0.04 | 0.8 | Current browser version |
| Inconsistency | -0.04 | 0.8 | UA matches headers |
| IP | 0.00 | 0.5 | Localhost (dev neutral) |

**Total weighted score: -2.11** → Strong human signal → Allow.

> **Note:** This is the demo's `fastpath` policy which runs **all** detectors for visibility. In real production with early exit enabled, high-confidence requests exit after just 2-3 detectors agree — typically **under 10ms**. The 51ms here is because demo mode disables early exit to show all contributions.

### Full Pipeline (Demo Mode with LLM)

For comparison, here's the `demo` policy — the complete pipeline including LLM reasoning. This shows what happens when detectors **disagree**:

```json
{
  "policy": "demo",
  "isBot": false,
  "isHuman": true,
  "humanProbability": 0.87,
  "botProbability": 0.13,
  "confidence": 1.0,
  "botType": "Scraper",
  "riskBand": "Low",
  "recommendedAction": { "action": "Allow", "reason": "Low risk (probability: 13%)" },
  "processingTimeMs": 1370,
  "aiRan": true,
  "detectorsRan": ["UserAgent", "Ip", "Header", "ClientSide", "Behavioral",
                   "VersionAge", "Inconsistency", "Heuristic", "HeuristicLate", "Llm"],
  "detectorCount": 10
}
```

**10 detectors in 1.4 seconds** — the LLM ran and *disagreed* with the heuristics.

| Detector | Impact | Weight | Weighted | Reason |
|----------|--------|--------|----------|--------|
| **LLM** | +0.85 | 2.5 | **+2.13** | "Chrome common in bots, cookies + referer suspicious" |
| **HeuristicLate** | -0.77 | 2.5 | -1.92 | 88% human (with all evidence) |
| **Heuristic** (early) | -0.77 | 2.0 | -1.54 | 88% human likelihood (16 features) |
| **UserAgent** | -0.20 | 1.0 | -0.20 | User-Agent appears normal |
| **Header** | -0.15 | 1.0 | -0.15 | Headers appear normal |
| **Behavioral** | -0.10 | 1.0 | -0.10 | Request patterns appear normal |
| **ClientSide** | 0.00 | 1.8 | 0.00 | No fingerprint (awaiting JS) |
| **VersionAge** | -0.05 | 0.8 | -0.04 | Browser/OS versions current |
| **Inconsistency** | -0.05 | 0.8 | -0.04 | No header/UA inconsistencies |
| **IP** | 0.00 | 0.5 | 0.00 | Localhost (neutral) |

This is the interesting case — **the LLM flagged it as a potential bot** while all static detectors said human:

```json
{
  "ai.prediction": "bot",
  "ai.confidence": 0.85,
  "ai.learned_pattern": "Browser string suggests Chrome, common in bots. Presence of cookies and a specific referer also points to a potential bot."
}
```

The LLM's reasoning gets recorded as a signal that feeds back into the learning system. Over time, if this pattern keeps appearing and gets confirmed as bot traffic, the heuristic weights will adjust.

Notice:

1. **Heuristic runs twice** — early (before all detectors) and late (after all evidence). Both said "human" with 88% confidence.

2. **LLM disagreed** — it spotted patterns the static detectors missed. Its +2.13 weighted impact partially counters the heuristic's -3.46.

3. **No fingerprint** — ClientSide returned 0 because JS hadn't executed yet. In a real browser, this would add more human signal.

4. **Final verdict: Allow** — even with the LLM's suspicion, the combined evidence still favours human (87%). But the `botType: "Scraper"` flag means it's being watched.

The category breakdown shows the tension:

| Category | Score | Weight | Notes |
|----------|-------|--------|-------|
| **Heuristic** | -3.46 | 4.5 | Strong human signal |
| **AI** | +2.13 | 2.5 | LLM says bot |
| UserAgent | -0.20 | 1.0 | Normal browser |
| Header | -0.15 | 1.0 | All headers present |
| Behavioral | -0.10 | 1.0 | Normal patterns |
| ClientSide | 0.00 | 1.8 | No fingerprint yet |
| VersionAge | -0.04 | 0.8 | Current versions |
| Inconsistency | -0.04 | 0.8 | UA matches headers |
| IP | 0.00 | 0.5 | Localhost |

**Total weighted score: -1.86** → Human wins, but the LLM's dissent is noted.

> **Key insight:** The system doesn't blindly trust any single detector. When they disagree, evidence is weighted and the majority wins — but minority opinions get recorded for learning.

> **Important:** This verbose output is demo-only. In production, you get a slim response via HTTP headers (`X-Bot-Confidence`, `X-Bot-RiskBand`, etc.) or a simple `context.IsBot()` check. The full JSON is for debugging and tuning — you'd never send this to clients.

---

## **Using the Results**

```csharp
if (context.IsBot())
    return Results.StatusCode(403);

var score = context.GetBotConfidence();  // 0.0–1.0
var risk  = context.GetRiskBand();       // Low/Elevated/Medium/High
```

### Protecting Endpoints

```csharp
app.MapGet("/api/data", Secret).BlockBots();
app.MapGet("/sitemap.xml", Sitemap)
   .BlockBots(allowVerifiedBots: true);
```

Risk levels guide the action:

| Risk     | Confidence | Recommended Action |
| -------- | ---------- | ------------------ |
| Low      | < 0.3      | Allow              |
| Elevated | 0.3–0.5    | Log / rate-limit   |
| Medium   | 0.5–0.7    | Challenge          |
| High     | > 0.7      | Block              |

---

## **AI Detection (Optional)**

Not required - but useful for catching advanced automation.

### **Heuristic Detector (Fast, Learning)**

The system includes a heuristic detector that uses logistic regression with dynamically learned weights. It starts with sensible defaults and evolves based on detection feedback.

Typical latency: **1–5ms**

```json
{
  "BotDetection": {
    "AiDetection": {
      "Heuristic": {
        "Enabled": true,
        "LoadLearnedWeights": true,
        "EnableWeightLearning": true
      }
    }
  }
}
```

Features are extracted dynamically - new patterns automatically get default weights and learn over time. The system discovers what matters for *your* traffic.

### **Ollama LLM (Deep Reasoning)**

Catches evasive bots that look "fine" to fast rules. Uses [Ollama](https://ollama.com/) for local LLM inference.

```bash
ollama pull gemma3:1b
```

```json
{
  "BotDetection": {
    "AiDetection": {
      "Provider": "Ollama",
      "Ollama": { "Model": "gemma3:1b" }
    }
  }
}
```

AI is **fail-safe** - if it's down, detection continues normally.

---

## **The Learning System: Adaptive, Not Trigger-Happy**

Static blocklists go stale. Attackers adapt.
So this system learns.

```mermaid
flowchart LR
    N[Neutral] -->|repeated bad activity| S[Suspect]
    S -->|confirmed| B[Blocked]
    B -->|no activity| S
    S -->|stays clean| N

    style B stroke:#ef4444,stroke-width:2px
    style S stroke:#eab308,stroke-width:2px
    style N stroke:#10b981,stroke-width:2px
```

Patterns decay over time:

* IPs get reassigned
* Misconfigured scripts get fixed
* Traffic changes naturally

Without decay you’d block legitimate users forever.

```json
{
  "BotDetection": {
    "Learning": {
      "Enabled": true,
      "ScoreDecayTauHours": 168,
      "GcEligibleDays": 90
    }
  }
}
```

---

## **YARP Gateway: Edge Protection for Your App**

There’s also a **Docker-first YARP reverse proxy** that runs detection *before* requests hit your app.

```mermaid
flowchart LR
    I[Internet] --> G[YARP Gateway]
    G -->|Human| App[Your App]
    G -->|Search Engine Bot| App
    G -->|Malicious| Block[403]
```

Run it in one line:

```bash
docker run -p 80:8080 \
  -e DEFAULT_UPSTREAM=http://your-app:3000 \
  scottgal/mostlylucid.yarpgateway
```

Works on:

* Linux
* macOS
* Windows
* **ARM (yes, Raspberry Pi)**

For custom routing:

```yaml
services:
  gateway:
    image: scottgal/mostlylucid.yarpgateway
    volumes:
      - ./yarp.json:/app/config/yarp.json
```

---

## **A Reasonable Production Config**

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,
    "BlockDetectedBots": true,
    "EnableAiDetection": true,
    "Learning": { "Enabled": true },
    "PathPolicies": {
      "/api/login": "strict",
      "/sitemap.xml": "allowVerifiedBots"
    }
  }
}
```

---

## **Where This Is Going**

This is Part 1 (the overview).
The next parts dig deeper:

* **Part 2**: The detection pipeline - fast, slow, and coordinated
* **Part 3**: Behaviour analytics
* **Part 4**: Client-side fingerprinting
* **Part 5**: The heuristic detector - learning weights in real-time
* **Part 6**: LLM detection internals
* **Part 7**: The learning system explained properly

**Future roadmap:**
* RAG-based pattern matching with vector embeddings
* Local small model inference via [LlamaSharp](https://github.com/SciSharp/LLamaSharp) / ONNX
* Semantic similarity for detecting novel attack patterns

If you want a bot detector you can *understand*, *extend*, and *run anywhere*, this series is for you.

---

## **Links**

* **GitHub:** full docs
  [https://github.com/scottgal/mostlylucid.nugetpackages](https://github.com/scottgal/mostlylucid.nugetpackages)
* **NuGet:** install the package
  [https://www.nuget.org/packages/mostlylucid.botdetection](https://www.nuget.org/packages/mostlylucid.botdetection)
* **Docker Hub:** YARP gateway
  [https://hub.docker.com/r/scottgal/mostlylucid.yarpgateway](https://hub.docker.com/r/scottgal/mostlylucid.yarpgateway)

**Unlicense – public domain. Use it however you want.**

