using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Interfaces;

/// <summary>
/// Interface that all workflow nodes must implement
/// </summary>
public interface IWorkflowNode
{
    /// <summary>
    /// The type identifier for this node
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// Executes the node logic
    /// </summary>
    /// <param name="nodeConfig">The node configuration from the workflow definition</param>
    /// <param name="context">The current execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the node execution</returns>
    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the node configuration
    /// </summary>
    /// <param name="nodeConfig">The node configuration to validate</param>
    /// <returns>List of validation errors (empty if valid)</returns>
    Task<List<string>> ValidateAsync(WorkflowNode nodeConfig);
}

/// <summary>
/// Context passed during workflow execution
/// </summary>
public class WorkflowExecutionContext
{
    /// <summary>
    /// Current workflow execution
    /// </summary>
    public WorkflowExecution Execution { get; set; } = new();

    /// <summary>
    /// Workflow definition being executed
    /// </summary>
    public WorkflowDefinition Workflow { get; set; } = new();

    /// <summary>
    /// Shared data between nodes
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Output from previous nodes (keyed by node ID)
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> NodeOutputs { get; set; } = new();

    /// <summary>
    /// Services available during execution
    /// </summary>
    public IServiceProvider Services { get; set; } = null!;
}
