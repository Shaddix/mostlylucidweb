namespace Mostlylucid.VoiceForm.Models.State;

/// <summary>
/// Current state of a single form field within a session
/// </summary>
public class FieldState
{
    /// <summary>
    /// The field ID this state belongs to
    /// </summary>
    public required string FieldId { get; set; }

    /// <summary>
    /// Current status of this field
    /// </summary>
    public FieldStatus Status { get; set; } = FieldStatus.Pending;

    /// <summary>
    /// The confirmed/final value (null until confirmed)
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// The pending value awaiting confirmation (null if not in AwaitingConfirmation)
    /// </summary>
    public string? PendingValue { get; set; }

    /// <summary>
    /// Confidence of the pending extraction
    /// </summary>
    public double? PendingConfidence { get; set; }

    /// <summary>
    /// Number of capture attempts for this field
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Last transcript received for this field
    /// </summary>
    public string? LastTranscript { get; set; }
}
