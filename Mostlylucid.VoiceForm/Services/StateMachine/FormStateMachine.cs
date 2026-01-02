using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Services.StateMachine;

/// <summary>
/// Deterministic form state machine.
/// All transitions are defined here - same inputs always produce same outputs.
/// The LLM cannot influence state transitions.
/// </summary>
public class FormStateMachine : IFormStateMachine
{
    private readonly ILogger<FormStateMachine> _logger;
    private FormSession? _session;

    public FormStateMachine(ILogger<FormStateMachine> logger)
    {
        _logger = logger;
    }

    public FormSession? CurrentSession => _session;

    public FormSession StartSession(FormDefinition form)
    {
        _logger.LogInformation("Starting new form session for '{FormId}'", form.Id);

        _session = new FormSession
        {
            Form = form,
            Status = FormStatus.InProgress,
            CurrentFieldIndex = 0
        };

        // Initialize field states
        foreach (var field in form.Fields)
        {
            _session.FieldStates[field.Id] = new FieldState
            {
                FieldId = field.Id,
                Status = FieldStatus.Pending
            };
        }

        // Mark first field as in progress
        if (form.Fields.Count > 0)
        {
            var firstField = form.Fields[0];
            _session.FieldStates[firstField.Id].Status = FieldStatus.InProgress;
        }

        return _session;
    }

    public FieldDefinition? GetCurrentField()
    {
        return _session?.GetCurrentField();
    }

    public StateTransitionResult ProcessExtraction(
        ExtractionResponse extraction,
        ValidationResult validation,
        ConfirmationDecision confirmationDecision)
    {
        if (_session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = GetCurrentField();
        if (currentField == null)
        {
            return new StateTransitionResult(false, _session, "Form is already complete");
        }

        var fieldState = _session.GetFieldState(currentField.Id);
        fieldState.AttemptCount++;

        _logger.LogDebug(
            "Processing extraction for field '{FieldId}': value={Value}, valid={Valid}, confirm={Confirm}",
            currentField.Id, extraction.Value, validation.IsValid, confirmationDecision.RequiresConfirmation);

        // If extraction failed (no value)
        if (extraction.Value == null)
        {
            fieldState.Status = FieldStatus.InProgress;
            return new StateTransitionResult(
                Success: false,
                Session: _session,
                Message: "Could not extract value from response. Please try again."
            );
        }

        // If validation failed
        if (!validation.IsValid)
        {
            fieldState.Status = FieldStatus.InProgress;
            return new StateTransitionResult(
                Success: false,
                Session: _session,
                Message: validation.ErrorMessage ?? "Invalid value. Please try again."
            );
        }

        // Use normalized value if available
        var finalValue = validation.NormalizedValue ?? extraction.Value;

        // If confirmation is required
        if (confirmationDecision.RequiresConfirmation)
        {
            fieldState.Status = FieldStatus.AwaitingConfirmation;
            fieldState.PendingValue = finalValue;
            fieldState.PendingConfidence = extraction.Confidence;
            _session.Status = FormStatus.AwaitingConfirmation;

            return new StateTransitionResult(
                Success: true,
                Session: _session,
                Message: $"Please confirm: {finalValue}",
                RequiresConfirmation: true,
                PendingValue: finalValue
            );
        }

        // Auto-confirm (high confidence, no confirmation needed)
        return AutoConfirmValue(fieldState, finalValue);
    }

    public StateTransitionResult ConfirmValue()
    {
        if (_session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = GetCurrentField();
        if (currentField == null)
        {
            return new StateTransitionResult(false, _session, "Form is already complete");
        }

        var fieldState = _session.GetFieldState(currentField.Id);

        if (fieldState.Status != FieldStatus.AwaitingConfirmation)
        {
            return new StateTransitionResult(false, _session, "No value awaiting confirmation");
        }

        if (fieldState.PendingValue == null)
        {
            return new StateTransitionResult(false, _session, "No pending value to confirm");
        }

        _logger.LogInformation("User confirmed value '{Value}' for field '{FieldId}'",
            fieldState.PendingValue, currentField.Id);

        // Confirm the value
        fieldState.Value = fieldState.PendingValue;
        fieldState.Status = FieldStatus.Confirmed;
        fieldState.PendingValue = null;
        fieldState.PendingConfidence = null;

        return MoveToNextField();
    }

    public StateTransitionResult RejectValue()
    {
        if (_session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = GetCurrentField();
        if (currentField == null)
        {
            return new StateTransitionResult(false, _session, "Form is already complete");
        }

        var fieldState = _session.GetFieldState(currentField.Id);

        if (fieldState.Status != FieldStatus.AwaitingConfirmation)
        {
            return new StateTransitionResult(false, _session, "No value awaiting confirmation");
        }

        _logger.LogInformation("User rejected value for field '{FieldId}'", currentField.Id);

        // Clear pending and go back to in progress
        fieldState.Status = FieldStatus.InProgress;
        fieldState.PendingValue = null;
        fieldState.PendingConfidence = null;
        _session.Status = FormStatus.InProgress;

        var reprompt = currentField.Reprompt ?? currentField.Prompt;
        return new StateTransitionResult(
            Success: true,
            Session: _session,
            Message: reprompt
        );
    }

    public StateTransitionResult SkipField()
    {
        if (_session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        var currentField = GetCurrentField();
        if (currentField == null)
        {
            return new StateTransitionResult(false, _session, "Form is already complete");
        }

        if (currentField.Required)
        {
            return new StateTransitionResult(false, _session, "This field is required and cannot be skipped");
        }

        var fieldState = _session.GetFieldState(currentField.Id);
        _logger.LogInformation("User skipped optional field '{FieldId}'", currentField.Id);

        fieldState.Status = FieldStatus.Skipped;
        fieldState.Value = null;

        return MoveToNextField();
    }

    public bool IsComplete()
    {
        return _session?.Status == FormStatus.Completed;
    }

    public int GetNextSequence()
    {
        if (_session == null) return 0;
        return ++_session.EventSequence;
    }

    private StateTransitionResult AutoConfirmValue(FieldState fieldState, string value)
    {
        _logger.LogInformation("Auto-confirming value '{Value}' for field '{FieldId}'",
            value, fieldState.FieldId);

        fieldState.Value = value;
        fieldState.Status = FieldStatus.Confirmed;
        fieldState.PendingValue = null;
        fieldState.PendingConfidence = null;

        return MoveToNextField();
    }

    private StateTransitionResult MoveToNextField()
    {
        if (_session == null)
        {
            return new StateTransitionResult(false, null!, "No active session");
        }

        // Find next pending field
        _session.CurrentFieldIndex++;

        while (_session.CurrentFieldIndex < _session.Form.Fields.Count)
        {
            var nextField = _session.Form.Fields[_session.CurrentFieldIndex];
            var nextState = _session.GetFieldState(nextField.Id);

            if (nextState.Status == FieldStatus.Pending)
            {
                nextState.Status = FieldStatus.InProgress;
                _session.Status = FormStatus.InProgress;

                _logger.LogInformation("Moving to next field: '{FieldId}'", nextField.Id);

                return new StateTransitionResult(
                    Success: true,
                    Session: _session,
                    Message: nextField.Prompt
                );
            }

            _session.CurrentFieldIndex++;
        }

        // All fields complete
        _session.Status = FormStatus.Completed;
        _session.CompletedAt = DateTime.UtcNow;

        _logger.LogInformation("Form completed");

        return new StateTransitionResult(
            Success: true,
            Session: _session,
            Message: "Form completed successfully!"
        );
    }
}
