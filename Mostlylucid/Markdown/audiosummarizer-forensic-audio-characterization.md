# AudioSummarizer: Constrained Fuzzy Forensic Audio Characterization

<!-- category -- AI,Audio,ONNX,Patterns,Architecture,LLM,Speaker Diarization -->
<datetime class="hidden">2026-01-12T18:00</datetime>

> **Status**: AudioSummarizer.Core is currently in development as part of **[lucidRAG](https://www.lucidrag.com)**, a forthcoming mostlylucid product for multi-modal RAG. The implementation is complete and working—this article documents the architecture and design. CLI integration will be available in an upcoming lucidRAG release.
>
> **Source**: [github.com/scottgal/lucidrag](https://github.com/scottgal/lucidrag) (branch: `v2`)

**Where this fits**: AudioSummarizer is part of the **[lucidRAG](https://www.lucidrag.com)** family of Reduced RAG implementations. Each handles a different modality:
- **[DocSummarizer](/blog/building-a-document-summarizer-with-rag)** - Documents (entity extraction, knowledge graphs)
- **[ImageSummarizer](/blog/constrained-fuzzy-image-intelligence)** - Images (22-wave visual intelligence)
- **[DataSummarizer](/blog/datasummarizer-how-it-works)** - Data (schema inference, profiling)
- **AudioSummarizer** (this article) - Audio (acoustic profiling, speaker diarization)

All follow the same **[Reduced RAG pattern](/blog/reduced-rag)**: extract signals once, store evidence, synthesize with bounded LLM input.

---

Many demo-first audio analysis pipelines make the same mistake: they treat LLMs as if they were acoustic engineers.

They feed waveforms into models, ask them to "detect speech quality" or "identify speakers," and hope the model can infer structural properties from what is fundamentally a pattern-matching system. It works just well enough to demo—and then hallucinates speaker names, invents music genres, or confidently misidentifies accents.

**AudioSummarizer combines two complementary patterns:**

1. **[Reduced RAG](https://www.mostlylucid.net/blog/reduced-rag)** for retrieval - extract signals once, store evidence, query against facts
2. **[Constrained Fuzziness](/blog/constrained-fuzziness-pattern)** for orchestration - wave-based pipeline, deterministic substrate constrains probabilistic models

Instead of feeding raw audio to an LLM at query time, it **reduces** each audio file to a **signal ledger** (deterministic signals like RMS loudness, spectral features, speaker embeddings) with **extracted evidence** (transcripts, speaker sample clips, diarization turns). This ledger is persisted once, indexed for retrieval.

At **query time**, an LLM synthesizes responses by retrieving and reasoning over salient signals—not by re-analyzing audio. The heavy lifting (signal processing, transcription, diarization) happens during ingestion. The LLM only runs at query time to interpret pre-computed facts and generate human-readable answers.

> **Pattern composition**: Reduced RAG handles *what* to store and retrieve. Constrained Fuzziness handles *how* to extract signals reliably. Together they enable forensic audio characterization with bounded LLM cost.

> **Learn more about the Reduced RAG pattern**: [Reduced RAG: Signal-Driven Document Understanding](https://www.mostlylucid.net/blog/reduced-rag)

### Key Terminology

- **Signals**: Typed facts with confidence scores (e.g., `audio.content_type = "speech"`, confidence: 0.85)
- **Evidence**: Auditable artifacts (transcripts, speaker sample clips, diarization turns)
- **Signal Ledger**: The persisted bundle of all signals, evidence pointers, and embeddings extracted from an audio file
- **SpeakerId**: Local identifier within a single file/diarization run (e.g., `SPEAKER_00`)
- **VoiceprintId**: Cross-file stable hash derived from voice embedding (e.g., `vprint:a3f9c2e1`)
- **Person**: Explicitly NOT inferred—we never claim "this is John Smith"

**Identity model**: `SPEAKER_00` is local to one audio file. `VoiceprintId` is stable across files but anonymous. We never map to human names.

**Forensic requirements**: Every signal includes **provenance** (which wave produced it), **confidence** (degree of certainty), and **versioning** (model/threshold versions). This enables reproducibility: same input + same config → same signal ledger.

The result is:

* Fast, privacy-preserving forensic audio characterization
* Speaker diarization without Python dependencies (pure .NET)
* Anonymous speaker similarity detection (no PII, no names)
* Voice embeddings for "find similar speakers" queries
* All without sending audio to cloud APIs during ingestion

This builds directly on the **[Constrained Fuzziness Pattern](/blog/constrained-fuzziness-pattern)** and the wave-based architecture from **[ImageSummarizer](/blog/constrained-fuzzy-image-intelligence)**.

> **Core insight:** LLMs should reason over acoustic facts, not compute them.

This article covers:
- How AudioSummarizer implements the Reduced RAG pattern
- Signal extraction: deterministic acoustic facts (not LLM summaries)
- Evidence storage: speaker samples, transcripts, diarization turns
- Pure .NET speaker diarization (no Python, no pyannote)
- Query-time synthesis: filter signals → retrieve evidence → LLM synthesizes

**Related articles**:
- **[Reduced RAG](https://www.mostlylucid.net/blog/reduced-rag)** - The core pattern this implements
- [Constrained Fuzziness Pattern](/blog/constrained-fuzziness-pattern) - The foundational pattern
- **Reduced RAG Implementations:**
  - [DocSummarizer](/blog/building-a-document-summarizer-with-rag) - Document RAG with entity extraction and knowledge graphs
  - [DataSummarizer](/blog/datasummarizer-how-it-works) - Data profiling and schema inference RAG
  - [ImageSummarizer](/blog/constrained-fuzzy-image-intelligence) - Image RAG with 22-wave visual intelligence pipeline
  - **AudioSummarizer (this article)** - Audio forensic characterization with speaker diarization

[TOC]

---

## The Problem: Audio Analysis is Hard (and Culturally Loaded)

Audio analysis fails in predictable ways:

- **Cultural assertions**: "This is jazz" (says who? based on what training data?)
- **Speaker naming**: "Speaker is John Smith" (privacy violation, hallucination)
- **Music identification**: "Song: Sandstorm by Darude" (copyright issues, external knowledge)
- **Accent guessing**: "Speaker has British accent" (often wrong, potentially offensive)
- **Python dependencies**: Most diarization tools require pyannote (Python ecosystem lock-in)

Traditional approach: "Run Whisper for transcription, pyannote for diarization, send to LLM for summary"

**Problem**: This either leaks PII (speaker names), costs too much (cloud APIs), or requires Python runtime (pyannote, DIART).

**Solution**: Build a pure .NET wave-based pipeline that characterizes audio structurally and acoustically—without making cultural assertions.

---

## The Reduced RAG Audio Framework

AudioSummarizer implements the [Reduced RAG pattern](https://www.mostlylucid.net/blog/reduced-rag) for audio files, following its three core principles:

### 1. Signal Extraction (Ingestion Phase)

**Deterministic signals extracted once—not LLM-processed summaries:**

- **Temporal**: duration, timestamps, segment boundaries
- **Acoustic**: RMS loudness, spectral centroid, dynamic range, clipping ratio
- **Identity**: SHA-256 hash, file format, sample rate, channels
- **Quality**: transcription confidence, diarization confidence
- **Speaker**: voiceprint IDs (anonymous hashes), turn counts, participation percentages
- **Categorical**: content type (speech/music/silence), speaker classification (single/two/multi)

**Example signal set for a podcast:**
```json
{
  "audio.hash.sha256": "3f2a9c8b1e4d...",
  "audio.duration_seconds": 912.0,
  "audio.rms_db": -18.2,
  "audio.spectral_centroid_hz": 2418.3,
  "audio.content_type": "speech",
  "speaker.count": 2,
  "speaker.classification": "two_speakers",
  "transcription.confidence": 0.87,
  "voice.voiceprint_id.speaker_00": "vprint:a3f9c2e1d4b8"
}
```

These signals are **deterministic** (same audio → same values), **indexable** (can filter/sort in database), and **computable without LLMs** (pure signal processing + ONNX models).

### 2. Evidence Storage (Structured Units)

Instead of storing just "chunks," AudioSummarizer stores:

- **Signals**: Structured fields (JSON) for deterministic filtering
- **Embeddings**: Voice embeddings (512-dim ECAPA-TDNN) for speaker similarity
  - **Privacy note**: Embeddings are not feasibly invertible in this system, but treat them as sensitive data
- **Evidence artifacts**:
  - Full transcript text (searchable)
  - Speaker sample clips (Base64 WAV, 2-second clips for verification)
  - Diarization turns (JSON with speaker_id, start/end timestamps)
- **Pointers**: File hash + evidence IDs for auditable provenance

**Evidence is auditable**: Users can play speaker samples, read transcripts, inspect diarization turns—not just trust an LLM summary.

### 3. Query-Time Synthesis (Bounded LLM Input)

At query time, the LLM **never** sees raw audio. Instead:

1. **Filter deterministically** (database WHERE clause):
   ```sql
   WHERE audio.content_type = 'speech'
     AND speaker.count >= 2
     AND audio.rms_db > -25.0
     AND transcription.confidence > 0.8
   ```

2. **Hybrid search** (BM25 + vector, ~5 results):
   - BM25 on transcript text (keyword matching)
   - Vector similarity on voice embeddings (speaker similarity)
   - Return top 5 audio files

3. **Synthesize from signals** (LLM sees structured evidence pack):
   ```
   Audio 1: podcast_ep42.mp3
   - Duration: 15m 12s
   - Speakers: 2 (SPEAKER_00: 52%, SPEAKER_01: 48%)
   - Quality: RMS -18.2dB, no clipping
   - Transcript: "Welcome to Tech Insights. Today we're discussing..."
   - Entities: ["quantum computing", "Google", "IBM"]
   ```

The LLM receives **5 structured evidence packs** instead of **500 chunks of raw audio metadata**. Context window reduction: ~50× smaller.

**Cost impact** (if using paid LLM APIs): Processing 100 audio files goes from ~$5 (re-analyzing audio every query) to ~$0.10 (query against pre-computed signals).
*Note: lucidRAG is local-first (Ollama by default, zero cost). Paid APIs (Claude, GPT-4) are optional. Cost estimates assume paid API usage: ~$0.01/1K tokens (varies by provider), 10K tokens/query (raw metadata) vs 200 tokens/query (signals), 100 queries/day.*

This is the core Reduced RAG insight: **decide what matters upfront** (signals), **store it deterministically** (evidence), **involve LLM only for synthesis** (query-time).

---

## The Constrained Fuzziness Audio Pipeline

The system runs waves in priority order (higher number = runs first):

```
Wave Priority Order:
  100: IdentityWave          → SHA-256, file metadata, duration
   90: FingerprintWave       → Chromaprint perceptual hash (optional)
   80: AcousticProfileWave   → RMS, spectral features, SNR
   70: ContentClassifierWave → Speech vs music heuristics (routing)
   65: TranscriptionWave     → Whisper.NET (optional)
   60: SpeakerDiarizationWave→ Pure .NET speaker separation
   30: VoiceEmbeddingWave    → ECAPA-TDNN speaker similarity
```

> **Note**: Sentiment/emotion detection intentionally excluded—culturally loaded and not part of forensic characterization.

### Architecture

```mermaid
flowchart LR
    A[Audio file] --> B[IdentityWave]
    B --> C[AcousticProfileWave]
    C --> D[ContentClassifierWave]
    D --> E[TranscriptionWave]
    E --> F[SpeakerDiarizationWave]
    F --> G[VoiceEmbeddingWave]
    G --> H[Signal Ledger]
    H --> I[Optional LLM Synthesis]

    style B stroke:#333,stroke-width:4px
    style D stroke:#333,stroke-width:4px
    style F stroke:#333,stroke-width:4px
    style H stroke:#333,stroke-width:4px
```

This preserves the familiar **Wave → Signal → Optional LLM** loop, but anchors it in deterministic facts:

* Acoustic profiling is deterministic (same input = same output)
* The LLM operates on evidence, not raw audio

### Real-World Example: Processing a Podcast Episode

Let's trace a 15-minute podcast episode through the pipeline:

```
Input: podcast_ep42.mp3
  - File size: 14.2 MB
  - Format: MP3, 44.1kHz stereo, 192 kbps
  - Duration: 15m 12s (912 seconds)
  - Content: 2-person interview

Wave Execution (priority order 100 → 30):

1. IdentityWave (Priority 100, 87ms):
   ✓ SHA-256: 3f2a9c8b1e4d...
   ✓ Duration: 912.0s
   ✓ Channels: 2 (stereo)
   ✓ Sample rate: 44100 Hz
   ✓ File size: 14,897,234 bytes

2. AcousticProfileWave (Priority 80, 142ms):
   ✓ RMS loudness: -18.2 dB (good mastering)
   ✓ Peak amplitude: 0.94 (no clipping)
   ✓ Dynamic range: 22.1 dB
   ✓ Spectral centroid: 2418 Hz (speech-like)
   ✓ Spectral rolloff: 7892 Hz

3. ContentClassifierWave (Priority 70, 89ms):
   ✓ Zero-crossing rate: 0.17 (high → speech)
   ✓ Spectral flux: 0.28 (low → not music)
   → Classification: "speech" (confidence: 0.85)

4. TranscriptionWave (Priority 65, 12.3s):
   ✓ Whisper.NET base model
   ✓ Segments: 142
   ✓ Total words: 2,341
   ✓ Confidence: 0.87 (high)
   ✓ Text: "Welcome to Tech Insights. Today we're discussing..."

5. SpeakerDiarizationWave (Priority 60, 3.8s):
   ✓ VAD detected: 47 speech segments
   ✓ Embeddings extracted: 47 × 512-dim vectors
   ✓ Clustering (threshold 0.75): 2 speakers
   ✓ Turns before merge: 47
   ✓ Turns after merge: 23
   ✓ SPEAKER_00 participation: 52% (474s)
   ✓ SPEAKER_01 participation: 48% (438s)
   ✓ Sample clips extracted: 2 (Base64 WAV, ~30KB each)

6. VoiceEmbeddingWave (Priority 30, 178ms):
   ✓ ECAPA-TDNN inference
   ✓ Embedding dimension: 512
   ✓ Voiceprint ID (SPEAKER_00): "vprint:a3f9c2e1d4b8"
   ✓ Voiceprint ID (SPEAKER_01): "vprint:7e2d8f1a9c3b"

Total processing time: 16.6s
Signals emitted: 47
LLM calls: 0 (fully offline)
Cost: $0 (local processing only)
```

The **Signal Ledger** is what gets persisted to the database—the complete bundle of signals, evidence pointers, and embeddings. Here's what it looks like for this podcast:

**Signal Ledger (excerpt):**
```json
{
  "identity.filename": "podcast_ep42.mp3",
  "audio.hash.sha256": "3f2a9c8b1e4d...",
  "audio.duration_seconds": 912.0,
  "audio.format": "mp3",
  "audio.sample_rate": 44100,
  "audio.channels": 2,
  "audio.channel_layout": "stereo",
  "audio.rms_db": -18.2,
  "audio.dynamic_range_db": 22.1,
  "audio.spectral_centroid_hz": 2418.3,
  "audio.spectral_rolloff_hz": 7892.1,
  "audio.content_type": "speech",
  "content.confidence": 0.85,
  "speaker.count": 2,
  "speaker.classification": "two_speakers",
  "speaker.turn_count": 23,
  "speaker.avg_turn_duration": 39.7,
  "speaker.diarization_method": "agglomerative_clustering",
  "speaker.participation": {
    "SPEAKER_00": 52.0,
    "SPEAKER_01": 48.0
  },
  "transcription.full_text": "Welcome to Tech Insights...",
  "transcription.word_count": 2341,
  "transcription.confidence": 0.87,
  "voice.embedding.speaker_00": [0.023, -0.511, 0.882, ...],
  "voice.voiceprint_id.speaker_00": "vprint:a3f9c2e1d4b8",
  "speaker.sample.speaker_00": "UklGRiQAAABXQVZF...",
  "speaker.sample.speaker_01": "UklGRiQBBBXQVZF..."
}
```

**What this enables:**
- Search for "Tech Insights discusses cloud infrastructure" → finds this episode via transcript
- Query "Find audio with voiceprint vprint:a3f9c2e1" → finds all episodes with same speaker (cross-file)
- Ask "What's the dynamic range of our podcast episodes?" → aggregate acoustic signals
- Filter "Find low-quality recordings" → RMS < -25dB or clipping > 1%
- Verify "Did speaker identification work?" → play `speaker.sample.speaker_00` clip (within-file)

---

## Why Signals Matter (Even Without an LLM)

A signal ledger answers the boring-but-urgent questions immediately:

* Is this audio speech, music, or silence?
* How many speakers are detected?
* What's the signal-to-noise ratio?
* Are there clipping artifacts or distortion?
* What's the spectral centroid (brightness)?
* What's the dynamic range?

These are the questions you normally discover 30 minutes into Audacity archaeology.

The signal ledger gives you them in seconds.

---

## The Wave Pipeline: From Identity to Intelligence

### Wave 1: Identity (Deterministic Substrate)

The baseline. Cryptographic identity and file metadata.

```csharp
public class IdentityWave : IAudioWave
{
    public string Name => "IdentityWave";
    public int Priority => 100;  // Runs first

    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string audioPath,
        AnalysisContext context,
        CancellationToken ct)
    {
        var signals = new List<Signal>();

        // Cryptographic hash (deterministic identity)
        var fileHash = await ComputeSha256Async(audioPath, ct);
        signals.Add(new Signal
        {
            Name = "audio.hash.sha256",
            Value = fileHash,
            Type = SignalType.Identity,
            Confidence = 1.0,
            Source = Name
        });

        // File-level metadata
        var fileInfo = new FileInfo(audioPath);
        signals.Add(new Signal
        {
            Name = "audio.file_size_bytes",
            Value = fileInfo.Length,
            Type = SignalType.Metadata,
            Source = Name
        });

        // Audio format metadata
        using var reader = new AudioFileReader(audioPath);

        signals.Add(new Signal
        {
            Name = "audio.duration_seconds",
            Value = reader.TotalTime.TotalSeconds,
            Type = SignalType.Metadata,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.sample_rate",
            Value = reader.WaveFormat.SampleRate,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.channels",
            Value = reader.WaveFormat.Channels,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.format",
            Value = Path.GetExtension(audioPath).TrimStart('.'),
            Type = SignalType.Metadata,
            Source = Name
        });

        return signals;
    }
}
```

**Key signals emitted:**
- `audio.hash.sha256` - Cryptographic identity
- `audio.duration_seconds` - Length (deterministic)
- `audio.sample_rate` - 44100, 48000, etc.
- `audio.channels` - 1 (mono), 2 (stereo), 6 (5.1)
- `audio.format` - mp3, wav, flac, etc.

**Why deterministic?**
- Same file → same hash → same identity
- No sampling randomness, no temperature parameter
- Confidence = 1.0 (not probabilistic)

---

### Wave 2: Acoustic Profile (Signal Processing)

Extract structural acoustic properties using NAudio and FftSharp.

```csharp
public class AcousticProfileWave : IAudioWave
{
    private readonly ILogger<AcousticProfileWave> _logger;

    public string Name => "AcousticProfileWave";
    public int Priority => 80;

    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string audioPath,
        AnalysisContext context,
        CancellationToken ct)
    {
        var signals = new List<Signal>();

        using var reader = new AudioFileReader(audioPath);

        // Convert to mono for analysis
        ISampleProvider sampleProvider = reader.WaveFormat.Channels == 1
            ? reader
            : new StereoToMonoSampleProvider(reader) { LeftVolume = 0.5f, RightVolume = 0.5f };

        // Read all samples
        var samples = ReadAllSamples(sampleProvider);

        // Time-domain analysis
        var rms = CalculateRms(samples);
        var peakAmplitude = samples.Max(Math.Abs);
        var dynamicRange = CalculateDynamicRange(samples);
        var clippingRatio = CalculateClippingRatio(samples, threshold: 0.99);

        signals.Add(new Signal
        {
            Name = "audio.rms_db",
            Value = 20 * Math.Log10(rms),  // Convert to decibels
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.peak_amplitude",
            Value = peakAmplitude,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.dynamic_range_db",
            Value = dynamicRange,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.clipping_ratio",
            Value = clippingRatio,
            Type = SignalType.Acoustic,
            Confidence = clippingRatio > 0.01 ? 0.9 : 1.0,  // Low confidence if clipping detected
            Source = Name
        });

        // Frequency-domain analysis (FFT)
        var spectralFeatures = CalculateSpectralFeatures(samples, reader.WaveFormat.SampleRate);

        signals.Add(new Signal
        {
            Name = "audio.spectral_centroid_hz",
            Value = spectralFeatures.Centroid,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.spectral_rolloff_hz",
            Value = spectralFeatures.Rolloff,
            Type = SignalType.Acoustic,
            Source = Name
        });

        signals.Add(new Signal
        {
            Name = "audio.spectral_bandwidth_hz",
            Value = spectralFeatures.Bandwidth,
            Type = SignalType.Acoustic,
            Source = Name
        });

        return signals;
    }

    private SpectralFeatures CalculateSpectralFeatures(float[] samples, int sampleRate)
    {
        // Use FftSharp for frequency analysis
        int fftSize = 2048;
        var fftInput = new double[fftSize];

        // Take middle section of audio
        int offset = Math.Max(0, (samples.Length - fftSize) / 2);
        for (int i = 0; i < fftSize; i++)
        {
            fftInput[i] = samples[offset + i];
        }

        // Apply Hamming window
        var window = FftSharp.Window.Hamming(fftSize);
        for (int i = 0; i < fftSize; i++)
        {
            fftInput[i] *= window[i];
        }

        // Compute FFT
        var fft = FftSharp.Transform.FFT(fftInput);
        var magnitudes = fft.Select(c => Math.Sqrt(c.Real * c.Real + c.Imaginary * c.Imaginary)).ToArray();

        // Calculate spectral centroid (brightness)
        double sumWeightedFreq = 0;
        double sumMagnitude = 0;
        for (int i = 0; i < magnitudes.Length / 2; i++)
        {
            double freq = i * sampleRate / (double)fftSize;
            sumWeightedFreq += freq * magnitudes[i];
            sumMagnitude += magnitudes[i];
        }
        double centroid = sumMagnitude > 0 ? sumWeightedFreq / sumMagnitude : 0;

        // Calculate spectral rolloff (85% energy threshold)
        double totalEnergy = magnitudes.Take(magnitudes.Length / 2).Sum(m => m * m);
        double cumulativeEnergy = 0;
        double rolloff = 0;
        for (int i = 0; i < magnitudes.Length / 2; i++)
        {
            cumulativeEnergy += magnitudes[i] * magnitudes[i];
            if (cumulativeEnergy >= 0.85 * totalEnergy)
            {
                rolloff = i * sampleRate / (double)fftSize;
                break;
            }
        }

        return new SpectralFeatures
        {
            Centroid = centroid,
            Rolloff = rolloff,
            Bandwidth = CalculateBandwidth(magnitudes, centroid, sampleRate, fftSize)
        };
    }
}
```

**Example output:**
```
Input: podcast.mp3 (15 minutes, 44.1kHz stereo)

Signals emitted:
  audio.rms_db = -18.2 dB (good loudness)
  audio.peak_amplitude = 0.94 (no clipping)
  audio.dynamic_range_db = 22 dB (moderate dynamics)
  audio.clipping_ratio = 0.003 (0.3% clipping, minimal)
  audio.spectral_centroid_hz = 2400 Hz (mid-brightness, speech-like)
  audio.spectral_rolloff_hz = 8000 Hz (most energy below 8kHz)
  audio.spectral_bandwidth_hz = 4200 Hz (moderate spread)
```

**Why this matters:**
- RMS loudness indicates mastering quality
- Clipping ratio detects recording artifacts
- Spectral centroid differentiates speech (2-4kHz) from music (variable)
- All deterministic—same audio produces same values

---

### Wave 3: Content Classification (Speech vs Music)

**Rough heuristic classification** using zero-crossing rate and spectral flux for routing purposes.

> **Note**: These heuristics are genre/context dependent—not reliably accurate across all content types. Used only for wave routing decisions (e.g., skip diarization for music), not as ground truth.

```csharp
public class ContentClassifierWave : IAudioWave
{
    public string Name => "ContentClassifierWave";
    public int Priority => 70;

    public async Task<IEnumerable<Signal>> AnalyzeAsync(
        string audioPath,
        AnalysisContext context,
        CancellationToken ct)
    {
        var signals = new List<Signal>();

        using var reader = new AudioFileReader(audioPath);
        var samples = ReadAllSamples(reader);

        // Zero-crossing rate (heuristic: speech tends higher ZCR - not universal)
        var zcr = CalculateZeroCrossingRate(samples);

        // Spectral flux (heuristic: music tends more consistent - varies by genre)
        var spectralFlux = CalculateSpectralFlux(samples, reader.WaveFormat.SampleRate);

        // Simple heuristic classifier (for routing, not identity)
        // Thresholds calibrated for typical podcast/interview content
        // Production: calibrate on your corpus for best routing accuracy
        string contentType;
        double confidence;

        if (zcr > 0.15 && spectralFlux < 0.3)  // Speech heuristic
        {
            contentType = "speech";
            confidence = 0.85;
        }
        else if (zcr < 0.10 && spectralFlux > 0.5)
        {
            contentType = "music";
            confidence = 0.80;
        }
        else
        {
            contentType = "mixed";
            confidence = 0.70;
        }

        // Check for silence
        var rmsDb = context.GetValue<double>("audio.rms_db");
        if (rmsDb < -50)
        {
            contentType = "silence";
            confidence = 0.95;
        }

        signals.Add(new Signal
        {
            Name = "audio.content_type",
            Value = contentType,
            Type = SignalType.Classification,
            Confidence = confidence,
            Source = Name,
            Metadata = new Dictionary<string, object>
            {
                ["zero_crossing_rate"] = zcr,
                ["spectral_flux"] = spectralFlux,
                ["rms_db"] = rmsDb
            }
        });

        return signals;
    }
}
```

**Example routing:**
```
Speech (ZCR=0.18, flux=0.25):
  → audio.content_type = "speech"
  → Enables: TranscriptionWave, SpeakerDiarizationWave

Music (ZCR=0.08, flux=0.65):
  → audio.content_type = "music"
  → Disables: SpeakerDiarizationWave (no speakers to detect)

Silence (RMS=-52dB):
  → audio.content_type = "silence"
  → Disables: All downstream waves (early exit)
```

---

## Pure .NET Speaker Diarization (No Python)

**For lucidRAG's deployment constraints**: speaker diarization without pyannote, DIART, or any Python dependencies.

### The Problem with Python-Based Diarization

Traditional approach:
```
pyannote.audio (Python) → ONNX export (experimental) → C# wrapper (fragile)
```

**Problems:**
- pyannote 3.1+ removed ONNX support (moved to pure PyTorch)
- Python runtime required (deployment nightmare)
- HTTP wrapper adds latency and complexity
- No offline support

**Solution:** Implement diarization in pure .NET using existing voice embedding models.

### The Pure .NET Algorithm

```
1. Voice Activity Detection (VAD) → Detect speech segments (energy-based RMS)
2. Segment Embedding → Extract ECAPA-TDNN embeddings for each segment
3. Agglomerative Clustering → Group segments by speaker (cosine similarity)
4. Speaker Turns → Merge consecutive turns from same speaker
```

### Implementation

```csharp
public class SpeakerDiarizationService
{
    private readonly ILogger<SpeakerDiarizationService> _logger;
    private readonly VoiceEmbeddingService _embeddingService;
    private readonly AudioConfig _config;

    public virtual async Task<DiarizationResult> DiarizeAsync(
        string audioPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting speaker diarization for {AudioPath}", audioPath);

        // Step 1: Detect speech segments using VAD
        var segments = DetectSpeechSegments(audioPath);
        _logger.LogDebug("Detected {Count} speech segments", segments.Count);

        if (segments.Count == 0)
        {
            return new DiarizationResult
            {
                Turns = new List<SpeakerTurn>(),
                SpeakerCount = 0
            };
        }

        // Step 2: Extract embeddings for each segment
        var embeddings = new List<(SpeechSegment Segment, float[] Embedding)>();
        foreach (var segment in segments)
        {
            try
            {
                var embedding = await ExtractSegmentEmbeddingAsync(audioPath, segment, ct);
                embeddings.Add((segment, embedding));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract embedding for segment {Start}-{End}",
                    segment.StartSeconds, segment.EndSeconds);
            }
        }

        // Step 3: Cluster embeddings to identify speakers
        var speakerClusters = ClusterSpeakers(embeddings);
        _logger.LogInformation("Identified {Count} speakers", speakerClusters.Keys.Count);

        // Step 4: Create speaker turns
        var turns = new List<SpeakerTurn>();
        foreach (var (segment, embedding) in embeddings)
        {
            var speakerId = FindSpeakerForEmbedding(embedding, speakerClusters);
            turns.Add(new SpeakerTurn
            {
                SpeakerId = speakerId,
                StartSeconds = segment.StartSeconds,
                EndSeconds = segment.EndSeconds,
                Confidence = 1.0  // TODO: Calculate based on cluster distance
            });
        }

        // Step 5: Merge consecutive turns from same speaker
        var mergedTurns = MergeConsecutiveTurns(turns);

        return new DiarizationResult
        {
            Turns = mergedTurns,
            SpeakerCount = speakerClusters.Keys.Count
        };
    }

    // Simple VAD using energy-based speech detection
    private List<SpeechSegment> DetectSpeechSegments(string audioPath)
    {
        using var reader = new AudioFileReader(audioPath);
        ISampleProvider sampleProvider = reader.WaveFormat.Channels == 1
            ? reader
            : new StereoToMonoSampleProvider(reader) { LeftVolume = 0.5f, RightVolume = 0.5f };

        var sampleRate = sampleProvider.WaveFormat.SampleRate;
        var windowSize = sampleRate / 10; // 100ms windows
        var buffer = new float[windowSize];

        var segments = new List<SpeechSegment>();
        SpeechSegment? currentSegment = null;

        double timeSeconds = 0;
        int samplesRead;

        while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Calculate RMS energy for this window
            double rms = Math.Sqrt(buffer.Take(samplesRead).Sum(s => s * s) / samplesRead);

            // Speech detection threshold (simple baseline - fragile across gain levels)
            // Production: use relative threshold (noise floor / percentile) or per-file calibration
            bool isSpeech = rms > 0.02;  // Fixed threshold for demonstration

            if (isSpeech)
            {
                if (currentSegment == null)
                {
                    // Start new segment
                    currentSegment = new SpeechSegment
                    {
                        StartSeconds = timeSeconds
                    };
                }
            }
            else
            {
                if (currentSegment != null)
                {
                    // End current segment
                    currentSegment.EndSeconds = timeSeconds;

                    // Only keep segments longer than minimum duration
                    if (currentSegment.Duration >= _config.VoiceEmbedding.MinDurationSeconds)
                    {
                        segments.Add(currentSegment);
                    }

                    currentSegment = null;
                }
            }

            timeSeconds += (double)samplesRead / sampleRate;
        }

        // Close final segment if still open
        if (currentSegment != null)
        {
            currentSegment.EndSeconds = timeSeconds;
            if (currentSegment.Duration >= _config.VoiceEmbedding.MinDurationSeconds)
            {
                segments.Add(currentSegment);
            }
        }

        return segments;
    }

    // Agglomerative clustering of speaker embeddings
    private Dictionary<string, List<float[]>> ClusterSpeakers(
        List<(SpeechSegment Segment, float[] Embedding)> embeddings)
    {
        const double SimilarityThreshold = 0.75;  // Cosine similarity threshold

        var clusters = new Dictionary<string, List<float[]>>();
        int nextSpeakerId = 0;

        foreach (var (segment, embedding) in embeddings)
        {
            string? assignedCluster = null;
            double maxSimilarity = 0;

            // Find best matching cluster
            foreach (var (clusterId, clusterEmbeddings) in clusters)
            {
                // Calculate average similarity to cluster
                var avgSimilarity = clusterEmbeddings
                    .Select(e => _embeddingService.CalculateSimilarity(embedding, e))
                    .Average();

                if (avgSimilarity > maxSimilarity)
                {
                    maxSimilarity = avgSimilarity;
                    assignedCluster = clusterId;
                }
            }

            // Assign to cluster or create new one
            if (maxSimilarity >= SimilarityThreshold && assignedCluster != null)
            {
                clusters[assignedCluster].Add(embedding);
            }
            else
            {
                var newClusterId = $"SPEAKER_{nextSpeakerId:D2}";
                clusters[newClusterId] = new List<float[]> { embedding };
                nextSpeakerId++;
            }
        }

        return clusters;
    }
}
```

**Example output:**
```
Input: podcast.mp3 (15 minutes, 2 speakers)

Processing:
  Step 1 (VAD): Detected 47 speech segments (0.5s to 45s each)
  Step 2 (Embeddings): Extracted 47 × 512-dim ECAPA-TDNN embeddings
  Step 3 (Clustering): 47 embeddings → 2 clusters (similarity threshold 0.75)
  Step 4 (Turns): Created 47 speaker turns
  Step 5 (Merge): 47 turns → 23 merged turns (consecutive same-speaker segments)

Output:
  SpeakerCount: 2
  Turns: 23
    - SPEAKER_00: 0.0s - 5.2s (confidence: 1.0)
    - SPEAKER_01: 5.2s - 12.8s (confidence: 1.0)
    - SPEAKER_00: 12.8s - 18.3s (confidence: 1.0)
    - ...

Signals emitted:
  speaker.count = 2
  speaker.classification = "two_speakers"
  speaker.turn_count = 23
  speaker.diarization_method = "agglomerative_clustering"
  speaker.avg_turn_duration = 39.1 seconds
  speaker.diarization_confidence = 1.0
```

**Why this works:**
- VAD is deterministic (RMS threshold)
- ECAPA-TDNN embeddings are deterministic (ONNX model, no sampling)
- Clustering threshold is configurable (0.75 cosine similarity)
- No Python runtime required (pure .NET)
- Runs offline (no API calls)

---

## Voice Embeddings: Anonymous Speaker Similarity

ECAPA-TDNN ONNX model extracts 512-dimensional speaker embeddings for similarity detection.

### The Privacy Model

**What we DON'T do:**
- ❌ Infer speaker names
- ❌ Guess demographics (age, gender, accent)
- ❌ Make cultural assertions
- ❌ Store raw audio

**What we DO:**
- ✓ Generate anonymous voiceprint IDs (`vprint:abc123...`)
- ✓ Calculate speaker similarity (cosine distance)
- ✓ Enable "find similar speakers" queries
- ✓ Preserve privacy (embeddings not feasibly invertible, but treat as sensitive data)

### Implementation

```csharp
public class VoiceEmbeddingService
{
    private readonly ILogger<VoiceEmbeddingService> _logger;
    private readonly VoiceEmbeddingModelDownloader _modelDownloader;
    private readonly string _modelPath;
    private InferenceSession? _session;

    public async Task<VoiceEmbedding> ExtractEmbeddingAsync(
        string audioPath,
        CancellationToken ct = default)
    {
        await EnsureModelLoadedAsync(ct);

        // Extract Mel spectrogram features (80 Mel bands)
        var features = await Task.Run(() => ExtractMelSpectrogram(audioPath), ct);

        // Flatten 2D array to 1D for DenseTensor
        int numMelBands = features.GetLength(0);
        int numFrames = features.GetLength(1);
        var flatFeatures = new float[numMelBands * numFrames];
        for (int i = 0; i < numMelBands; i++)
        {
            for (int j = 0; j < numFrames; j++)
            {
                flatFeatures[i * numFrames + j] = features[i, j];
            }
        }

        // Run ONNX inference (ECAPA-TDNN model)
        var inputTensor = new DenseTensor<float>(flatFeatures, new[] { 1, numMelBands, numFrames });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = _session!.Run(inputs);
        var embeddingTensor = results.First().AsEnumerable<float>().ToArray();

        // Normalize embedding (L2 normalization)
        var embedding = NormalizeEmbedding(embeddingTensor);

        // Generate anonymous voiceprint ID (SHA-256 hash of embedding)
        var voiceprintId = GenerateVoiceprintId(embedding);

        return new VoiceEmbedding
        {
            Vector = embedding,
            VoiceprintId = voiceprintId,
            Dimension = embedding.Length,
            Model = "ecapa-tdnn"
        };
    }

    // Calculate cosine similarity between two voice embeddings
    public virtual double CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have same dimension");

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    private string GenerateVoiceprintId(float[] embedding)
    {
        // Generate anonymous ID from embedding hash
        // This ensures same voice → same ID, but no PII
        //
        // Stability notes:
        // - Deterministic on same runtime/endianness/float serialization
        // - Voiceprint IDs are stable for a given model + preprocessing version
        // - Treat as versioned identifiers - regenerate if model changes
        // - Cross-platform determinism depends on float serialization consistency
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        var hash = sha256.ComputeHash(bytes);

        return "vprint:" + Convert.ToHexString(hash).Substring(0, 16).ToLower();
    }
}
```

**Example usage:**
```csharp
// Extract embedding from two audio files
var embedding1 = await voiceEmbeddingService.ExtractEmbeddingAsync("speaker1.wav");
var embedding2 = await voiceEmbeddingService.ExtractEmbeddingAsync("speaker2.wav");

// Calculate similarity
var similarity = voiceEmbeddingService.CalculateSimilarity(
    embedding1.Vector,
    embedding2.Vector
);

// Output:
// embedding1.VoiceprintId = "vprint:a3f9c2e1d4b8"
// embedding2.VoiceprintId = "vprint:7e2d8f1a9c3b"
// similarity = 0.91  (high similarity, likely same speaker)
```

**Anonymous speaker search:**
```csharp
// Find audio files with similar speakers
var queryEmbedding = await voiceEmbeddingService.ExtractEmbeddingAsync("query.wav");

var similarSpeakers = audioDatabase
    .Where(audio => audio.VoiceEmbedding != null)
    .Select(audio => new
    {
        Audio = audio,
        Similarity = voiceEmbeddingService.CalculateSimilarity(
            queryEmbedding.Vector,
            audio.VoiceEmbedding
        )
    })
    .Where(x => x.Similarity > 0.75)  // Threshold for "similar"
    .OrderByDescending(x => x.Similarity)
    .Take(10)
    .ToList();

// Results:
// 1. podcast_ep12.mp3 (similarity: 0.94) - likely same speaker
// 2. podcast_ep15.mp3 (similarity: 0.89) - likely same speaker
// 3. interview_2024.wav (similarity: 0.76) - possibly same speaker
```

**Why anonymous?**
- Voiceprint ID is a hash of the embedding (one-way function)
- Embedding is 512 dimensions → cannot infer speaker name
- Similarity score is relative (no absolute "this is John Smith")
- Privacy-preserving (no PII stored)

---

## Audio Segment Extraction: Evidence Playback

Extract time-range audio clips for speaker diarization evidence.

### The Problem

Diarization tells you "SPEAKER_00 spoke from 5.2s to 12.8s" but provides no way to verify this claim.

**Solution:** Extract 2-second audio clips from the first turn of each speaker, encode as Base64, and store as signals.

### Implementation

```csharp
public class AudioSegmentExtractor
{
    private readonly ILogger<AudioSegmentExtractor> _logger;

    // Extract speaker sample clips from diarization result
    public virtual async Task<Dictionary<string, string>> ExtractSpeakerSamplesAsync(
        string audioPath,
        IEnumerable<SpeakerTurn> turns,
        double sampleDurationSeconds = 2.0,
        CancellationToken ct = default)
    {
        var samples = new Dictionary<string, string>();

        // Group turns by speaker and take first turn for each
        var speakerFirstTurns = turns
            .GroupBy(t => t.SpeakerId)
            .Select(g => g.OrderBy(t => t.StartSeconds).First())
            .ToList();

        foreach (var turn in speakerFirstTurns)
        {
            try
            {
                // Extract sample from start of first turn
                var sampleEnd = Math.Min(turn.EndSeconds, turn.StartSeconds + sampleDurationSeconds);
                var base64 = await ExtractSegmentAsBase64Async(
                    audioPath,
                    turn.StartSeconds,
                    sampleEnd,
                    sampleDurationSeconds,
                    ct);

                samples[turn.SpeakerId] = base64;

                _logger.LogDebug("Extracted {Duration:F2}s sample for {SpeakerId}",
                    sampleEnd - turn.StartSeconds, turn.SpeakerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract sample for {SpeakerId}", turn.SpeakerId);
            }
        }

        return samples;
    }

    // Extract segment and return as Base64-encoded WAV
    public async Task<string> ExtractSegmentAsBase64Async(
        string audioPath,
        double startSeconds,
        double endSeconds,
        double maxDurationSeconds = 3.0,
        CancellationToken ct = default)
    {
        var wavBytes = await ExtractSegmentAsWavBytesAsync(
            audioPath,
            startSeconds,
            endSeconds,
            maxDurationSeconds,
            ct);

        return Convert.ToBase64String(wavBytes);
    }

    // Core extraction logic
    private async Task ExtractSegmentToStreamAsync(
        string audioPath,
        double startSeconds,
        double endSeconds,
        Stream outputStream,
        CancellationToken ct)
    {
        using var reader = new AudioFileReader(audioPath);

        // Calculate byte positions
        var startPosition = (long)(startSeconds * reader.WaveFormat.AverageBytesPerSecond);
        var endPosition = (long)(endSeconds * reader.WaveFormat.AverageBytesPerSecond);

        // Align to block boundaries
        startPosition -= startPosition % reader.WaveFormat.BlockAlign;
        endPosition -= endPosition % reader.WaveFormat.BlockAlign;

        // Clamp to file boundaries
        startPosition = Math.Max(0, startPosition);
        endPosition = Math.Min(reader.Length, endPosition);

        var segmentLength = endPosition - startPosition;

        // Seek to start position
        reader.Position = startPosition;

        // Create trimmed reader
        // AudioFileReader outputs decoded PCM samples; TrimmedStream creates a windowed view
        // WaveFileReader wraps it for format conversion pipeline (works for MP3/FLAC/WAV)
        using var trimmedReader = new WaveFileReader(new TrimmedStream(reader, segmentLength));

        // Convert to 16-bit PCM WAV for maximum compatibility
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(trimmedReader);
        using var resampler = new MediaFoundationResampler(pcmStream, new WaveFormat(16000, 16, 1))
        {
            ResamplerQuality = 60  // High quality
        };

        // Write to output as WAV
        await Task.Run(() => WaveFileWriter.WriteWavFileToStream(outputStream, resampler), ct);
    }
}
```

**Example integration in SpeakerDiarizationWave:**
```csharp
// Extract speaker sample audio clips (2 second clips from first turn of each speaker)
try
{
    _logger.LogDebug("Extracting speaker samples from {AudioPath}", audioPath);

    var speakerSamples = await _segmentExtractor.ExtractSpeakerSamplesAsync(
        audioPath,
        result.Turns,
        sampleDurationSeconds: 2.0,
        cancellationToken);

    // Emit speaker samples as signals (Base64-encoded WAV)
    foreach (var (speakerId, base64Wav) in speakerSamples)
    {
        signals.Add(new Signal
        {
            Name = $"speaker.sample.{speakerId.ToLowerInvariant()}",
            Value = base64Wav,
            Type = SignalType.Embedding,  // Using Embedding type for binary data
            Source = Name
        });

        _logger.LogDebug("Added audio sample for {SpeakerId} ({SizeKB} KB)",
            speakerId,
            Math.Round(base64Wav.Length / 1024.0, 1));
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to extract speaker samples: {Message}", ex.Message);
}
```

**Example output:**
```
Input: podcast.mp3 (2 speakers detected)

Extraction:
  SPEAKER_00: 0.0s - 2.0s (first turn 0.0s - 5.2s)
  SPEAKER_01: 5.2s - 7.2s (first turn 5.2s - 12.8s)

Signals emitted:
  speaker.sample.speaker_00 = "UklGRiQAAABXQVZFZm10..." (Base64 WAV, 31.2 KB)
  speaker.sample.speaker_01 = "UklGRiQAAABXQVZFZm10..." (Base64 WAV, 29.8 KB)

UI integration:
  <audio controls>
    <source src="data:audio/wav;base64,{base64Wav}" type="audio/wav">
  </audio>
```

**Why this matters:**
- Evidence playback: Users can hear each speaker sample
- Verification: Confirms diarization accuracy
- Debugging: Identifies misclassified segments
- Privacy: Only 2-second samples (not full audio)

---

## Integration with lucidRAG

AudioSummarizer integrates seamlessly with lucidRAG's document processing pipeline via a **two-stage reduction**:

1. **Stage 1 (AudioSummarizer)**: Reduce audio file to acoustic signature + transcript
   - Signals: RMS, spectral features, speaker embeddings, diarization turns
   - Evidence: Speaker sample clips, full transcript text
   - Output: Markdown summary + signal JSON

2. **Stage 2 (DocSummarizer)**: Reduce transcript to semantic signature
   - Process transcript through DocSummarizer pipeline
   - Extract entities (people, organizations, topics)
   - Generate embeddings for semantic search
   - Build knowledge graph links
   - Output: Entity graph + chunked embeddings

This **cascading reduction** enables queries like:
- "Find audio mentioning quantum computing" → DocSummarizer entities from transcript
- "Find podcasts with similar speakers" → AudioSummarizer voice embeddings
- "Show low-quality recordings about AI" → Combine acoustic signals + semantic entities

### Document Handler Registration

```csharp
// In LucidRAG.Core, register audio handler
services.AddSingleton<IDocumentHandler, AudioDocumentHandler>();

// AudioDocumentHandler.cs
public class AudioDocumentHandler : IDocumentHandler
{
    private readonly AudioWaveOrchestrator _orchestrator;
    private readonly IDocumentProcessingService _docProcessingService;

    public bool CanHandle(string filePath, string mimeType)
    {
        var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg" };
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return supportedExtensions.Contains(extension);
    }

    public async Task<DocumentContent> ProcessAsync(
        string filePath,
        ProcessingOptions options,
        CancellationToken ct)
    {
        // STAGE 1: Run audio wave pipeline (acoustic + transcript extraction)
        var profile = await _orchestrator.AnalyzeAsync(filePath, ct);

        // Extract transcript text
        var transcriptText = profile.GetValue<string>("transcription.full_text");

        // STAGE 2: Pass transcript through DocSummarizer pipeline (entity extraction)
        DocumentContent? transcriptContent = null;
        if (!string.IsNullOrEmpty(transcriptText) && options.ExtractEntities)
        {
            // Create temporary markdown file from transcript
            // NOTE: Temp file approach is demo code for CLI integration example.
            // Production should use in-memory pipeline API to avoid privacy leaks from temp files.
            var tempTranscript = Path.GetTempFileName() + ".md";
            await File.WriteAllTextAsync(tempTranscript, transcriptText, ct);

            try
            {
                // Process transcript through DocSummarizer
                transcriptContent = await _docProcessingService.ProcessDocumentAsync(
                    tempTranscript,
                    new ProcessingOptions
                    {
                        ExtractEntities = true,
                        GenerateEmbeddings = true
                    },
                    ct);
            }
            finally
            {
                File.Delete(tempTranscript);
            }
        }

        // Convert audio signals to markdown
        var audioMarkdown = ConvertToMarkdown(profile);

        // Merge audio signals + transcript entities
        var finalMarkdown = audioMarkdown;
        if (transcriptContent != null)
        {
            finalMarkdown += "\n\n---\n\n";
            finalMarkdown += "## Transcript Analysis (DocSummarizer)\n\n";
            finalMarkdown += transcriptContent.MainContent;
        }

        return new DocumentContent
        {
            MainContent = finalMarkdown,
            Title = Path.GetFileNameWithoutExtension(filePath),
            ContentType = "audio/summary",
            Entities = transcriptContent?.Entities ?? new List<ExtractedEntity>(),
            Embeddings = transcriptContent?.Embeddings ?? Array.Empty<float[]>()
        };
    }

    private string ConvertToMarkdown(DynamicAudioProfile profile)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Audio Analysis: {profile.GetValue<string>("identity.filename")}");
        sb.AppendLine();

        // Acoustic properties
        sb.AppendLine("## Acoustic Profile");
        sb.AppendLine($"- Duration: {profile.GetValue<double>("audio.duration_seconds"):F1} seconds");
        sb.AppendLine($"- Format: {profile.GetValue<string>("audio.format")}");
        sb.AppendLine($"- Sample Rate: {profile.GetValue<int>("audio.sample_rate")} Hz");
        sb.AppendLine($"- Channels: {profile.GetValue<string>("audio.channel_layout")}");
        sb.AppendLine($"- RMS Loudness: {profile.GetValue<double>("audio.rms_db"):F1} dB");
        sb.AppendLine($"- Dynamic Range: {profile.GetValue<double>("audio.dynamic_range_db"):F1} dB");
        sb.AppendLine();

        // Content classification
        sb.AppendLine("## Content Classification");
        sb.AppendLine($"- Type: {profile.GetValue<string>("audio.content_type")}");
        sb.AppendLine();

        // Transcription (if available)
        var transcriptText = profile.GetValue<string>("transcription.full_text");
        if (!string.IsNullOrEmpty(transcriptText))
        {
            sb.AppendLine("## Transcript");
            sb.AppendLine(transcriptText);
            sb.AppendLine();
        }

        // Speaker diarization (if available)
        var speakerCount = profile.GetValue<int?>("speaker.count");
        if (speakerCount.HasValue && speakerCount > 0)
        {
            sb.AppendLine("## Speaker Diarization");
            sb.AppendLine($"- Speakers Detected: {speakerCount}");
            sb.AppendLine($"- Classification: {profile.GetValue<string>("speaker.classification")}");
            sb.AppendLine($"- Turn Count: {profile.GetValue<int>("speaker.turn_count")}");

            var turnsJson = profile.GetValue<string>("speaker.turns");
            if (!string.IsNullOrEmpty(turnsJson))
            {
                sb.AppendLine();
                sb.AppendLine("### Speaker Turns");
                sb.AppendLine("```json");
                sb.AppendLine(turnsJson);
                sb.AppendLine("```");
            }
        }

        return sb.ToString();
    }
}
```

### CLI Integration (Coming Soon)

When AudioSummarizer is integrated into lucidRAG CLI (upcoming release), you'll be able to process audio files like this:

```bash
# Process audio file (auto-detects audio type)
dotnet run --project src/LucidRAG.Cli -- process podcast.mp3

# Expected output:
# Processing: podcast.mp3
# ├─ IdentityWave (100ms): SHA-256, duration, format
# ├─ AcousticProfileWave (142ms): RMS, spectral features
# ├─ ContentClassifierWave (89ms): Detected speech content
# ├─ TranscriptionWave (12.3s): Whisper.NET transcription
# ├─ SpeakerDiarizationWave (3.8s): 2 speakers, 23 turns
# └─ VoiceEmbeddingWave (178ms): ECAPA-TDNN embeddings
#
# Signals emitted: 47
# Processing time: 16.6s
# Saved to database: doc_abc123
#
# Acoustic Profile:
#   Duration: 900.0s (15m 0s)
#   Format: mp3, 44100 Hz, stereo
#   RMS: -18.2 dB, Dynamic Range: 22.1 dB
#   Content: speech (confidence: 0.85)
#
# Speaker Diarization:
#   Speakers: 2 (two_speakers)
#   Turns: 23
#   SPEAKER_00: 52% participation (468s)
#   SPEAKER_01: 48% participation (432s)
#
# Transcription: Available (12,847 characters)

# Batch process audio directory
dotnet run --project src/LucidRAG.Cli -- process ./audio/*.mp3 --verbose

# Extract entities from audio (via transcript)
dotnet run --project src/LucidRAG.Cli -- process interview.wav --extract-entities

# Export signals as JSON for inspection
dotnet run --project src/LucidRAG.Cli -- process podcast.mp3 --export-signals signals.json
```

**Example signals.json output:**
```json
{
  "audio.hash.sha256": "a3f9c2e1d4b8...",
  "audio.duration_seconds": 900.0,
  "audio.format": "mp3",
  "audio.sample_rate": 44100,
  "audio.channels": 2,
  "audio.channel_layout": "stereo",
  "audio.rms_db": -18.2,
  "audio.dynamic_range_db": 22.1,
  "audio.spectral_centroid_hz": 2418.3,
  "audio.content_type": "speech",
  "speaker.count": 2,
  "speaker.classification": "two_speakers",
  "speaker.turn_count": 23,
  "speaker.diarization_confidence": 1.0,
  "speaker.participation": {
    "SPEAKER_00": 52.0,
    "SPEAKER_01": 48.0
  },
  "transcription.full_text": "...",
  "transcription.confidence": 0.87,
  "speaker.sample.speaker_00": "UklGRiQAAABXQVZF..."
}
```

### Evidence Storage

```csharp
// Store speaker samples as evidence artifacts
foreach (var (speakerId, base64Wav) in speakerSamples)
{
    var evidence = new EvidenceArtifact
    {
        Id = Guid.NewGuid(),
        DocumentId = documentEntity.Id,
        Type = EvidenceTypes.SpeakerSample,
        Content = base64Wav,  // Base64-encoded WAV
        Metadata = new SpeakerSampleMetadata
        {
            SpeakerId = speakerId,
            StartSeconds = turn.StartSeconds,
            EndSeconds = turn.EndSeconds,
            SampleRate = 16000,
            Channels = 1,
            Format = "wav"
        }
    };

    await evidenceRepository.SaveAsync(evidence);
}

// Store full signal dump as evidence
var signalDump = JsonSerializer.Serialize(profile.GetAllSignals(), new JsonSerializerOptions
{
    WriteIndented = true
});

await evidenceRepository.SaveAsync(new EvidenceArtifact
{
    Id = Guid.NewGuid(),
    DocumentId = documentEntity.Id,
    Type = EvidenceTypes.AudioSignals,
    Content = signalDump,
    Metadata = new AudioSignalsMetadata
    {
        SignalCount = profile.GetAllSignals().Count(),
        WavesExecuted = profile.GetExecutedWaves().ToList(),
        ProcessingSeconds = profile.GetValue<double>("processing_time_seconds"),
        HasFingerprint = profile.HasSignal("audio.fingerprint.hash"),
        HasTranscription = profile.HasSignal("transcription.full_text"),
        HasDiarization = profile.HasSignal("speaker.count"),
        ContentType = profile.GetValue<string>("audio.content_type")
    }
});
```

---

## Configuration

Full configuration example:

```json
{
  "AudioSummarizer": {
    "EnableSpeakerDiarization": true,
    "EnableTranscription": true,
    "EnableVoiceEmbeddings": true,
    "SupportedFormats": [".mp3", ".wav", ".m4a", ".flac", ".ogg", ".wma", ".aac"],
    "MaxFileSizeMB": 500,

    "VoiceEmbedding": {
      "ModelPath": "./models/voxceleb_ECAPA512_LM.onnx",
      "MinDurationSeconds": 1.0,
      "AutoDownload": true
    },

    "Transcription": {
      "Backend": "Whisper",
      "Whisper": {
        "ModelPath": "./models/whisper-base.en.bin",
        "ModelSize": "base",
        "Language": "en",
        "UseGpu": false
      },
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "Model": "whisper"
      }
    },

    "AcousticProfiling": {
      "EnableSpectralAnalysis": true,
      "FftSize": 2048,
      "VadThreshold": 0.02
    },

    "Fingerprinting": {
      "Provider": "PureNet",
      "EnableDeduplication": true
    }
  }
}
```

---

## Performance Characteristics

| Wave | Priority | Speed | Deterministic | Requires |
|------|----------|-------|---------------|----------|
| IdentityWave | 100 | ~50ms | Yes | NAudio |
| FingerprintWave | 90 | ~200ms | Yes | FftSharp |
| AcousticProfileWave | 80 | ~150ms | Yes | NAudio, FftSharp |
| ContentClassifierWave | 70 | ~100ms | Heuristic | - |
| TranscriptionWave | 65 | ~5-15s | No | Whisper.NET or Ollama |
| SpeakerDiarizationWave | 60 | ~2-8s | Clustering | ECAPA-TDNN ONNX |
| VoiceEmbeddingWave | 30 | ~200ms | Yes | ECAPA-TDNN ONNX |

**Example timings (15-minute podcast, 2 speakers):**
```
IdentityWave: 45ms (SHA-256 + metadata)
AcousticProfileWave: 132ms (FFT + spectral analysis)
ContentClassifierWave: 87ms (ZCR + spectral flux)
TranscriptionWave: 12.3s (Whisper.NET base model)
SpeakerDiarizationWave: 3.8s (VAD + embeddings + clustering)
VoiceEmbeddingWave: 178ms (ECAPA-TDNN inference)

Total: ~16.6 seconds
```

**Offline mode (no transcription):**
```
IdentityWave → VoiceEmbeddingWave: ~600ms
```

---

## Conclusion

AudioSummarizer proves that the **[Reduced RAG pattern](https://www.mostlylucid.net/blog/reduced-rag)** works for audio: reduce once to signals + evidence, query against structured facts, synthesize with bounded LLM input.

**Key insights:**

1. **Signal extraction over summaries**: RMS, spectral features, speaker embeddings—not LLM-generated descriptions
2. **Evidence is auditable**: Speaker samples (Base64 WAV), transcripts, diarization turns—not opaque chunks
3. **Query-time filters**: Database WHERE clauses eliminate 95% of audio before the LLM sees anything
4. **Context window reduction**: 5 audio summaries instead of 500 raw chunks → ~50× smaller
5. **Pure .NET diarization**: Agglomerative clustering + ECAPA-TDNN = no Python, no pyannote
6. **Privacy by design**: Anonymous voiceprints (hash-based IDs), no speaker naming, no cultural assertions

**The Reduced RAG pattern for audio:**
```
Ingestion:  Audio file → Wave pipeline → Signals (JSON) + Evidence (transcript, samples)
Storage:    Signals (indexed) + Embeddings (speaker similarity) + Evidence (artifacts)
Query:      Filter (SQL) → Search (BM25 + vector) → Synthesize (LLM, ~5 results)
```

This is **Constrained Fuzziness** composed with **Reduced RAG**: deterministic signals constrain probabilistic synthesis, and the LLM operates on pre-computed evidence—not raw audio.

The pattern scales across all lucidRAG modalities:
- **[DocSummarizer](/blog/building-a-document-summarizer-with-rag)** - Documents (entity extraction, knowledge graphs, semantic chunking)
- **[ImageSummarizer](/blog/constrained-fuzzy-image-intelligence)** - Images (22-wave visual intelligence, OCR, object detection)
- **[DataSummarizer](/blog/datasummarizer-how-it-works)** - Data (schema inference, quality profiling, type detection)
- **AudioSummarizer** (this article) - Audio (acoustic profiling, speaker diarization, transcription)

All implementing the same core **[Reduced RAG pattern](/blog/reduced-rag)**: signals + evidence + query-time synthesis.

---

## Resources

### lucidRAG Documentation
- **[AudioSummarizer Library](https://github.com/scottgal/lucidrag/tree/main/src/AudioSummarizer.Core)** - Source code
- **[Architecture Guide](https://github.com/scottgal/lucidrag/blob/main/src/AudioSummarizer.Core/docs/architecture.md)** - Waves, signals, pipeline
- **[Signals Reference](https://github.com/scottgal/lucidrag/blob/main/src/AudioSummarizer.Core/docs/signals.md)** - Complete signal catalog

### ONNX Models
- **[ECAPA-TDNN on HuggingFace](https://huggingface.co/Wespeaker/wespeaker-ecapa-tdnn512-LM)** - Voice embedding model
- **[Whisper.NET](https://github.com/sandrohanea/whisper.net)** - C# wrapper for Whisper.cpp
- **[ONNX Runtime Documentation](https://onnxruntime.ai/docs/get-started/with-csharp.html)**

### Research Papers
- **[ECAPA-TDNN Paper](https://arxiv.org/abs/2005.07143)** - Speaker verification architecture
- **[Whisper Paper](https://arxiv.org/abs/2212.04356)** - OpenAI's speech recognition
- **[PyAnnote 3.1](https://huggingface.co/pyannote/speaker-diarization-3.1)** - Reference implementation (Python)

### Related Articles

**Core Patterns:**
- **[Reduced RAG](https://www.mostlylucid.net/blog/reduced-rag)** - The core pattern (signals + evidence + query-time synthesis)
- **[Constrained Fuzziness Pattern](/blog/constrained-fuzziness-pattern)** - Foundational pattern

**Reduced RAG Implementations:**
- **[DocSummarizer](/blog/building-a-document-summarizer-with-rag)** - Document RAG with entity extraction and knowledge graphs
- **[DataSummarizer](/blog/datasummarizer-how-it-works)** - Data profiling and schema inference RAG
- **[ImageSummarizer: Image Intelligence](/blog/constrained-fuzzy-image-intelligence)** - Image RAG with 22-wave visual intelligence pipeline
  - **[Constrained Fuzzy OCR](/blog/constrained-fuzzy-image-ocr-pipeline)** - Three-tier OCR (local → ONNX → LLM)
- **AudioSummarizer (this article)** - Audio forensic characterization with speaker diarization

---

## The Series

| Part | Pattern | Focus |
|------|---------|-------|
| 1 | [Constrained Fuzziness](/blog/constrained-fuzziness-pattern) | Single component |
| 2 | [Constrained Fuzzy MoM](/blog/constrained-mom-mixture-of-models) | Multiple components |
| 3 | [Context Dragging](/blog/constrained-fuzzy-context-dragging) | Time / memory |
| 4 | [Image Intelligence](/blog/constrained-fuzzy-image-intelligence) | Wave architecture, 22 waves |
| 4.1 | [Three-Tier OCR Pipeline](/blog/constrained-fuzzy-image-ocr-pipeline) | OCR, ONNX models, filmstrips |
| **4.2** | **AudioSummarizer (this article)** | **Forensic audio, speaker diarization** |

**Next**: Multi-modal graph RAG with lucidRAG—composing DocSummarizer, ImageSummarizer, AudioSummarizer, and DataSummarizer into a unified knowledge graph.

All parts follow the same invariant: **probabilistic components propose; deterministic systems persist**.
