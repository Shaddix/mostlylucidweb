using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Confirmation;

/// <summary>
/// Abstraction for confirmation policy.
/// This is DETERMINISTIC - rules-based, not LLM judgment.
/// The policy decides when to ask the user to confirm, based on:
/// - Field configuration (alwaysConfirm)
/// - Confidence thresholds
/// - Whether natural language parsing was involved
/// </summary>
public interface IConfirmationPolicy
{
    /// <summary>
    /// Determine if a value needs user confirmation
    /// </summary>
    /// <param name="field">The field definition with its confirmation policy</param>
    /// <param name="extraction">The extraction result from the LLM</param>
    /// <param name="validation">The validation result</param>
    /// <returns>Decision with reason</returns>
    ConfirmationDecision ShouldConfirm(
        FieldDefinition field,
        ExtractionResponse extraction,
        ValidationResult validation);
}
