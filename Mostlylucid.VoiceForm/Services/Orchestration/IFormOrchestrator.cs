using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Services.Orchestration;

/// <summary>
/// Orchestrates the voice form workflow.
/// Coordinates STT, extraction, validation, confirmation, and state transitions.
/// </summary>
public interface IFormOrchestrator
{
    /// <summary>
    /// Start a new form session
    /// </summary>
    Task<StateTransitionResult> StartSessionAsync(
        string formId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process audio input for the current field
    /// </summary>
    Task<StateTransitionResult> ProcessAudioAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// User confirms the pending value
    /// </summary>
    Task<StateTransitionResult> ConfirmValueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// User rejects the pending value
    /// </summary>
    Task<StateTransitionResult> RejectValueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// User skips the current field (if optional)
    /// </summary>
    Task<StateTransitionResult> SkipFieldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current session state
    /// </summary>
    FormSession? GetCurrentSession();

    /// <summary>
    /// Get the current field prompt
    /// </summary>
    string? GetCurrentPrompt();
}

/// <summary>
/// Loads form schemas from storage
/// </summary>
public interface IFormSchemaLoader
{
    /// <summary>
    /// Get all available form IDs
    /// </summary>
    Task<IReadOnlyList<string>> GetFormIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a form definition by ID
    /// </summary>
    Task<FormDefinition?> LoadFormAsync(string formId, CancellationToken cancellationToken = default);
}
