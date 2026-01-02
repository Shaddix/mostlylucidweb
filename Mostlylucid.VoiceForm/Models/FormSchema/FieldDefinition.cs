namespace Mostlylucid.VoiceForm.Models.FormSchema;

/// <summary>
/// Definition of a single form field.
/// This is the source of truth for what the field is and how it should be validated.
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Unique identifier for the field
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Human-readable label for display
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// The prompt to speak/display when requesting this field
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Reprompt to use when the first attempt fails
    /// </summary>
    public string? Reprompt { get; set; }

    /// <summary>
    /// The type of field (determines validation rules)
    /// </summary>
    public FieldType Type { get; set; } = FieldType.Text;

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Validation constraints for this field
    /// </summary>
    public FieldConstraints? Constraints { get; set; }

    /// <summary>
    /// Per-field confirmation policy (overrides form defaults)
    /// </summary>
    public ConfirmationPolicy? ConfirmationPolicy { get; set; }

    /// <summary>
    /// Example values to help the LLM understand expected format
    /// </summary>
    public List<string>? Examples { get; set; }
}
