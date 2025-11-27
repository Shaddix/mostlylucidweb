using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Interfaces;

/// <summary>
/// Interface for executing workflows
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Executes a workflow
    /// </summary>
    /// <param name="workflow">The workflow to execute</param>
    /// <param name="inputData">Initial input data</param>
    /// <param name="triggeredBy">Who/what triggered the workflow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The workflow execution result</returns>
    Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? inputData = null,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused workflow execution
    /// </summary>
    /// <param name="executionId">ID of the execution to resume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated workflow execution</returns>
    Task<WorkflowExecution> ResumeAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running workflow execution
    /// </summary>
    /// <param name="executionId">ID of the execution to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CancelAsync(string executionId, CancellationToken cancellationToken = default);
}
