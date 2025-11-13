namespace Mostlylucid.Workflow.Shared.Models;

/// <summary>
/// Represents a connection between two nodes in a workflow
/// </summary>
public class NodeConnection
{
    /// <summary>
    /// Unique identifier for this connection
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Source node ID
    /// </summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Target node ID
    /// </summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Output name from source node (e.g., "success", "failure", "output1")
    /// </summary>
    public string SourceOutput { get; set; } = "default";

    /// <summary>
    /// Input name on target node (e.g., "input1", "data")
    /// </summary>
    public string TargetInput { get; set; } = "default";

    /// <summary>
    /// Optional condition that must be met for data to flow through this connection
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Label to display on the connection
    /// </summary>
    public string? Label { get; set; }
}
