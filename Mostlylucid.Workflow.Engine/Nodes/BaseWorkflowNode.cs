using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Base class for workflow nodes with common functionality
/// </summary>
public abstract class BaseWorkflowNode : IWorkflowNode
{
    public abstract string NodeType { get; }

    public abstract Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);

    public virtual Task<List<string>> ValidateAsync(WorkflowNode nodeConfig)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(nodeConfig.Type))
        {
            errors.Add("Node type is required");
        }

        if (string.IsNullOrEmpty(nodeConfig.Name))
        {
            errors.Add("Node name is required");
        }

        return Task.FromResult(errors);
    }

    /// <summary>
    /// Helper to resolve template variables in strings
    /// </summary>
    protected string ResolveTemplate(string template, WorkflowExecutionContext context)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;

        // Replace {{variable}} with actual values from context
        var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\{\{([^}]+)\}\}");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var variable = match.Groups[1].Value.Trim();
            if (context.Data.TryGetValue(variable, out var value))
            {
                result = result.Replace(match.Value, value?.ToString() ?? string.Empty);
            }
        }

        return result;
    }

    /// <summary>
    /// Helper to resolve all templates in a dictionary
    /// </summary>
    protected Dictionary<string, object> ResolveTemplates(
        Dictionary<string, object> inputs,
        WorkflowExecutionContext context)
    {
        var result = new Dictionary<string, object>();

        foreach (var (key, value) in inputs)
        {
            if (value is string strValue)
            {
                result[key] = ResolveTemplate(strValue, context);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a successful node execution result
    /// </summary>
    protected NodeExecutionResult CreateSuccessResult(
        WorkflowNode nodeConfig,
        Dictionary<string, object>? outputData = null,
        Dictionary<string, object>? inputData = null)
    {
        return new NodeExecutionResult
        {
            NodeId = nodeConfig.Id,
            NodeType = nodeConfig.Type,
            Status = NodeExecutionStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            OutputData = outputData,
            InputData = inputData
        };
    }

    /// <summary>
    /// Creates a failed node execution result
    /// </summary>
    protected NodeExecutionResult CreateFailureResult(
        WorkflowNode nodeConfig,
        string errorMessage,
        Dictionary<string, object>? inputData = null)
    {
        return new NodeExecutionResult
        {
            NodeId = nodeConfig.Id,
            NodeType = nodeConfig.Type,
            Status = NodeExecutionStatus.Failed,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            InputData = inputData
        };
    }
}
