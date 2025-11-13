using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Services.Workflow;

/// <summary>
/// Service for managing workflow definitions
/// </summary>
public class WorkflowService
{
    private readonly MostlylucidDbContext _context;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        MostlylucidDbContext context,
        ILogger<WorkflowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all workflows
    /// </summary>
    public async Task<List<WorkflowDefinition>> GetAllAsync(bool includeDisabled = false)
    {
        var query = _context.WorkflowDefinitions.AsQueryable();

        if (!includeDisabled)
        {
            query = query.Where(w => w.IsEnabled);
        }

        var entities = await query
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync();

        return entities.Select(MapFromEntity).ToList();
    }

    /// <summary>
    /// Get workflow by ID
    /// </summary>
    public async Task<WorkflowDefinition?> GetByIdAsync(string workflowId)
    {
        var entity = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        return entity == null ? null : MapFromEntity(entity);
    }

    /// <summary>
    /// Create a new workflow
    /// </summary>
    public async Task<WorkflowDefinition> CreateAsync(WorkflowDefinition workflow)
    {
        var entity = MapToEntity(workflow);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.WorkflowDefinitions.AddAsync(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created workflow {WorkflowId}: {Name}",
            workflow.Id, workflow.Name);

        return MapFromEntity(entity);
    }

    /// <summary>
    /// Update an existing workflow
    /// </summary>
    public async Task<WorkflowDefinition?> UpdateAsync(WorkflowDefinition workflow)
    {
        var entity = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.WorkflowId == workflow.Id);

        if (entity == null)
        {
            _logger.LogWarning("Workflow {WorkflowId} not found for update", workflow.Id);
            return null;
        }

        entity.Name = workflow.Name;
        entity.Description = workflow.Description;
        entity.Version = workflow.Version;
        entity.DefinitionJson = JsonSerializer.Serialize(workflow);
        entity.IsEnabled = workflow.IsEnabled;
        entity.Tags = workflow.Tags.ToArray();
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated workflow {WorkflowId}: {Name}",
            workflow.Id, workflow.Name);

        return MapFromEntity(entity);
    }

    /// <summary>
    /// Delete a workflow
    /// </summary>
    public async Task<bool> DeleteAsync(string workflowId)
    {
        var entity = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        if (entity == null)
        {
            return false;
        }

        _context.WorkflowDefinitions.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted workflow {WorkflowId}", workflowId);

        return true;
    }

    /// <summary>
    /// Enable/disable a workflow
    /// </summary>
    public async Task<bool> SetEnabledAsync(string workflowId, bool enabled)
    {
        var updated = await _context.WorkflowDefinitions
            .Where(w => w.WorkflowId == workflowId)
            .ExecuteUpdateAsync(w => w.SetProperty(x => x.IsEnabled, enabled)
                                       .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));

        return updated > 0;
    }

    /// <summary>
    /// Search workflows by tag
    /// </summary>
    public async Task<List<WorkflowDefinition>> SearchByTagAsync(string tag)
    {
        var entities = await _context.WorkflowDefinitions
            .Where(w => w.IsEnabled && w.Tags != null && w.Tags.Contains(tag))
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync();

        return entities.Select(MapFromEntity).ToList();
    }

    /// <summary>
    /// Map entity to model
    /// </summary>
    private WorkflowDefinition MapFromEntity(WorkflowDefinitionEntity entity)
    {
        return JsonSerializer.Deserialize<WorkflowDefinition>(entity.DefinitionJson)
               ?? throw new InvalidOperationException($"Failed to deserialize workflow {entity.WorkflowId}");
    }

    /// <summary>
    /// Map model to entity
    /// </summary>
    private WorkflowDefinitionEntity MapToEntity(WorkflowDefinition workflow)
    {
        return new WorkflowDefinitionEntity
        {
            WorkflowId = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Version = workflow.Version,
            DefinitionJson = JsonSerializer.Serialize(workflow),
            IsEnabled = workflow.IsEnabled,
            Tags = workflow.Tags.ToArray(),
            CreatedBy = workflow.CreatedBy
        };
    }
}
