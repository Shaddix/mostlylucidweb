namespace Mostlylucid.VoiceForm.Models.FormSchema;

/// <summary>
/// Complete definition of a voice form.
/// This JSON schema is the source of truth for form structure.
/// </summary>
public class FormDefinition
{
    /// <summary>
    /// Unique identifier for the form
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Human-readable name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of the form's purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Version number for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Default confirmation policy for all fields (can be overridden per-field)
    /// </summary>
    public ConfirmationPolicy DefaultConfirmationPolicy { get; set; } = new();

    /// <summary>
    /// Ordered list of fields to collect
    /// </summary>
    public required List<FieldDefinition> Fields { get; set; }
}
