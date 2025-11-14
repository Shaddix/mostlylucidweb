using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Workflow.Demo.Services;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;
using System.Text.Json;

namespace Mostlylucid.Workflow.Demo.Controllers;

public class WorkflowController : Controller
{
    private readonly WorkflowCacheService _cacheService;
    private readonly IWorkflowExecutor _executor;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        WorkflowCacheService cacheService,
        IWorkflowExecutor executor,
        ILogger<WorkflowController> logger)
    {
        _cacheService = cacheService;
        _executor = executor;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var workflows = _cacheService.GetAllWorkflows();
        if (Request.IsHtmx())
        {
            return PartialView("_WorkflowList", workflows);
        }
        return View(workflows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (Request.IsHtmx())
        {
            return PartialView("_CreateWorkflow");
        }
        return View();
    }

    [HttpPost]
    public IActionResult Create(string name, string description)
    {
        var workflow = new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Nodes = new List<WorkflowNode>(),
            IsEnabled = true
        };

        _cacheService.SaveWorkflow(workflow);

        if (Request.IsHtmx())
        {
            Response.Headers.Append("HX-Redirect", $"/Workflow/Edit/{workflow.Id}");
            return Ok();
        }

        return RedirectToAction("Edit", new { id = workflow.Id });
    }

    [HttpGet]
    public IActionResult Edit(string id)
    {
        var workflow = _cacheService.GetWorkflow(id);
        if (workflow == null)
        {
            return NotFound();
        }

        return View(workflow);
    }

    [HttpPost]
    public IActionResult Save([FromBody] WorkflowDefinition workflow)
    {
        try
        {
            _cacheService.SaveWorkflow(workflow);
            _cacheService.TouchCache(); // Extend expiration

            return Json(new { success = true, message = "Workflow saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete]
    public IActionResult Delete(string id)
    {
        _cacheService.DeleteWorkflow(id);

        if (Request.IsHtmx())
        {
            return Ok();
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Execute(string id, [FromBody] Dictionary<string, object>? inputData = null)
    {
        try
        {
            var workflow = _cacheService.GetWorkflow(id);
            if (workflow == null)
            {
                return NotFound();
            }

            var execution = await _executor.ExecuteAsync(workflow, inputData, "Manual");

            return Json(new
            {
                success = true,
                executionId = execution.Id,
                status = execution.Status.ToString(),
                output = execution.OutputData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Touch()
    {
        _cacheService.TouchCache();
        return Ok();
    }
}
