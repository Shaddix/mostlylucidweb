using System.Text.Json;

namespace Mostlylucid.VoiceForm.Models.Events;

/// <summary>
/// Base class for all form events.
/// Events are immutable and used for audit logging and session replay.
/// </summary>
public abstract class FormEvent
{
    /// <summary>
    /// Session this event belongs to
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Sequence number within the session (for ordering)
    /// </summary>
    public required int SequenceNumber { get; init; }

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Event type discriminator for deserialization
    /// </summary>
    public abstract string EventType { get; }

    /// <summary>
    /// Field ID this event relates to (null for session-level events)
    /// </summary>
    public virtual string? FieldId { get; init; }

    /// <summary>
    /// Serialize the event payload to JSON
    /// </summary>
    public abstract string ToPayloadJson();
}

/// <summary>
/// Event raised when a session is started
/// </summary>
public class SessionStartedEvent : FormEvent
{
    public override string EventType => "SessionStarted";
    public required string FormId { get; init; }
    public required int FormVersion { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        FormId,
        FormVersion
    });
}

/// <summary>
/// Event raised when STT produces a transcript
/// </summary>
public class TranscriptReceivedEvent : FormEvent
{
    public override string EventType => "TranscriptReceived";
    public override string? FieldId { get; init; }
    public required string Transcript { get; init; }
    public required double Confidence { get; init; }
    public required int DurationMs { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        Transcript,
        Confidence,
        DurationMs
    });
}

/// <summary>
/// Event raised when LLM extraction is attempted
/// </summary>
public class ExtractionAttemptEvent : FormEvent
{
    public override string EventType => "ExtractionAttempt";
    public override string? FieldId { get; init; }
    public required string? ExtractedValue { get; init; }
    public required double Confidence { get; init; }
    public required bool NeedsConfirmation { get; init; }
    public string? Reason { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        ExtractedValue,
        Confidence,
        NeedsConfirmation,
        Reason
    });
}

/// <summary>
/// Event raised when a field value is confirmed by the user
/// </summary>
public class FieldConfirmedEvent : FormEvent
{
    public override string EventType => "FieldConfirmed";
    public override string? FieldId { get; init; }
    public required string Value { get; init; }
    public required string ConfirmedBy { get; init; } // "user" or "auto"

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        Value,
        ConfirmedBy
    });
}

/// <summary>
/// Event raised when a field value is rejected by the user
/// </summary>
public class FieldRejectedEvent : FormEvent
{
    public override string EventType => "FieldRejected";
    public override string? FieldId { get; init; }
    public required string AttemptedValue { get; init; }
    public required string Reason { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        AttemptedValue,
        Reason
    });
}

/// <summary>
/// Event raised when a field is skipped
/// </summary>
public class FieldSkippedEvent : FormEvent
{
    public override string EventType => "FieldSkipped";
    public override string? FieldId { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new { });
}

/// <summary>
/// Event raised when the form is completed
/// </summary>
public class FormCompletedEvent : FormEvent
{
    public override string EventType => "FormCompleted";
    public required Dictionary<string, string?> FinalValues { get; init; }

    public override string ToPayloadJson() => JsonSerializer.Serialize(new
    {
        FinalValues
    });
}
