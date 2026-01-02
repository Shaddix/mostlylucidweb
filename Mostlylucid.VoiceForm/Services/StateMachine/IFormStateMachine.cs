using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Services.StateMachine;

/// <summary>
/// The deterministic state machine for form progression.
/// This owns all state transitions - the LLM has no say.
/// Same inputs always produce the same state changes.
/// </summary>
public interface IFormStateMachine
{
    /// <summary>
    /// Current form session (null if not started)
    /// </summary>
    FormSession? CurrentSession { get; }

    /// <summary>
    /// Start a new form session
    /// </summary>
    FormSession StartSession(FormDefinition form);

    /// <summary>
    /// Get the current field to capture (null if form complete)
    /// </summary>
    FieldDefinition? GetCurrentField();

    /// <summary>
    /// Process an extraction result - state machine decides the transition
    /// </summary>
    StateTransitionResult ProcessExtraction(
        ExtractionResponse extraction,
        ValidationResult validation,
        ConfirmationDecision confirmationDecision);

    /// <summary>
    /// User confirmed the pending value
    /// </summary>
    StateTransitionResult ConfirmValue();

    /// <summary>
    /// User rejected the pending value - retry current field
    /// </summary>
    StateTransitionResult RejectValue();

    /// <summary>
    /// Skip the current field (only valid for optional fields)
    /// </summary>
    StateTransitionResult SkipField();

    /// <summary>
    /// Check if the form is complete
    /// </summary>
    bool IsComplete();

    /// <summary>
    /// Get the next event sequence number
    /// </summary>
    int GetNextSequence();
}
