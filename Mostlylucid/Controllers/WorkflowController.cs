using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Controllers;
using Mostlylucid.Services;
using Mostlylucid.Services.Workflow;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucidblog.Controllers;

[Route("workflow")]
public class WorkflowController : BaseController
{
    private readonly WorkflowService _workflowService;
    private readonly WorkflowExecutionService _executionService;

    public WorkflowController(
        BaseControllerService baseControllerService,
        WorkflowService workflowService,
        WorkflowExecutionService executionService,
        ILogger<WorkflowController> logger) : base(baseControllerService, logger)
    {
        _workflowService = workflowService;
        _executionService = executionService;
    }

    /// <summary>
    /// List all workflows
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var workflows = await _workflowService.GetAllAsync();

        if (Request.IsHtmx())
        {
            return PartialView("_WorkflowList", workflows);
        }

        return View("Index", workflows);
    }

    /// <summary>
    /// Show workflow editor (create new)
    /// </summary>
    [HttpGet("create")]
    public IActionResult Create()
    {
        var workflow = new WorkflowDefinition
        {
            Name = "New Workflow",
            StartNodeId = null,
            Nodes = new List<WorkflowNode>(),
            Connections = new List<NodeConnection>()
        };

        return View("Editor", workflow);
    }

    /// <summary>
    /// Show workflow editor (edit existing)
    /// </summary>
    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);

        if (workflow == null)
        {
            return NotFound();
        }

        return View("Editor", workflow);
    }

    /// <summary>
    /// Save workflow (create or update)
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] WorkflowDefinition workflow)
    {
        try
        {
            var existing = await _workflowService.GetByIdAsync(workflow.Id);

            WorkflowDefinition saved;
            if (existing == null)
            {
                saved = await _workflowService.CreateAsync(workflow);
            }
            else
            {
                saved = await _workflowService.UpdateAsync(workflow)
                        ?? throw new InvalidOperationException("Failed to update workflow");
            }

            return Json(new
            {
                success = true,
                workflowId = saved.Id,
                message = "Workflow saved successfully"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving workflow {WorkflowId}", workflow.Id);
            return Json(new
            {
                success = false,
                message = $"Error saving workflow: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Delete a workflow
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var deleted = await _workflowService.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return Json(new { success = true, message = "Workflow deleted successfully" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting workflow {WorkflowId}", id);
            return Json(new { success = false, message = $"Error deleting workflow: {ex.Message}" });
        }
    }

    /// <summary>
    /// Execute a workflow
    /// </summary>
    [HttpPost("execute/{id}")]
    public async Task<IActionResult> Execute(string id, [FromBody] Dictionary<string, object>? inputData = null)
    {
        try
        {
            var execution = await _executionService.ExecuteWorkflowAsync(
                id,
                inputData,
                User.Identity?.Name ?? "Anonymous");

            return Json(new
            {
                success = true,
                executionId = execution.Id,
                status = execution.Status.ToString(),
                message = "Workflow execution started"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing workflow {WorkflowId}", id);
            return Json(new { success = false, message = $"Error executing workflow: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get execution details
    /// </summary>
    [HttpGet("execution/{id}")]
    public async Task<IActionResult> Execution(string id)
    {
        var execution = await _executionService.GetExecutionAsync(id);

        if (execution == null)
        {
            return NotFound();
        }

        if (Request.IsHtmx())
        {
            return PartialView("_ExecutionDetails", execution);
        }

        return View("Execution", execution);
    }

    /// <summary>
    /// Get workflow execution history
    /// </summary>
    [HttpGet("{id}/executions")]
    public async Task<IActionResult> Executions(string id, int page = 1, int pageSize = 20)
    {
        var executions = await _executionService.GetWorkflowExecutionsAsync(
            id,
            (page - 1) * pageSize,
            pageSize);

        if (Request.IsHtmx())
        {
            return PartialView("_ExecutionList", executions);
        }

        return View("Executions", executions);
    }

    /// <summary>
    /// Get workflow stats
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<IActionResult> Stats(string id)
    {
        try
        {
            var stats = await _executionService.GetExecutionStatsAsync(id);
            return Json(new { success = true, stats });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting workflow stats {WorkflowId}", id);
            return Json(new { success = false, message = $"Error getting stats: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get available node types
    /// </summary>
    [HttpGet("node-types")]
    public IActionResult NodeTypes()
    {
        var nodeTypes = new[]
        {
            new
            {
                type = "HttpRequest",
                name = "HTTP Request",
                description = "Make HTTP API calls",
                icon = "🌐",
                color = "#10B981",
                inputs = new[] { "url", "method", "headers", "body" },
                outputs = new[] { "statusCode", "body", "headers", "isSuccess" }
            },
            new
            {
                type = "Transform",
                name = "Transform Data",
                description = "Transform and manipulate data",
                icon = "🔄",
                color = "#3B82F6",
                inputs = new[] { "operation", "data" },
                outputs = new[] { "result" }
            },
            new
            {
                type = "Delay",
                name = "Delay",
                description = "Wait for a specified duration",
                icon = "⏱️",
                color = "#F59E0B",
                inputs = new[] { "durationMs" },
                outputs = new[] { "delayedMs", "completedAt" }
            },
            new
            {
                type = "Condition",
                name = "Condition",
                description = "Branch execution based on conditions",
                icon = "🔀",
                color = "#8B5CF6",
                inputs = new[] { "condition" },
                outputs = new[] { "true", "false" }
            }
        };

        return Json(nodeTypes);
    }
}
