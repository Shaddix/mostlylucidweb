using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that delays execution for a specified duration
/// </summary>
public class DelayNode : BaseWorkflowNode
{
    public override string NodeType => "Delay";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            // Get delay duration
            var durationMs = 0;
            if (resolvedInputs.TryGetValue("durationMs", out var durationValue))
            {
                if (durationValue is int intValue)
                {
                    durationMs = intValue;
                }
                else if (int.TryParse(durationValue?.ToString(), out var parsedValue))
                {
                    durationMs = parsedValue;
                }
            }

            if (durationMs <= 0)
            {
                return CreateFailureResult(nodeConfig, "Invalid duration", resolvedInputs);
            }

            // Wait for the specified duration
            await Task.Delay(durationMs, cancellationToken);

            var outputData = new Dictionary<string, object>
            {
                ["delayedMs"] = durationMs,
                ["completedAt"] = DateTime.UtcNow.ToString("O")
            };

            return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(nodeConfig, ex.Message, nodeConfig.Inputs);
        }
    }

    public override async Task<List<string>> ValidateAsync(WorkflowNode nodeConfig)
    {
        var errors = await base.ValidateAsync(nodeConfig);

        if (!nodeConfig.Inputs.ContainsKey("durationMs"))
        {
            errors.Add("Delay node requires 'durationMs' input");
        }

        return errors;
    }
}
