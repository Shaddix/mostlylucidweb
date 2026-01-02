using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Models.Extraction;

/// <summary>
/// Context provided to the LLM for field extraction.
/// Contains ONLY what the LLM needs - no control flow information.
/// </summary>
public record ExtractionContext(
    /// <summary>
    /// The field definition (type, constraints, examples)
    /// </summary>
    FieldDefinition Field,

    /// <summary>
    /// The prompt that was spoken to the user
    /// </summary>
    string Prompt,

    /// <summary>
    /// The transcript from STT
    /// </summary>
    string Transcript
);

/// <summary>
/// The response from the LLM extraction.
/// This is the ONLY output the LLM provides.
/// The LLM does NOT control flow, validation, or next steps.
/// </summary>
public record ExtractionResponse(
    /// <summary>
    /// The field ID being extracted
    /// </summary>
    string FieldId,

    /// <summary>
    /// The extracted value (null if nothing could be extracted)
    /// </summary>
    string? Value,

    /// <summary>
    /// Confidence in the extraction (0.0-1.0)
    /// </summary>
    double Confidence,

    /// <summary>
    /// Whether the LLM suggests confirmation (advisory only - policy decides)
    /// </summary>
    bool NeedsConfirmation,

    /// <summary>
    /// Reason for the confidence level or confirmation suggestion
    /// </summary>
    string? Reason
);

/// <summary>
/// Result from STT transcription
/// </summary>
public record SttResult(
    /// <summary>
    /// The transcribed text
    /// </summary>
    string Transcript,

    /// <summary>
    /// Confidence score from STT (0.0-1.0)
    /// </summary>
    double Confidence,

    /// <summary>
    /// Duration of the audio
    /// </summary>
    TimeSpan Duration,

    /// <summary>
    /// Detected language (if available)
    /// </summary>
    string? Language = null,

    /// <summary>
    /// Raw response from STT service for debugging
    /// </summary>
    string? RawResponse = null
);

/// <summary>
/// Result of field validation
/// </summary>
public record ValidationResult(
    /// <summary>
    /// Whether the value is valid
    /// </summary>
    bool IsValid,

    /// <summary>
    /// Error message if invalid
    /// </summary>
    string? ErrorMessage = null,

    /// <summary>
    /// Normalized value (e.g., "may 12 1984" -> "1984-05-12")
    /// </summary>
    string? NormalizedValue = null
);

/// <summary>
/// Result of confirmation policy evaluation
/// </summary>
public record ConfirmationDecision(
    /// <summary>
    /// Whether confirmation is required
    /// </summary>
    bool RequiresConfirmation,

    /// <summary>
    /// Why confirmation is or isn't required
    /// </summary>
    ConfirmationReason Reason
);

/// <summary>
/// Reasons why confirmation may be required
/// </summary>
public enum ConfirmationReason
{
    /// <summary>
    /// No confirmation needed
    /// </summary>
    None,

    /// <summary>
    /// Extraction confidence below threshold
    /// </summary>
    LowConfidence,

    /// <summary>
    /// Field policy requires confirmation for this field type
    /// </summary>
    FieldPolicyRequires,

    /// <summary>
    /// Natural language was parsed (date from "May twelfth")
    /// </summary>
    NaturalLanguageParsed,

    /// <summary>
    /// LLM flagged the extraction as ambiguous
    /// </summary>
    AmbiguousExtraction,

    /// <summary>
    /// High-value/sensitive field (SSN, financial, etc.)
    /// </summary>
    HighValueField
}

/// <summary>
/// Result of a state machine transition
/// </summary>
public record StateTransitionResult(
    /// <summary>
    /// Whether the transition succeeded
    /// </summary>
    bool Success,

    /// <summary>
    /// The new session state after transition
    /// </summary>
    FormSession Session,

    /// <summary>
    /// Message describing what happened
    /// </summary>
    string Message,

    /// <summary>
    /// Whether confirmation is now required from user
    /// </summary>
    bool RequiresConfirmation = false,

    /// <summary>
    /// The pending value awaiting confirmation
    /// </summary>
    string? PendingValue = null
);
