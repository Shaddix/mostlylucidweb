using Microsoft.Extensions.Logging;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Execution;

/// <summary>
/// Executes workflow definitions
/// </summary>
public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly ILogger<WorkflowExecutor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, CancellationTokenSource> _runningExecutions = new();

    public WorkflowExecutor(
        INodeRegistry nodeRegistry,
        ILogger<WorkflowExecutor> logger,
        IServiceProvider serviceProvider)
    {
        _nodeRegistry = nodeRegistry;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Executes a workflow
    /// </summary>
    public async Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? inputData = null,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default)
    {
        var execution = new WorkflowExecution
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowId = workflow.Id,
            Status = WorkflowExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            InputData = inputData,
            TriggeredBy = triggeredBy,
            Context = new Dictionary<string, object>(inputData ?? new Dictionary<string, object>())
        };

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningExecutions[execution.Id] = cts;

        try
        {
            _logger.LogInformation("Starting workflow execution {ExecutionId} for workflow {WorkflowId}",
                execution.Id, workflow.Id);

            // Validate workflow
            var validationErrors = ValidateWorkflow(workflow);
            if (validationErrors.Any())
            {
                throw new InvalidOperationException(
                    $"Workflow validation failed: {string.Join(", ", validationErrors)}");
            }

            // Create execution context
            var context = new WorkflowExecutionContext
            {
                Execution = execution,
                Workflow = workflow,
                Data = execution.Context,
                Services = _serviceProvider
            };

            // Find start node
            var startNode = workflow.Nodes.FirstOrDefault(n => n.Id == workflow.StartNodeId);
            if (startNode == null)
            {
                throw new InvalidOperationException("No start node found in workflow");
            }

            // Execute workflow starting from the start node
            await ExecuteNodeRecursiveAsync(startNode, context, cts.Token);

            execution.Status = WorkflowExecutionStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.OutputData = context.Data;

            _logger.LogInformation("Workflow execution {ExecutionId} completed successfully",
                execution.Id);
        }
        catch (OperationCanceledException)
        {
            execution.Status = WorkflowExecutionStatus.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Workflow execution {ExecutionId} was cancelled", execution.Id);
        }
        catch (Exception ex)
        {
            execution.Status = WorkflowExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;
            execution.ErrorStackTrace = ex.StackTrace;

            _logger.LogError(ex, "Workflow execution {ExecutionId} failed", execution.Id);
        }
        finally
        {
            _runningExecutions.Remove(execution.Id);
            cts.Dispose();
        }

        return execution;
    }

    /// <summary>
    /// Executes a node and its downstream nodes recursively
    /// </summary>
    private async Task ExecuteNodeRecursiveAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Get node implementation
        var node = _nodeRegistry.GetNode(nodeConfig.Type);
        if (node == null)
        {
            throw new InvalidOperationException($"Node type '{nodeConfig.Type}' not registered");
        }

        _logger.LogDebug("Executing node {NodeId} of type {NodeType}",
            nodeConfig.Id, nodeConfig.Type);

        // Execute node
        var result = await node.ExecuteAsync(nodeConfig, context, cancellationToken);

        // Add to execution history
        context.Execution.NodeExecutions.Add(result);

        // Store node outputs for downstream nodes
        if (result.OutputData != null)
        {
            context.NodeOutputs[nodeConfig.Id] = result.OutputData;
        }

        // Handle node execution result
        if (result.Status == NodeExecutionStatus.Failed)
        {
            _logger.LogError("Node {NodeId} failed: {ErrorMessage}",
                nodeConfig.Id, result.ErrorMessage);

            // Check for error handler connection
            var errorConnection = context.Workflow.Connections
                .FirstOrDefault(c => c.SourceNodeId == nodeConfig.Id && c.SourceOutput == "error");

            if (errorConnection != null)
            {
                var errorNode = context.Workflow.Nodes.FirstOrDefault(n => n.Id == errorConnection.TargetNodeId);
                if (errorNode != null)
                {
                    await ExecuteNodeRecursiveAsync(errorNode, context, cancellationToken);
                    return;
                }
            }

            throw new Exception($"Node {nodeConfig.Id} failed: {result.ErrorMessage}");
        }

        // Find and execute next nodes
        var outgoingConnections = context.Workflow.Connections
            .Where(c => c.SourceNodeId == nodeConfig.Id && c.SourceOutput != "error")
            .ToList();

        foreach (var connection in outgoingConnections)
        {
            // Check connection condition if specified
            if (!string.IsNullOrEmpty(connection.Condition))
            {
                if (!EvaluateCondition(connection.Condition, context))
                {
                    _logger.LogDebug("Connection condition not met for {ConnectionId}", connection.Id);
                    continue;
                }
            }

            // Find target node
            var targetNode = context.Workflow.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
            if (targetNode != null)
            {
                // Pass output data to next node
                if (result.OutputData != null)
                {
                    foreach (var (key, value) in result.OutputData)
                    {
                        context.Data[key] = value;
                    }
                }

                await ExecuteNodeRecursiveAsync(targetNode, context, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Evaluates a simple condition expression
    /// </summary>
    private bool EvaluateCondition(string condition, WorkflowExecutionContext context)
    {
        // Simple condition evaluation (can be enhanced with a proper expression evaluator)
        // Format: "variable == value" or "variable != value"
        try
        {
            var parts = condition.Split(new[] { "==", "!=" }, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return false;

            var variable = parts[0].Trim();
            var expectedValue = parts[1].Trim().Trim('"', '\'');
            var isEquals = condition.Contains("==");

            if (context.Data.TryGetValue(variable, out var actualValue))
            {
                var actualStr = actualValue?.ToString() ?? string.Empty;
                return isEquals
                    ? actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase)
                    : !actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a workflow definition
    /// </summary>
    private List<string> ValidateWorkflow(WorkflowDefinition workflow)
    {
        var errors = new List<string>();

        if (!workflow.Nodes.Any())
        {
            errors.Add("Workflow must have at least one node");
        }

        if (string.IsNullOrEmpty(workflow.StartNodeId))
        {
            errors.Add("Workflow must have a start node");
        }

        if (!string.IsNullOrEmpty(workflow.StartNodeId) &&
            !workflow.Nodes.Any(n => n.Id == workflow.StartNodeId))
        {
            errors.Add($"Start node '{workflow.StartNodeId}' not found in workflow nodes");
        }

        // Check for invalid connections
        foreach (var connection in workflow.Connections)
        {
            if (!workflow.Nodes.Any(n => n.Id == connection.SourceNodeId))
            {
                errors.Add($"Connection source node '{connection.SourceNodeId}' not found");
            }

            if (!workflow.Nodes.Any(n => n.Id == connection.TargetNodeId))
            {
                errors.Add($"Connection target node '{connection.TargetNodeId}' not found");
            }
        }

        return errors;
    }

    /// <summary>
    /// Resumes a paused workflow execution
    /// </summary>
    public Task<WorkflowExecution> ResumeAsync(string executionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Resume functionality not yet implemented");
    }

    /// <summary>
    /// Cancels a running workflow execution
    /// </summary>
    public Task CancelAsync(string executionId, CancellationToken cancellationToken = default)
    {
        if (_runningExecutions.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled workflow execution {ExecutionId}", executionId);
        }

        return Task.CompletedTask;
    }
}
