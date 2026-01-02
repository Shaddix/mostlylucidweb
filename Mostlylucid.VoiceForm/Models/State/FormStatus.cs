namespace Mostlylucid.VoiceForm.Models.State;

/// <summary>
/// Overall status of a form session
/// </summary>
public enum FormStatus
{
    /// <summary>
    /// Session created but not started
    /// </summary>
    NotStarted,

    /// <summary>
    /// Actively collecting fields
    /// </summary>
    InProgress,

    /// <summary>
    /// Waiting for user to confirm or reject a value
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// All fields collected, form complete
    /// </summary>
    Completed,

    /// <summary>
    /// User abandoned the form
    /// </summary>
    Abandoned
}

/// <summary>
/// Status of an individual field within a session
/// </summary>
public enum FieldStatus
{
    /// <summary>
    /// Not yet attempted
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being captured
    /// </summary>
    InProgress,

    /// <summary>
    /// Value extracted, waiting for user confirmation
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// User confirmed the value
    /// </summary>
    Confirmed,

    /// <summary>
    /// Optional field skipped by user
    /// </summary>
    Skipped
}
