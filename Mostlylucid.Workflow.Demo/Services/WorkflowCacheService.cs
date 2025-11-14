using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Demo.Services;

public class WorkflowCacheService
{
    private readonly IMemoryCache _cache;
    private const string WorkflowListKey = "workflow_list";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(10);

    public WorkflowCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public List<WorkflowDefinition> GetAllWorkflows()
    {
        return _cache.GetOrCreate(WorkflowListKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            return new List<WorkflowDefinition>();
        }) ?? new List<WorkflowDefinition>();
    }

    public WorkflowDefinition? GetWorkflow(string workflowId)
    {
        var workflows = GetAllWorkflows();
        return workflows.FirstOrDefault(w => w.Id == workflowId);
    }

    public void SaveWorkflow(WorkflowDefinition workflow)
    {
        var workflows = GetAllWorkflows();

        var existing = workflows.FirstOrDefault(w => w.Id == workflow.Id);
        if (existing != null)
        {
            workflows.Remove(existing);
        }

        workflows.Add(workflow);

        _cache.Set(WorkflowListKey, workflows, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });
    }

    public void DeleteWorkflow(string workflowId)
    {
        var workflows = GetAllWorkflows();
        var workflow = workflows.FirstOrDefault(w => w.Id == workflowId);

        if (workflow != null)
        {
            workflows.Remove(workflow);
            _cache.Set(WorkflowListKey, workflows, new MemoryCacheEntryOptions
            {
                SlidingExpiration = SlidingExpiration
            });
        }
    }

    public void TouchCache()
    {
        // Refresh the sliding expiration
        var workflows = GetAllWorkflows();
        _cache.Set(WorkflowListKey, workflows, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });
    }
}
