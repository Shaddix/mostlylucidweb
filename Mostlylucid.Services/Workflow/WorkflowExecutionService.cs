using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Services.Workflow;

/// <summary>
/// Service for executing and tracking workflow executions
/// </summary>
public class WorkflowExecutionService
{
    private readonly MostlylucidDbContext _context;
    private readonly IWorkflowExecutor _executor;
    private readonly WorkflowService _workflowService;
    private readonly ILogger<WorkflowExecutionService> _logger;

    public WorkflowExecutionService(
        MostlylucidDbContext context,
        IWorkflowExecutor executor,
        WorkflowService workflowService,
        ILogger<WorkflowExecutionService> logger)
    {
        _context = context;
        _executor = executor;
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a workflow by ID
    /// </summary>
    public async Task<WorkflowExecution> ExecuteWorkflowAsync(
        string workflowId,
        Dictionary<string, object>? inputData = null,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowService.GetByIdAsync(workflowId);
        if (workflow == null)
        {
            throw new InvalidOperationException($"Workflow {workflowId} not found");
        }

        if (!workflow.IsEnabled)
        {
            throw new InvalidOperationException($"Workflow {workflowId} is disabled");
        }

        _logger.LogInformation("Starting execution of workflow {WorkflowId}", workflowId);

        var execution = await _executor.ExecuteAsync(workflow, inputData, triggeredBy, cancellationToken);

        // Save execution to database
        await SaveExecutionAsync(execution, workflow);

        return execution;
    }

    /// <summary>
    /// Get execution by ID
    /// </summary>
    public async Task<WorkflowExecution?> GetExecutionAsync(string executionId)
    {
        var entity = await _context.WorkflowExecutions
            .Include(e => e.WorkflowDefinition)
            .FirstOrDefaultAsync(e => e.ExecutionId == executionId);

        return entity == null ? null : MapFromEntity(entity);
    }

    /// <summary>
    /// Get executions for a workflow
    /// </summary>
    public async Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(
        string workflowId,
        int skip = 0,
        int take = 50)
    {
        var entities = await _context.WorkflowExecutions
            .Include(e => e.WorkflowDefinition)
            .Where(e => e.WorkflowDefinition.WorkflowId == workflowId)
            .OrderByDescending(e => e.StartedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return entities.Select(MapFromEntity).ToList();
    }

    /// <summary>
    /// Get recent executions
    /// </summary>
    public async Task<List<WorkflowExecution>> GetRecentExecutionsAsync(int count = 20)
    {
        var entities = await _context.WorkflowExecutions
            .Include(e => e.WorkflowDefinition)
            .OrderByDescending(e => e.StartedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(MapFromEntity).ToList();
    }

    /// <summary>
    /// Cancel a running execution
    /// </summary>
    public async Task<bool> CancelExecutionAsync(string executionId)
    {
        await _executor.CancelAsync(executionId);

        var updated = await _context.WorkflowExecutions
            .Where(e => e.ExecutionId == executionId)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.Status, "Cancelled")
                                       .SetProperty(x => x.CompletedAt, DateTime.UtcNow));

        return updated > 0;
    }

    /// <summary>
    /// Get execution statistics for a workflow
    /// </summary>
    public async Task<WorkflowExecutionStats> GetExecutionStatsAsync(string workflowId)
    {
        var executions = await _context.WorkflowExecutions
            .Where(e => e.WorkflowDefinition.WorkflowId == workflowId)
            .Select(e => new { e.Status, e.StartedAt, e.CompletedAt })
            .ToListAsync();

        return new WorkflowExecutionStats
        {
            TotalExecutions = executions.Count,
            SuccessfulExecutions = executions.Count(e => e.Status == "Completed"),
            FailedExecutions = executions.Count(e => e.Status == "Failed"),
            AverageDurationMs = executions
                .Where(e => e.StartedAt.HasValue && e.CompletedAt.HasValue)
                .Select(e => (e.CompletedAt!.Value - e.StartedAt!.Value).TotalMilliseconds)
                .DefaultIfEmpty(0)
                .Average()
        };
    }

    /// <summary>
    /// Save execution to database
    /// </summary>
    private async Task SaveExecutionAsync(WorkflowExecution execution, WorkflowDefinition workflow)
    {
        var workflowEntity = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.WorkflowId == workflow.Id);

        if (workflowEntity == null)
        {
            _logger.LogWarning("Workflow {WorkflowId} not found in database", workflow.Id);
            return;
        }

        var entity = new WorkflowExecutionEntity
        {
            ExecutionId = execution.Id,
            WorkflowDefinitionId = workflowEntity.Id,
            Status = execution.Status.ToString(),
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            InputDataJson = execution.InputData != null
                ? JsonSerializer.Serialize(execution.InputData)
                : null,
            OutputDataJson = execution.OutputData != null
                ? JsonSerializer.Serialize(execution.OutputData)
                : null,
            NodeExecutionsJson = JsonSerializer.Serialize(execution.NodeExecutions),
            ContextJson = JsonSerializer.Serialize(execution.Context),
            ErrorMessage = execution.ErrorMessage,
            ErrorStackTrace = execution.ErrorStackTrace,
            TriggeredBy = execution.TriggeredBy
        };

        await _context.WorkflowExecutions.AddAsync(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved execution {ExecutionId} for workflow {WorkflowId}",
            execution.Id, workflow.Id);
    }

    /// <summary>
    /// Map entity to model
    /// </summary>
    private WorkflowExecution MapFromEntity(WorkflowExecutionEntity entity)
    {
        return new WorkflowExecution
        {
            Id = entity.ExecutionId,
            WorkflowId = entity.WorkflowDefinition.WorkflowId,
            Status = Enum.Parse<WorkflowExecutionStatus>(entity.Status),
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            InputData = entity.InputDataJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.InputDataJson)
                : null,
            OutputData = entity.OutputDataJson != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.OutputDataJson)
                : null,
            NodeExecutions = JsonSerializer.Deserialize<List<NodeExecutionResult>>(
                                 entity.NodeExecutionsJson)
                             ?? new List<NodeExecutionResult>(),
            Context = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ContextJson)
                      ?? new Dictionary<string, object>(),
            ErrorMessage = entity.ErrorMessage,
            ErrorStackTrace = entity.ErrorStackTrace,
            TriggeredBy = entity.TriggeredBy
        };
    }
}

/// <summary>
/// Statistics for workflow executions
/// </summary>
public class WorkflowExecutionStats
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double AverageDurationMs { get; set; }
}
