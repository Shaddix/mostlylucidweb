using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Models.State;

/// <summary>
/// Complete state of a form filling session.
/// This is the source of truth for current progress.
/// </summary>
public class FormSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The form definition being filled
    /// </summary>
    public required FormDefinition Form { get; set; }

    /// <summary>
    /// Current status of the overall form
    /// </summary>
    public FormStatus Status { get; set; } = FormStatus.NotStarted;

    /// <summary>
    /// Index of the current field being captured (0-based)
    /// </summary>
    public int CurrentFieldIndex { get; set; } = 0;

    /// <summary>
    /// State of each field in the form
    /// </summary>
    public Dictionary<string, FieldState> FieldStates { get; set; } = new();

    /// <summary>
    /// When the session was started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session was completed (null if not complete)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Monotonically increasing sequence number for event ordering
    /// </summary>
    public int EventSequence { get; set; } = 0;

    /// <summary>
    /// Get the current field definition (null if form is complete)
    /// </summary>
    public FieldDefinition? GetCurrentField()
    {
        if (CurrentFieldIndex >= Form.Fields.Count)
            return null;
        return Form.Fields[CurrentFieldIndex];
    }

    /// <summary>
    /// Get the state of a specific field
    /// </summary>
    public FieldState GetFieldState(string fieldId)
    {
        if (!FieldStates.TryGetValue(fieldId, out var state))
        {
            state = new FieldState { FieldId = fieldId };
            FieldStates[fieldId] = state;
        }
        return state;
    }

    /// <summary>
    /// Get all confirmed values as a dictionary
    /// </summary>
    public Dictionary<string, string?> GetConfirmedValues()
    {
        return FieldStates
            .Where(f => f.Value.Status == FieldStatus.Confirmed)
            .ToDictionary(f => f.Key, f => f.Value.Value);
    }
}
