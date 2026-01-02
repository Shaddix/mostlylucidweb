namespace Mostlylucid.VoiceForm.Models.FormSchema;

/// <summary>
/// Policy for determining when confirmation is required for a field.
/// This is DETERMINISTIC - rules-based, not LLM judgment.
/// </summary>
public class ConfirmationPolicy
{
    /// <summary>
    /// Always require confirmation regardless of confidence
    /// </summary>
    public bool AlwaysConfirm { get; set; } = false;

    /// <summary>
    /// Confidence threshold below which confirmation is required (0.0-1.0)
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.85;

    /// <summary>
    /// Require confirmation when natural language was parsed (e.g., "May twelfth" -> "2024-05-12")
    /// </summary>
    public bool ConfirmNaturalLanguageParsing { get; set; } = true;
}
