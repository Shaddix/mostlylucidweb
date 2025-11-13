namespace Mostlylucid.Workflow.Shared.Models;

/// <summary>
/// Represents a complete workflow definition
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Unique identifier for this workflow
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the workflow
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this workflow does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Version number for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// List of nodes in this workflow
    /// </summary>
    public List<WorkflowNode> Nodes { get; set; } = new();

    /// <summary>
    /// List of connections between nodes
    /// </summary>
    public List<NodeConnection> Connections { get; set; } = new();

    /// <summary>
    /// ID of the starting node (trigger)
    /// </summary>
    public string? StartNodeId { get; set; }

    /// <summary>
    /// Tags for categorizing workflows
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Is this workflow enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;

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
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Global workflow variables/context
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }
}
