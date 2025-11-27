namespace Mostlylucid.Workflow.Shared.Models;

/// <summary>
/// Represents an instance of a workflow execution
/// </summary>
public class WorkflowExecution
{
    /// <summary>
    /// Unique identifier for this execution
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the workflow being executed
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the execution
    /// </summary>
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;

    /// <summary>
    /// When the execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the execution completed (successfully or with error)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Input data provided to start the workflow
    /// </summary>
    public Dictionary<string, object>? InputData { get; set; }

    /// <summary>
    /// Output data produced by the workflow
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// Execution history of individual nodes
    /// </summary>
    public List<NodeExecutionResult> NodeExecutions { get; set; } = new();

    /// <summary>
    /// Current context/state during execution
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if execution failed
    /// </summary>
    public string? ErrorStackTrace { get; set; }

    /// <summary>
    /// Triggered by (user, schedule, webhook, etc.)
    /// </summary>
    public string? TriggeredBy { get; set; }
}

/// <summary>
/// Status of a workflow execution
/// </summary>
public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Result of executing a single node
/// </summary>
public class NodeExecutionResult
{
    /// <summary>
    /// Node ID that was executed
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Node type
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// When this node started executing
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this node completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Status of this node execution
    /// </summary>
    public NodeExecutionStatus Status { get; set; } = NodeExecutionStatus.Running;

    /// <summary>
    /// Input data received by the node
    /// </summary>
    public Dictionary<string, object>? InputData { get; set; }

    /// <summary>
    /// Output data produced by the node
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// Error message if node execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long DurationMs => CompletedAt.HasValue
        ? (long)(CompletedAt.Value - StartedAt).TotalMilliseconds
        : 0;
}

/// <summary>
/// Status of a node execution
/// </summary>
public enum NodeExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}
