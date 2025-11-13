using Microsoft.Extensions.Logging;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that logs messages (useful for debugging workflows)
/// </summary>
public class LogNode : BaseWorkflowNode
{
    private readonly ILogger<LogNode> _logger;

    public LogNode(ILogger<LogNode> logger)
    {
        _logger = logger;
    }

    public override string NodeType => "Log";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            var message = resolvedInputs.GetValueOrDefault("message")?.ToString() ?? "No message";
            var level = resolvedInputs.GetValueOrDefault("level")?.ToString()?.ToLower() ?? "info";

            // Log at appropriate level
            switch (level)
            {
                case "debug":
                    _logger.LogDebug("[Workflow {WorkflowId}] {Message}", context.Workflow.Id, message);
                    break;
                case "info":
                    _logger.LogInformation("[Workflow {WorkflowId}] {Message}", context.Workflow.Id, message);
                    break;
                case "warning":
                    _logger.LogWarning("[Workflow {WorkflowId}] {Message}", context.Workflow.Id, message);
                    break;
                case "error":
                    _logger.LogError("[Workflow {WorkflowId}] {Message}", context.Workflow.Id, message);
                    break;
                default:
                    _logger.LogInformation("[Workflow {WorkflowId}] {Message}", context.Workflow.Id, message);
                    break;
            }

            var outputData = new Dictionary<string, object>
            {
                ["message"] = message,
                ["level"] = level,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
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

        if (!nodeConfig.Inputs.ContainsKey("message"))
        {
            errors.Add("Log node requires 'message' input");
        }

        return errors;
    }
}
