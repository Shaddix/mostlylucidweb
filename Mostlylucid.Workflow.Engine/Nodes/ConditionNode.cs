using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that evaluates conditions and branches execution
/// </summary>
public class ConditionNode : BaseWorkflowNode
{
    public override string NodeType => "Condition";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            var condition = resolvedInputs.GetValueOrDefault("condition")?.ToString();
            if (string.IsNullOrEmpty(condition))
            {
                return CreateFailureResult(nodeConfig, "Condition is required", resolvedInputs);
            }

            // Evaluate the condition
            var result = EvaluateCondition(condition, context);

            var outputData = new Dictionary<string, object>
            {
                ["result"] = result,
                ["condition"] = condition,
                ["branch"] = result ? "true" : "false"
            };

            // Set context variable for routing
            context.Data["conditionResult"] = result;

            return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(nodeConfig, ex.Message, nodeConfig.Inputs);
        }
    }

    /// <summary>
    /// Evaluate a condition expression
    /// </summary>
    private bool EvaluateCondition(string condition, WorkflowExecutionContext context)
    {
        try
        {
            // Replace variables in condition
            var resolvedCondition = ResolveTemplate(condition, context);

            // Support various comparison operators
            if (resolvedCondition.Contains("=="))
            {
                var parts = resolvedCondition.Split("==", StringSplitOptions.TrimEntries);
                return parts[0].Trim() == parts[1].Trim();
            }
            else if (resolvedCondition.Contains("!="))
            {
                var parts = resolvedCondition.Split("!=", StringSplitOptions.TrimEntries);
                return parts[0].Trim() != parts[1].Trim();
            }
            else if (resolvedCondition.Contains(">="))
            {
                var parts = resolvedCondition.Split(">=", StringSplitOptions.TrimEntries);
                if (double.TryParse(parts[0].Trim(), out var left) &&
                    double.TryParse(parts[1].Trim(), out var right))
                {
                    return left >= right;
                }
            }
            else if (resolvedCondition.Contains("<="))
            {
                var parts = resolvedCondition.Split("<=", StringSplitOptions.TrimEntries);
                if (double.TryParse(parts[0].Trim(), out var left) &&
                    double.TryParse(parts[1].Trim(), out var right))
                {
                    return left <= right;
                }
            }
            else if (resolvedCondition.Contains(">"))
            {
                var parts = resolvedCondition.Split(">", StringSplitOptions.TrimEntries);
                if (double.TryParse(parts[0].Trim(), out var left) &&
                    double.TryParse(parts[1].Trim(), out var right))
                {
                    return left > right;
                }
            }
            else if (resolvedCondition.Contains("<"))
            {
                var parts = resolvedCondition.Split("<", StringSplitOptions.TrimEntries);
                if (double.TryParse(parts[0].Trim(), out var left) &&
                    double.TryParse(parts[1].Trim(), out var right))
                {
                    return left < right;
                }
            }
            else if (resolvedCondition.Contains("contains"))
            {
                var parts = resolvedCondition.Split("contains", StringSplitOptions.TrimEntries);
                return parts[0].Trim().Contains(parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }
            else if (resolvedCondition.Contains("startswith"))
            {
                var parts = resolvedCondition.Split("startswith", StringSplitOptions.TrimEntries);
                return parts[0].Trim().StartsWith(parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }
            else if (resolvedCondition.Contains("endswith"))
            {
                var parts = resolvedCondition.Split("endswith", StringSplitOptions.TrimEntries);
                return parts[0].Trim().EndsWith(parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }

            // If no operator found, try to parse as boolean
            if (bool.TryParse(resolvedCondition, out var boolValue))
            {
                return boolValue;
            }

            // Check if variable exists and is truthy
            if (context.Data.TryGetValue(resolvedCondition, out var value))
            {
                if (value is bool b) return b;
                if (value is string s) return !string.IsNullOrEmpty(s);
                if (value is int i) return i != 0;
                return value != null;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<string>> ValidateAsync(WorkflowNode nodeConfig)
    {
        var errors = await base.ValidateAsync(nodeConfig);

        if (!nodeConfig.Inputs.ContainsKey("condition"))
        {
            errors.Add("Condition node requires 'condition' input");
        }

        return errors;
    }
}
