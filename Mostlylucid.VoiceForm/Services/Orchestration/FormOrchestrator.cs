using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Models.Events;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.State;
using Mostlylucid.VoiceForm.Services.Confirmation;
using Mostlylucid.VoiceForm.Services.EventLog;
using Mostlylucid.VoiceForm.Services.Extraction;
using Mostlylucid.VoiceForm.Services.StateMachine;
using Mostlylucid.VoiceForm.Services.Stt;
using Mostlylucid.VoiceForm.Services.Validation;

namespace Mostlylucid.VoiceForm.Services.Orchestration;

/// <summary>
/// Main orchestrator for voice form workflow.
/// Coordinates all services but does not make decisions - that's the state machine's job.
/// </summary>
public class FormOrchestrator : IFormOrchestrator
{
    private readonly IFormSchemaLoader _schemaLoader;
    private readonly ISttService _sttService;
    private readonly IFieldExtractor _extractor;
    private readonly IFormValidator _validator;
    private readonly IConfirmationPolicy _confirmationPolicy;
    private readonly IFormStateMachine _stateMachine;
    private readonly IFormEventLog _eventLog;
    private readonly ILogger<FormOrchestrator> _logger;

    public FormOrchestrator(
        IFormSchemaLoader schemaLoader,
        ISttService sttService,
        IFieldExtractor extractor,
        IFormValidator validator,
        IConfirmationPolicy confirmationPolicy,
        IFormStateMachine stateMachine,
        IFormEventLog eventLog,
        ILogger<FormOrchestrator> logger)
    {
        _schemaLoader = schemaLoader;
        _sttService = sttService;
        _extractor = extractor;
        _validator = validator;
        _confirmationPolicy = confirmationPolicy;
        _stateMachine = stateMachine;
        _eventLog = eventLog;
        _logger = logger;
    }

    public async Task<StateTransitionResult> StartSessionAsync(
        string formId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting session for form '{FormId}'", formId);

        var form = await _schemaLoader.LoadFormAsync(formId, cancellationToken);
        if (form == null)
        {
            return new StateTransitionResult(
                Success: false,
                Session: null!,
                Message: $"Form '{formId}' not found"
            );
        }

        var session = _stateMachine.StartSession(form);

        // Log session started event
        await _eventLog.LogAsync(new SessionStartedEvent
        {
            SessionId = session.Id,
            SequenceNumber = _stateMachine.GetNextSequence(),
            FormId = form.Id,
            FormVersion = form.Version
        }, cancellationToken);

        var currentField = _stateMachine.GetCurrentField();
        return new StateTransitionResult(
            Success: true,
            Session: session,
            Message: currentField?.Prompt ?? "Form started"
        );
    }

    public async Task<StateTransitionResult> ProcessAudioAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        var session = _stateMachine.CurrentSession;
        if (session == null)
        {
            return new StateTransitionResult(false, null!, "No active session. Start a session first.");
        }

        var currentField = _stateMachine.GetCurrentField();
        if (currentField == null)
        {
            return new StateTransitionResult(false, session, "Form is already complete");
        }

        _logger.LogInformation("Processing audio for field '{FieldId}'", currentField.Id);

        // Step 1: Transcribe audio (STT)
        SttResult sttResult;
        try
        {
            sttResult = await _sttService.TranscribeAsync(audioData, "wav", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STT failed for field '{FieldId}'", currentField.Id);
            return new StateTransitionResult(
                Success: false,
                Session: session,
                Message: "Speech recognition failed. Please try again."
            );
        }

        // Log transcript event
        await _eventLog.LogAsync(new TranscriptReceivedEvent
        {
            SessionId = session.Id,
            SequenceNumber = _stateMachine.GetNextSequence(),
            FieldId = currentField.Id,
            Transcript = sttResult.Transcript,
            Confidence = sttResult.Confidence,
            DurationMs = (int)sttResult.Duration.TotalMilliseconds
        }, cancellationToken);

        // Update field state with transcript
        var fieldState = session.GetFieldState(currentField.Id);
        fieldState.LastTranscript = sttResult.Transcript;

        if (string.IsNullOrWhiteSpace(sttResult.Transcript))
        {
            return new StateTransitionResult(
                Success: false,
                Session: session,
                Message: "I didn't hear anything. Please try again."
            );
        }

        // Step 2: Extract field value (LLM - as translator only)
        ExtractionResponse extraction;
        try
        {
            var context = new ExtractionContext(
                Field: currentField,
                Prompt: currentField.Prompt,
                Transcript: sttResult.Transcript
            );
            extraction = await _extractor.ExtractAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed for field '{FieldId}'", currentField.Id);
            return new StateTransitionResult(
                Success: false,
                Session: session,
                Message: "Failed to understand response. Please try again."
            );
        }

        // Step 3: Validate (deterministic, in code)
        var validation = _validator.Validate(currentField, extraction.Value);

        // Log extraction attempt
        await _eventLog.LogAsync(new ExtractionAttemptEvent
        {
            SessionId = session.Id,
            SequenceNumber = _stateMachine.GetNextSequence(),
            FieldId = currentField.Id,
            ExtractedValue = extraction.Value,
            Confidence = extraction.Confidence,
            NeedsConfirmation = extraction.NeedsConfirmation,
            Reason = extraction.Reason
        }, cancellationToken);

        // Step 4: Apply confirmation policy (deterministic, rules-based)
        var confirmationDecision = _confirmationPolicy.ShouldConfirm(
            currentField, extraction, validation);

        // Step 5: State machine decides the transition (deterministic)
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmationDecision);

        return result;
    }

    public async Task<StateTransitionResult> ConfirmValueAsync(CancellationToken cancellationToken = default)
    {
        var session = _stateMachine.CurrentSession;
        if (session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = _stateMachine.GetCurrentField();
        var fieldState = currentField != null ? session.GetFieldState(currentField.Id) : null;
        var pendingValue = fieldState?.PendingValue;

        var result = _stateMachine.ConfirmValue();

        if (result.Success && currentField != null && pendingValue != null)
        {
            // Log confirmation event
            await _eventLog.LogAsync(new FieldConfirmedEvent
            {
                SessionId = session.Id,
                SequenceNumber = _stateMachine.GetNextSequence(),
                FieldId = currentField.Id,
                Value = pendingValue,
                ConfirmedBy = "user"
            }, cancellationToken);

            // Check if form is now complete
            if (_stateMachine.IsComplete())
            {
                await _eventLog.LogAsync(new FormCompletedEvent
                {
                    SessionId = session.Id,
                    SequenceNumber = _stateMachine.GetNextSequence(),
                    FinalValues = session.GetConfirmedValues()
                }, cancellationToken);
            }
        }

        return result;
    }

    public async Task<StateTransitionResult> RejectValueAsync(CancellationToken cancellationToken = default)
    {
        var session = _stateMachine.CurrentSession;
        if (session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = _stateMachine.GetCurrentField();
        var fieldState = currentField != null ? session.GetFieldState(currentField.Id) : null;
        var pendingValue = fieldState?.PendingValue;

        var result = _stateMachine.RejectValue();

        if (result.Success && currentField != null && pendingValue != null)
        {
            // Log rejection event
            await _eventLog.LogAsync(new FieldRejectedEvent
            {
                SessionId = session.Id,
                SequenceNumber = _stateMachine.GetNextSequence(),
                FieldId = currentField.Id,
                AttemptedValue = pendingValue,
                Reason = "user_rejected"
            }, cancellationToken);
        }

        return result;
    }

    public async Task<StateTransitionResult> SkipFieldAsync(CancellationToken cancellationToken = default)
    {
        var session = _stateMachine.CurrentSession;
        if (session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = _stateMachine.GetCurrentField();
        var result = _stateMachine.SkipField();

        if (result.Success && currentField != null)
        {
            // Log skip event
            await _eventLog.LogAsync(new FieldSkippedEvent
            {
                SessionId = session.Id,
                SequenceNumber = _stateMachine.GetNextSequence(),
                FieldId = currentField.Id
            }, cancellationToken);
        }

        return result;
    }

    public FormSession? GetCurrentSession()
    {
        return _stateMachine.CurrentSession;
    }

    public string? GetCurrentPrompt()
    {
        var field = _stateMachine.GetCurrentField();
        return field?.Prompt;
    }
}
