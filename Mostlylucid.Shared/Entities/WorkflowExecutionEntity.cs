using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.Shared.Entities;

/// <summary>
/// Database entity for workflow executions
/// </summary>
[Table("workflow_executions")]
public class WorkflowExecutionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Unique string identifier
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to workflow definition
    /// </summary>
    public int WorkflowDefinitionId { get; set; }

    /// <summary>
    /// Navigation property to workflow definition
    /// </summary>
    public WorkflowDefinitionEntity WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Execution status
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Input data as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? InputDataJson { get; set; }

    /// <summary>
    /// Output data as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? OutputDataJson { get; set; }

    /// <summary>
    /// Execution history as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string NodeExecutionsJson { get; set; } = "[]";

    /// <summary>
    /// Current context as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string ContextJson { get; set; } = "{}";

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error stack trace
    /// </summary>
    public string? ErrorStackTrace { get; set; }

    /// <summary>
    /// Who/what triggered this execution
    /// </summary>
    [MaxLength(200)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
