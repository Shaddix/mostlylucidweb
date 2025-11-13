using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.Shared.Entities;

/// <summary>
/// Database entity for workflow definitions
/// </summary>
[Table("workflow_definitions")]
public class WorkflowDefinitionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Unique string identifier
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the workflow
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the workflow
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Version number
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// JSON serialized workflow definition
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string DefinitionJson { get; set; } = string.Empty;

    /// <summary>
    /// Is this workflow enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Created by user
    /// </summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Workflow executions
    /// </summary>
    public ICollection<WorkflowExecutionEntity> Executions { get; set; } = new List<WorkflowExecutionEntity>();
}
