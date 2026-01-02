using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Confirmation;

/// <summary>
/// Threshold-based confirmation policy.
/// All decisions are deterministic based on configuration.
/// The LLM's "needsConfirmation" is ADVISORY only - this policy decides.
/// </summary>
public class ThresholdConfirmationPolicy : IConfirmationPolicy
{
    private readonly VoiceFormConfig _config;
    private readonly ILogger<ThresholdConfirmationPolicy> _logger;

    // Field types that are high-value and should have lower thresholds
    private static readonly HashSet<FieldType> HighValueFieldTypes =
    [
        FieldType.Email,
        FieldType.Date
    ];

    public ThresholdConfirmationPolicy(
        VoiceFormConfig config,
        ILogger<ThresholdConfirmationPolicy> logger)
    {
        _config = config;
        _logger = logger;
    }

    public ConfirmationDecision ShouldConfirm(
        FieldDefinition field,
        ExtractionResponse extraction,
        ValidationResult validation)
    {
        var fieldPolicy = field.ConfirmationPolicy;
        var defaultThreshold = _config.DefaultConfidenceThreshold;

        _logger.LogDebug(
            "Evaluating confirmation for field '{FieldId}': confidence={Confidence:F2}, llmSuggests={LlmSuggests}",
            field.Id, extraction.Confidence, extraction.NeedsConfirmation);

        // Rule 1: Field explicitly requires confirmation
        if (fieldPolicy?.AlwaysConfirm == true)
        {
            _logger.LogDebug("Field '{FieldId}' requires confirmation by policy", field.Id);
            return new ConfirmationDecision(true, ConfirmationReason.FieldPolicyRequires);
        }

        // Rule 2: Extraction confidence below threshold
        var threshold = fieldPolicy?.ConfidenceThreshold ?? defaultThreshold;
        if (extraction.Confidence < threshold)
        {
            _logger.LogDebug(
                "Field '{FieldId}' confidence {Confidence:F2} below threshold {Threshold:F2}",
                field.Id, extraction.Confidence, threshold);
            return new ConfirmationDecision(true, ConfirmationReason.LowConfidence);
        }

        // Rule 3: LLM flagged as ambiguous AND confidence is borderline
        // We trust the LLM's ambiguity signal if confidence is within a margin
        if (extraction.NeedsConfirmation && extraction.Confidence < threshold + 0.1)
        {
            _logger.LogDebug("Field '{FieldId}' flagged as ambiguous by LLM", field.Id);
            return new ConfirmationDecision(true, ConfirmationReason.AmbiguousExtraction);
        }

        // Rule 4: Natural language parsing was involved (detected by reason)
        var involvedParsing = !string.IsNullOrEmpty(extraction.Reason) &&
            (extraction.Reason.Contains("parsed", StringComparison.OrdinalIgnoreCase) ||
             extraction.Reason.Contains("natural language", StringComparison.OrdinalIgnoreCase) ||
             extraction.Reason.Contains("inferred", StringComparison.OrdinalIgnoreCase));

        if (involvedParsing && (fieldPolicy?.ConfirmNaturalLanguageParsing ?? true))
        {
            _logger.LogDebug("Field '{FieldId}' involved natural language parsing", field.Id);
            return new ConfirmationDecision(true, ConfirmationReason.NaturalLanguageParsed);
        }

        // Rule 5: High-value field types have stricter requirements
        if (HighValueFieldTypes.Contains(field.Type) && extraction.Confidence < 0.95)
        {
            _logger.LogDebug("Field '{FieldId}' is high-value type with confidence below 0.95", field.Id);
            return new ConfirmationDecision(true, ConfirmationReason.HighValueField);
        }

        // All checks passed - no confirmation needed
        _logger.LogDebug("Field '{FieldId}' does not require confirmation", field.Id);
        return new ConfirmationDecision(false, ConfirmationReason.None);
    }
}
