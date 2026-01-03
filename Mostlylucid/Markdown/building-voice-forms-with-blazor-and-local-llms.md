# Deterministic Voice Forms with Blazor and Local LLMs
## A Ten Commandments–Compliant Architecture

<datetime class="hidden">2026-01-05T14:30</datetime>

<!--category-- ASP.NET, Blazor, AI, LLM, Whisper, Ollama -->

**In which we build a voice-to-form system that doesn't hand the keys to the AI kingdom**

Here's the thing about voice interfaces: everyone wants them, nobody trusts them. And they're right not to.

Most voice forms fail the moment the model mishears a date or decides to "helpfully" skip ahead. You say "May 12th 1984" and the LLM extracts "May 2014" and advances to the next field before you can correct it. Or worse: it hallucinates confidence and auto-submits your form with wrong data.

The moment you let an LLM "control" your form flow, you've introduced a non-deterministic black box into what should be a predictable user experience.

This isn't hypothetical. [McDonald's ran AI-powered drive-thru ordering](/blog/tencommandments#mcdonalds-ibm-drive-thru-ai) across 100+ US locations. It routinely failed to understand basic modifications. They ended the partnership in 2024. The lesson: order-taking is a structured data problem-a finite menu, known modifiers, clear pricing. It's a form with speech recognition, not a reasoning task.

But what if we could have our cake and eat it too? Voice input for convenience, LLM for translation, but *deterministic code* for everything that actually matters?

That's what we're building today-a "Ten Commandments compliant" voice form system using Blazor Server, local Whisper for speech-to-text, and local Ollama for field extraction. No cloud dependencies. No unpredictable AI overlords. Just clean architecture.

[TOC]

## Applying the Ten Commandments

This project follows [The Ten Commandments of LLM Use](/blog/tencommandments). If you haven't read that yet, the short version: LLMs are probability engines, not decision-makers. They translate and classify; they don't control.

For voice forms, the relevant commandments are:

- **I. Don't let the LLM own state** - The state machine tracks form progress, not the model
- **II. Don't let the LLM cause side-effects** - It recommends extractions; code commits them
- **III. Separate causality from narration** - The LLM explains what it heard; validators decide if it's correct
- **IV. Use LLMs where probability is acceptable** - Extracting "May 12th" from speech is fuzzy; field ordering is not

### Voice Form Design Goals

Beyond the commandments, this system follows specific principles:

- **Speech is lossy input** - One way to populate fields, never the source of truth
- **Confirmation is policy-driven** - Thresholds in config, not LLM judgment
- **The user can always override** - Voice is convenience, not compulsion
- **Everything runs locally** - Whisper and Ollama, no cloud dependencies
- **Failure is graceful** - When voice fails, typing still works

Here's how this looks in practice:

```mermaid
flowchart LR
    A[User Speaks] --> B[Whisper STT]
    B --> C[Raw Transcript]
    C --> D[Ollama LLM]
    D --> E[Extracted Value]
    E --> F{Validation}
    F -->|Pass| G{Policy Check}
    F -->|Fail| H[Show Error]
    G -->|High Confidence| I[Auto-Confirm]
    G -->|Low Confidence| J[Ask User]
    J --> K{User Confirms?}
    K -->|Yes| L[State Machine: Next Field]
    K -->|No| M[Retry Recording]
    I --> L
    H --> M

    style D stroke:#f96,stroke-width:2px
    style F stroke:#6f6,stroke-width:2px
    style G stroke:#ff9,stroke-width:2px
```

## What This Doesn't Do

Let's be clear about scope:

- **No natural conversation** - This isn't a chatbot. One field at a time.
- **No adaptive questioning** - The form schema is static. The LLM doesn't improvise follow-ups.
- **No "assistant" personality** - The LLM extracts values, it doesn't make small talk.
- **No context across fields** - Each extraction is independent. The LLM doesn't remember your name when asking for email.

These constraints are features, not bugs. They make the system predictable, testable, and trustworthy.

If you want a conversational agent, build a conversational agent. If you want correct data, don't pretend conversation helps.

## Project Structure

We're building a standalone Blazor Server app. Here's the architecture:

```
Mostlylucid.VoiceForm/
├── Config/
│   └── VoiceFormConfig.cs         # Configuration binding
├── Models/
│   ├── FormSchema/                # Form definitions
│   ├── State/                     # Session state
│   └── Extraction/                # LLM input/output
├── Services/
│   ├── Stt/                       # Speech-to-text (Whisper)
│   ├── Extraction/                # Field extraction (Ollama)
│   ├── Validation/                # Type-based validators
│   ├── StateMachine/              # Form flow control
│   └── Orchestration/             # Coordinates everything
├── Components/
│   └── Pages/                     # Blazor UI
└── wwwroot/js/
    └── audio-recorder.js          # Web Audio API capture
```

**Dependency rule:** Services depend only on abstractions below them. Nothing upstream knows what happens downstream.

## Setting Up the Infrastructure

First, you'll need Whisper and Ollama running locally. Add these to your `devdeps-docker-compose.yml`:

```yaml
services:
  whisper:
    image: onerahmet/openai-whisper-asr-webservice:latest
    container_name: whisper-stt
    ports:
      - "9000:9000"
    environment:
      - ASR_MODEL=base.en
      - ASR_ENGINE=faster_whisper
    volumes:
      - whisper-models:/root/.cache/huggingface
    restart: unless-stopped

  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama-models:/root/.ollama
    restart: unless-stopped

volumes:
  whisper-models:
  ollama-models:
```

After spinning up, pull a model for Ollama:

```bash
docker exec -it ollama ollama pull llama3.2:3b
```

> **Why local?** Testability + determinism + data locality. CI spins up the same containers. Tests hit the same endpoints. No rate limits, no network variability.

**Note:** The `base.en` Whisper model is fast and accurate for English. For production, consider `small.en` or `medium.en` for better accuracy.

## The Form Schema: Declarative Form Definitions

Forms are defined in JSON. This is the single source of truth for what fields exist and how they behave:

```json
{
  "id": "customer-intake",
  "name": "Customer Intake Form",
  "fields": [
    {
      "id": "fullName",
      "label": "Full Name",
      "type": "Text",
      "prompt": "Please say your full name.",
      "required": true
    },
    {
      "id": "dateOfBirth",
      "label": "Date of Birth",
      "type": "Date",
      "prompt": "What is your date of birth?",
      "required": true,
      "confirmationPolicy": {
        "alwaysConfirm": true
      }
    },
    {
      "id": "email",
      "label": "Email Address",
      "type": "Email",
      "prompt": "What is your email address?",
      "required": true
    },
    {
      "id": "phone",
      "label": "Phone Number",
      "type": "Phone",
      "prompt": "What is your phone number?",
      "required": false
    },
    {
      "id": "notes",
      "label": "Additional Notes",
      "type": "Text",
      "prompt": "Any additional notes?",
      "required": false
    }
  ]
}
```

The C# models that represent this:

```csharp
public record FormDefinition(
    string Id,
    string Name,
    List<FieldDefinition> Fields);

public record FieldDefinition(
    string Id,
    string Label,
    FieldType Type,
    string Prompt,
    bool Required = true,
    ConfirmationPolicy? ConfirmationPolicy = null);

public enum FieldType
{
    Text,
    Date,
    Email,
    Phone,
    Choice  // Constrained vocabulary - especially important for voice
}

public record ConfirmationPolicy(
    bool AlwaysConfirm = false,
    double ConfidenceThreshold = 0.85);
```

Because the schema is declarative, **adding fields never requires touching the state machine**. Drop a new field definition in the JSON, and the form automatically includes it with proper flow control.

## The State Machine: Deterministic Form Flow

The anti-pattern we're avoiding is "LLM-driven navigation": advancing form state based on model output. This state machine explicitly forbids that.

**The state machine doesn't use AI.** It's pure C# logic that determines transitions based on explicit rules.

```csharp
public class FormStateMachine : IFormStateMachine
{
    private FormSession _session = null!;
    private int _currentFieldIndex;

    public FormSession StartSession(FormDefinition form)
    {
        _session = new FormSession
        {
            Id = Guid.NewGuid().ToString(),
            Form = form,
            Status = FormStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            FieldStates = form.Fields.ToDictionary(
                f => f.Id,
                f => new FieldState { FieldId = f.Id, Status = FieldStatus.Pending })
        };

        // First field starts in progress
        if (form.Fields.Count > 0)
        {
            _session.FieldStates[form.Fields[0].Id].Status = FieldStatus.InProgress;
        }

        return _session;
    }

    public FieldDefinition? GetCurrentField()
    {
        if (_currentFieldIndex >= _session.Form.Fields.Count)
            return null;

        return _session.Form.Fields[_currentFieldIndex];
    }
}
```

The state transitions are explicit and testable:

```csharp
public StateTransitionResult ProcessExtraction(
    ExtractionResponse extraction,
    ValidationResult validation)
{
    var currentField = GetCurrentField();
    if (currentField == null)
        return new StateTransitionResult(false, "No current field");

    var fieldState = _session.FieldStates[currentField.Id];
    fieldState.AttemptCount++;

    // Validation failed? Stay on current field
    if (!validation.IsValid)
    {
        return new StateTransitionResult(
            false,
            $"Validation failed: {validation.ErrorMessage}");
    }

    // Store the pending value
    fieldState.PendingValue = extraction.Value;
    fieldState.PendingConfidence = extraction.Confidence;

    // Check confirmation policy - this is rules, not AI
    var policy = currentField.ConfirmationPolicy
        ?? new ConfirmationPolicy();

    var needsConfirmation = policy.AlwaysConfirm
        || extraction.Confidence < policy.ConfidenceThreshold;

    if (needsConfirmation)
    {
        fieldState.Status = FieldStatus.AwaitingConfirmation;
        return new StateTransitionResult(
            true,
            "Please confirm this value",
            RequiresConfirmation: true);
    }

    // Auto-confirm high confidence values
    return ConfirmValue();
}
```

**Key point:** The `_currentFieldIndex` only advances on confirmation, never on extraction. This means a failed extraction or rejected confirmation keeps you on the same field. The user stays in control.

Notice the confirmation logic: it's a simple policy check, not LLM reasoning. High confidence + no `alwaysConfirm` = auto-confirm. Low confidence or sensitive field = ask the user.

## The LLM's Contract: Translation Only

The Ollama extractor has one job: turn messy human speech into structured field values.

If you can't write this interface without embarrassment, the LLM is doing too much:

```csharp
public interface IFieldExtractor
{
    Task<ExtractionResponse> ExtractAsync(
        ExtractionContext context,
        CancellationToken ct = default);
}

public record ExtractionContext(
    FieldDefinition Field,
    string Prompt,
    string Transcript);

public record ExtractionResponse(
    string FieldId,
    string? Value,
    double Confidence,
    bool NeedsConfirmation,  // Suggestion only - policy has final say
    string? Reason);
```

**Important:** The extractor may suggest `NeedsConfirmation: true`, but the confirmation policy always has the final say. The LLM's opinion is advisory, not authoritative.

And the implementation:

```csharp
public class OllamaFieldExtractor : IFieldExtractor
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public async Task<ExtractionResponse> ExtractAsync(
        ExtractionContext context,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(context.Field);
        var userPrompt = $"User said: \"{context.Transcript}\"";

        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            format = "json",
            stream = false,
            options = new { temperature = 0.1 }  // Low = deterministic
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/chat", request, ct);

        return ParseResponse(response, context.Field.Id);
    }
}
```

**Why temperature 0.1?** We want the LLM to be boring and predictable. Given the same transcript, it should extract the same value every time. Temperature 0.1 minimizes creative variation-exactly what we want for data extraction.

The system prompt is explicit about the LLM's limited role:

```csharp
private string BuildSystemPrompt(FieldDefinition field)
{
    return $"""
        You are a data extraction assistant. Extract the {field.Label}
        from the user's speech.

        Field type: {field.Type}

        Return JSON only:
        {{
          "fieldId": "{field.Id}",
          "value": "<extracted value or null>",
          "confidence": <0.0-1.0>,
          "needsConfirmation": <true/false>,
          "reason": "<brief explanation>"
        }}

        Rules:
        - For dates, output ISO format (YYYY-MM-DD)
        - For emails, output lowercase
        - For phones, output digits only
        - If you can't extract, set value to null
        - Be conservative with confidence scores

        DO NOT:
        - Ask follow-up questions
        - Suggest next steps
        - Make assumptions beyond the transcript
        """;
}
```

The LLM extracts, validates type format, and reports confidence. It doesn't decide what happens next.

## The Audio Recorder: Browser to WAV

The JavaScript side captures microphone audio and converts it to 16kHz mono WAV:

```javascript
window.voiceFormAudio = (function () {
    let mediaRecorder = null;
    let audioChunks = [];
    let audioContext = null;
    let dotNetRef = null;

    async function startRecording() {
        const stream = await navigator.mediaDevices
            .getUserMedia({ audio: true });

        audioContext = new AudioContext();
        audioChunks = [];

        const options = { mimeType: 'audio/webm;codecs=opus' };
        mediaRecorder = new MediaRecorder(stream, options);

        mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                audioChunks.push(event.data);
            }
        };

        mediaRecorder.onstop = async () => {
            stream.getTracks().forEach(track => track.stop());

            const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
            const wavBlob = await convertToWav(audioBlob);
            const wavBytes = await wavBlob.arrayBuffer();

            // Send to Blazor
            const uint8Array = new Uint8Array(wavBytes);
            await dotNetRef.invokeMethodAsync(
                'OnRecordingComplete',
                Array.from(uint8Array));
        };

        mediaRecorder.start(100);
    }

    return { initialize, startRecording, stopRecording };
})();
```

> **Why WAV?** Whisper is trained on 16kHz mono WAV. Same audio in → same bytes out → same transcript.

The `convertToWav` function handles resampling-I'll spare you the WAV header bit-twiddling, but it's in the repo.

## The Blazor UI: Two-Column Layout

The UI shows the current field prominently on the left, with all fields visible in a sidebar on the right. **The sidebar is not cosmetic-it's a trust boundary.** Users can see progress, state, and intent at all times.

```mermaid
graph LR
    subgraph "Left Panel"
        A[Current Field Prompt]
        B[Record Button]
        C[Transcript Display]
        D[Confirmation Dialog]
    end

    subgraph "Right Sidebar"
        E[Field 1: Full Name ✓]
        F[Field 2: DOB - Active]
        G[Field 3: Email - Pending]
        H[Field 4: Phone - Pending]
    end

    style F stroke:#36f,stroke-width:2px
    style E stroke:#6f6,stroke-width:2px
```

Here's the Blazor page structure:

```razor
@page "/voiceform/{FormId}"
@inject IFormOrchestrator Orchestrator
@rendermode InteractiveServer

<div class="top-bar">
    <h1>Voice Form</h1>
    <button class="theme-toggle" onclick="voiceFormTheme.toggle()">
        Dark Mode
    </button>
</div>

<main>
    <div class="voice-form-layout">
        <!-- Left: Active Field -->
        <div class="active-field-panel">
            @if (_currentField != null)
            {
                <div class="current-prompt">
                    <h2>@_currentField.Label</h2>
                    <p class="prompt-text">@_message</p>
                </div>

                <AudioRecorder OnAudioCaptured="HandleAudioCaptured"
                               IsRecording="_isRecording"
                               IsProcessing="_isProcessing" />

                @if (!string.IsNullOrEmpty(_transcript))
                {
                    <TranscriptDisplay Transcript="_transcript"
                                       Confidence="_transcriptConfidence" />
                }

                @if (_showConfirmation)
                {
                    <ConfirmationDialog ExtractedValue="_pendingValue"
                                        OnConfirm="HandleConfirm"
                                        OnReject="HandleReject" />
                }
            }
        </div>

        <!-- Right: Form Overview -->
        <div class="form-sidebar">
            <h3>@_session.Form.Name</h3>
            <div class="form-fields-list">
                @foreach (var field in _session.Form.Fields)
                {
                    var state = _session.GetFieldState(field.Id);
                    <div class="form-field-item @GetFieldClass(field, state)">
                        <div class="field-status-icon">
                            @GetStatusIcon(state.Status)
                        </div>
                        <div class="field-info">
                            <div class="field-name">@field.Label</div>
                            <div class="field-value">
                                @(state.Value ?? "Waiting")
                            </div>
                        </div>
                    </div>
                }
            </div>
        </div>
    </div>
</main>
```

## The Orchestrator: Bringing It All Together

The orchestrator coordinates the services. It has **no branching logic of its own**-it just calls services in sequence and passes results along:

```csharp
public class FormOrchestrator : IFormOrchestrator
{
    private readonly ISttService _sttService;
    private readonly IFieldExtractor _extractor;
    private readonly IFormValidator _validator;
    private readonly IFormStateMachine _stateMachine;
    private readonly IFormEventLog _eventLog;

    public async Task<ProcessingResult> ProcessAudioAsync(byte[] audioData)
    {
        var currentField = _stateMachine.GetCurrentField();
        if (currentField == null)
            return ProcessingResult.Error("No current field");

        // Step 1: Speech to text
        var sttResult = await _sttService.TranscribeAsync(audioData);
        await _eventLog.LogAsync(new TranscriptReceivedEvent(
            currentField.Id, sttResult.Transcript, sttResult.Confidence));

        // Step 2: Extract structured value
        var context = new ExtractionContext(
            currentField, currentField.Prompt, sttResult.Transcript);
        var extraction = await _extractor.ExtractAsync(context);
        await _eventLog.LogAsync(new ExtractionAttemptEvent(
            currentField.Id, extraction));

        // Step 3: Validate
        var validation = _validator.Validate(currentField, extraction.Value);

        // Step 4: State machine decides next step
        var transition = _stateMachine.ProcessExtraction(extraction, validation);

        return new ProcessingResult(
            Success: transition.Success,
            Message: transition.Message,
            Session: _stateMachine.GetSession(),
            RequiresConfirmation: transition.RequiresConfirmation,
            PendingValue: extraction.Value);
    }
}
```

Each step is independent and testable. The state machine **never touches the network**. The LLM **never touches the state**.

## Dark Mode: CSS Variables for the Win

Dark mode is implemented with CSS custom properties:

```css
:root {
    --bg-primary: #f8fafc;
    --bg-secondary: #ffffff;
    --text-primary: #1e293b;
    --accent-blue: #3b82f6;
    --accent-green: #22c55e;
}

[data-theme="dark"] {
    --bg-primary: #0f172a;
    --bg-secondary: #1e293b;
    --text-primary: #f1f5f9;
    --accent-blue: #60a5fa;
    --accent-green: #4ade80;
}

body {
    background: var(--bg-primary);
    color: var(--text-primary);
}
```

The theme toggle persists via localStorage:

```javascript
window.voiceFormTheme = (function () {
    const THEME_KEY = 'voiceform-theme';

    function toggle() {
        const current = document.documentElement
            .getAttribute('data-theme') || 'light';
        const newTheme = current === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', newTheme);
        localStorage.setItem(THEME_KEY, newTheme);
    }

    // Initialize from saved preference or system preference
    function init() {
        const saved = localStorage.getItem(THEME_KEY);
        const systemDark = window.matchMedia(
            '(prefers-color-scheme: dark)').matches;
        const theme = saved || (systemDark ? 'dark' : 'light');
        document.documentElement.setAttribute('data-theme', theme);
    }

    return { toggle, init };
})();
```

## Testing: Unit Tests + Browser Integration

The deterministic architecture pays off in testing. Here's a state machine unit test for the happy path:

```csharp
[Fact]
public void ProcessExtraction_HighConfidence_AutoConfirms()
{
    // Arrange
    var form = CreateTestForm();
    var stateMachine = new FormStateMachine();
    stateMachine.StartSession(form);

    var extraction = new ExtractionResponse(
        FieldId: "fullName",
        Value: "John Smith",
        Confidence: 0.95,  // High confidence
        NeedsConfirmation: false,
        Reason: "Clear speech");

    var validation = ValidationResult.Success();

    // Act
    var result = stateMachine.ProcessExtraction(extraction, validation);

    // Assert
    result.Success.Should().BeTrue();
    result.RequiresConfirmation.Should().BeFalse();

    var fieldState = stateMachine.GetSession()
        .GetFieldState("fullName");
    fieldState.Status.Should().Be(FieldStatus.Confirmed);
    fieldState.Value.Should().Be("John Smith");
}
```

And here's a negative test-low confidence **forces** confirmation regardless of what the LLM suggests:

```csharp
[Fact]
public void ProcessExtraction_LowConfidence_RequiresConfirmation()
{
    // Arrange
    var form = CreateTestForm();
    var stateMachine = new FormStateMachine();
    stateMachine.StartSession(form);

    var extraction = new ExtractionResponse(
        FieldId: "fullName",
        Value: "John Smith",
        Confidence: 0.65,  // Below threshold
        NeedsConfirmation: false,  // LLM says no, but policy overrides
        Reason: "Noisy audio");

    var validation = ValidationResult.Success();

    // Act
    var result = stateMachine.ProcessExtraction(extraction, validation);

    // Assert
    result.RequiresConfirmation.Should().BeTrue(
        "Policy threshold (0.85) should override LLM suggestion");

    var fieldState = stateMachine.GetSession()
        .GetFieldState("fullName");
    fieldState.Status.Should().Be(FieldStatus.AwaitingConfirmation);
}
```

And a PuppeteerSharp browser test that actually clicks things:

```csharp
[Fact]
public async Task HomePage_ThemeToggle_ShouldSwitchTheme()
{
    await _page!.GoToAsync(BaseUrl);
    await _page.WaitForSelectorAsync(".theme-toggle");

    var initialTheme = await _page.EvaluateFunctionAsync<string?>(
        "() => document.documentElement.getAttribute('data-theme')");

    // Click toggle
    await _page.ClickAsync(".theme-toggle");
    await Task.Delay(100);

    var newTheme = await _page.EvaluateFunctionAsync<string?>(
        "() => document.documentElement.getAttribute('data-theme')");

    newTheme.Should().NotBe(initialTheme);
}

[Fact]
public async Task VoiceFormPage_Sidebar_ShouldShowAllFields()
{
    await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
    await _page.WaitForSelectorAsync(".form-fields-list");

    var fieldItems = await _page.QuerySelectorAllAsync(".form-field-item");

    fieldItems.Should().HaveCount(5, "Customer intake form has 5 fields");
}
```

The full test suite runs 98 tests-76 unit tests for the business logic, 22 browser integration tests for the UI.

The important thing is not the tests themselves-it's that the architecture makes them possible.

## Why This Architecture Matters

Let's revisit why we built it this way:

```mermaid
graph TD
    subgraph "Traditional Voice Form"
        A1[User Speaks] --> B1[LLM]
        B1 --> C1[LLM decides field]
        C1 --> D1[LLM validates]
        D1 --> E1[LLM confirms]
        E1 --> F1[LLM advances form]
    end

    subgraph "Ten Commandments Approach"
        A2[User Speaks] --> B2[Whisper STT]
        B2 --> C2[Ollama Extract]
        C2 --> D2[C# Validator]
        D2 --> E2[Policy Check]
        E2 --> F2[State Machine]
    end

    style B1 stroke:#f96,stroke-width:2px
    style C1 stroke:#f96,stroke-width:2px
    style D1 stroke:#f96,stroke-width:2px
    style E1 stroke:#f96,stroke-width:2px
    style F1 stroke:#f96,stroke-width:2px

    style C2 stroke:#f96,stroke-width:2px
    style D2 stroke:#6f6,stroke-width:2px
    style E2 stroke:#6f6,stroke-width:2px
    style F2 stroke:#6f6,stroke-width:2px
```

In the traditional approach, **everything is the LLM**. Every decision is non-deterministic. Every test is probabilistic.

> Every bug is "it just sometimes does that"-unreproducible and unexplainable.

In our approach, the LLM does **one thing**: translate speech to structured values. Everything else is deterministic C# you can debug, test, and trace.

This is Commandments II, III, and IV in action: LLM translates (no side-effects), state machine controls flow (causality in code), policy decides confirmation (probability where acceptable).

## Running It Yourself

1. Start the dependencies:
```bash
docker compose -f devdeps-docker-compose.yml up -d
docker exec -it ollama ollama pull llama3.2:3b
```

2. Run the app:
```bash
cd Mostlylucid.VoiceForm
dotnet run
```

3. Navigate to `http://localhost:5000`

4. Click "Customer Intake" and start speaking!

## Conclusion

Voice interfaces don't have to be black boxes. By treating speech as just another input method, LLMs as translators, and keeping flow control in deterministic code, you get:

- **Predictable behavior** - Same inputs, same outputs
- **Testable logic** - Unit tests for business rules
- **Auditable decisions** - Every state transition is logged
- **Graceful degradation** - Voice fails? Type instead
- **Local operation** - No cloud dependencies for core functionality

The "Ten Commandments" aren't about being anti-AI. They're about **putting AI in its proper place**: a powerful tool that translates between human messiness and computer precision, not an oracle that makes decisions for us.

AI earns its place when it reduces ambiguity-not when it replaces responsibility.

Now go build something where you-not the model-decide what happens next.

## Coming in Part 2: Voice Feedback (IVR)

Right now, the user reads prompts and speaks responses. But what if the system could *speak back*? Part 2 will add text-to-speech feedback, turning this into a full IVR (Interactive Voice Response) system:

- **Read prompts aloud** - "Please say your full name"
- **Confirm extractions** - "I heard 'John Smith'. Is that correct?"
- **Audio state cues** - Sounds for recording start/stop, confirmation, errors
- **Hands-free operation** - Complete forms without looking at the screen

The architecture is already prepared: the state machine emits events, the orchestrator coordinates services. Adding voice output is just another service that listens to those events.

## Coming in Part 3: Policy Enforcement

The current validation is type-based: emails look like emails, dates parse as dates. But what about business rules? "Date of birth must be at least 18 years ago." "Phone number must match the region selected." "Notes field cannot contain profanity."

Part 3 will add:

- **Rule-based validation** - Declarative policies in the schema
- **Cross-field constraints** - "If country is US, phone must be 10 digits"
- **LLM-assisted policy checking** - Using the model to flag potential issues, with deterministic code making the final call
- **Audit trails** - Which policy triggered, why it failed, what the user did next

Same principle: the LLM advises, the code decides.

## Further Reading

- [The Ten Commandments of LLM Use](/blog/tencommandments) - The philosophy behind this architecture
- [Using Docker Compose for Development Dependencies](/blog/dockercomposedevdeps) - Setting up local services
- [HTMX with ASP.NET Core](/blog/htmxwithaspnetcore) - Server-driven UI patterns

The full source code is in the `Mostlylucid.VoiceForm` project in the repo.
