using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that sets variables in the workflow context
/// </summary>
public class SetVariableNode : BaseWorkflowNode
{
    public override string NodeType => "SetVariable";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            var variableName = resolvedInputs.GetValueOrDefault("name")?.ToString();
            var variableValue = resolvedInputs.GetValueOrDefault("value");

            if (string.IsNullOrEmpty(variableName))
            {
                return CreateFailureResult(nodeConfig, "Variable name is required", resolvedInputs);
            }

            // Set the variable in the context
            context.Data[variableName] = variableValue ?? string.Empty;

            var outputData = new Dictionary<string, object>
            {
                ["variableName"] = variableName,
                ["variableValue"] = variableValue ?? string.Empty,
                ["success"] = true
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

        if (!nodeConfig.Inputs.ContainsKey("name"))
        {
            errors.Add("SetVariable node requires 'name' input");
        }

        if (!nodeConfig.Inputs.ContainsKey("value"))
        {
            errors.Add("SetVariable node requires 'value' input");
        }

        return errors;
    }
}
