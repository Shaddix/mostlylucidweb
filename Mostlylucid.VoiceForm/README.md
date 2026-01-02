# Mostlylucid.VoiceForm

A **Ten Commandments compliant** local-first voice-to-form demo demonstrating:

- **Speech as lossy input** - STT is treated as a potentially incorrect input device, not truth
- **LLM as translator, not controller** - The LLM extracts and normalizes values, but never controls flow
- **Deterministic state machine** - All transitions are defined in code; same inputs = same outputs
- **Rules-based confirmation** - Policy decides when to confirm, not LLM judgment
- **Fully local stack** - whisper.cpp + Ollama, no cloud dependencies

## Quick Start

### 1. Start the local services

```bash
cd Mostlylucid.VoiceForm
docker-compose up -d
```

### 2. Pull the Ollama model

```bash
docker exec -it ollama ollama pull llama3.2:3b
```

### 3. Run the application

```bash
dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in console).

## Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ Browser/Mic  │────▶│  Whisper STT │────▶│ Transcript   │
└──────────────┘     └──────────────┘     └──────┬───────┘
                                                  │
                                                  ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ Confirmation │◀────│   Validator  │◀────│ Ollama LLM   │
│    Policy    │     │ (in code)    │     │ (extract)    │
└──────┬───────┘     └──────────────┘     └──────────────┘
       │
       ▼
┌──────────────┐     ┌──────────────┐
│ State Machine│────▶│ Event Log    │
│ (transitions)│     │ (SQLite)     │
└──────────────┘     └──────────────┘
```

## Key Principles

### LLM Contract

The LLM receives ONLY:
- Current field definition (id, type, constraints, examples)
- Current prompt (what we asked)
- Transcript text

And returns strict JSON:
```json
{
  "fieldId": "dateOfBirth",
  "value": "1984-05-12",
  "confidence": 0.91,
  "needsConfirmation": true,
  "reason": "Date parsed from natural language"
}
```

The LLM does **NOT**:
- Pick the next field
- Decide completion
- Submit anything
- Mark anything "valid"

### Confirmation Policy

Confirmation is a **deterministic rule**, not LLM judgment:

- Always confirm: email, date, consent fields (configured per-field)
- Confirm if STT confidence < threshold
- Confirm if extraction confidence < threshold
- Confirm if natural language parsing was involved

### State Machine

All transitions are defined in code:

```
Pending → InProgress (StartCapture)
InProgress → AwaitingConfirmation (ExtractionSuccess)
InProgress → InProgress (ExtractionFailed, retry)
InProgress → Confirmed (AutoConfirm, high confidence)
AwaitingConfirmation → Confirmed (UserConfirmed)
AwaitingConfirmation → InProgress (UserRejected, retry)
```

### Event Log

Every step is logged for audit and replay:
- Transcript received (with confidence)
- Extraction attempted (with result)
- Field confirmed/rejected
- Form completed

## Project Structure

```
Mostlylucid.VoiceForm/
├── Config/                    # Configuration classes
├── Models/
│   ├── FormSchema/           # Form definition models
│   ├── State/                # Session state models
│   ├── Events/               # Audit event models
│   └── Extraction/           # LLM I/O models
├── Services/
│   ├── Stt/                  # Speech-to-text (Whisper)
│   ├── Extraction/           # Field extraction (Ollama)
│   ├── Validation/           # Deterministic validators
│   ├── Confirmation/         # Rules-based confirmation
│   ├── StateMachine/         # Form state transitions
│   ├── EventLog/             # SQLite audit log
│   └── Orchestration/        # Workflow coordinator
├── Components/               # Blazor components
└── Data/SampleForms/         # JSON form definitions
```

## Configuration

See `appsettings.json`:

```json
{
  "VoiceForm": {
    "FormsPath": "Data/SampleForms",
    "EventLogDbPath": "data/voiceform-events.db",
    "DefaultConfidenceThreshold": 0.85,
    "Whisper": {
      "BaseUrl": "http://localhost:9000",
      "TimeoutSeconds": 60
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.2:3b",
      "Temperature": 0.1
    }
  }
}
```

## Adding New Forms

Create a JSON file in `Data/SampleForms/`:

```json
{
  "id": "my-form",
  "name": "My Custom Form",
  "fields": [
    {
      "id": "fieldName",
      "label": "Field Label",
      "prompt": "What is your field value?",
      "type": "Text",
      "required": true
    }
  ]
}
```

Field types: `Text`, `Date`, `Number`, `Email`, `Phone`, `Choice`, `Boolean`

## Why Local Models Are Enough

Because the model isn't doing the job of the system.

The LLM's only job is translation: convert natural language to typed values. The actual work—validation, flow control, confirmation decisions, state management—happens in deterministic code.

A 3B parameter model is sufficient for:
- "January fifteenth nineteen eighty five" → "1985-01-15"
- "john dot smith at gmail dot com" → "john.smith@gmail.com"
- "five five five one two three four" → "5551234"

The model doesn't need to be smart. It needs to be predictable. When it's wrong, the deterministic confirmation policy catches it.
