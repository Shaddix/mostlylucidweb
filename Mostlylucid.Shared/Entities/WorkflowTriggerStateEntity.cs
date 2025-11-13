using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.Shared.Entities;

/// <summary>
/// Database entity for storing workflow trigger states (for polling, schedules, etc.)
/// </summary>
[Table("workflow_trigger_states")]
public class WorkflowTriggerStateEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to workflow definition
    /// </summary>
    public int WorkflowDefinitionId { get; set; }

    /// <summary>
    /// Navigation property to workflow definition
    /// </summary>
    public WorkflowDefinitionEntity WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Type of trigger (Poll, Schedule, Webhook, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Configuration for the trigger (JSON)
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// Current state data (JSON) - stores things like last poll time, last seen ID, etc.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string StateJson { get; set; } = "{}";

    /// <summary>
    /// Is this trigger enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Last time the trigger was checked/executed
    /// </summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>
    /// Last time the trigger successfully fired
    /// </summary>
    public DateTime? LastFiredAt { get; set; }

    /// <summary>
    /// Number of times the trigger has fired
    /// </summary>
    public int FireCount { get; set; } = 0;

    /// <summary>
    /// Error message if last check failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated date
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
