using System.Text.Json;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that transforms data using simple operations
/// </summary>
public class TransformNode : BaseWorkflowNode
{
    public override string NodeType => "Transform";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            var operation = resolvedInputs.GetValueOrDefault("operation")?.ToString();
            var inputData = resolvedInputs.GetValueOrDefault("data");

            if (string.IsNullOrEmpty(operation))
            {
                return CreateFailureResult(nodeConfig, "Operation is required", resolvedInputs);
            }

            object result = operation.ToLower() switch
            {
                "uppercase" => inputData?.ToString()?.ToUpper() ?? string.Empty,
                "lowercase" => inputData?.ToString()?.ToLower() ?? string.Empty,
                "trim" => inputData?.ToString()?.Trim() ?? string.Empty,
                "length" => inputData?.ToString()?.Length ?? 0,
                "json_parse" => JsonSerializer.Deserialize<Dictionary<string, object>>(inputData?.ToString() ?? "{}"),
                "json_stringify" => JsonSerializer.Serialize(inputData),
                _ => inputData ?? string.Empty
            };

            var outputData = new Dictionary<string, object>
            {
                ["result"] = result,
                ["operation"] = operation
            };

            // Apply output mappings
            foreach (var (outputKey, templateValue) in nodeConfig.Outputs)
            {
                context.Data[outputKey] = result;
            }

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

        if (!nodeConfig.Inputs.ContainsKey("operation"))
        {
            errors.Add("Transform node requires 'operation' input");
        }

        return errors;
    }
}
