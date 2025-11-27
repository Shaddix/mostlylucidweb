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
        InitializeExampleWorkflows();
    }

    private void InitializeExampleWorkflows()
    {
        var workflows = GetAllWorkflows();
        if (workflows.Any()) return; // Already initialized

        // Example 1: Simple Logging Workflow
        SaveWorkflow(new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Simple Logger",
            Description = "Basic workflow that logs a message",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "SetVariable",
                    Name = "Set Message",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 100,
                        ["variableName"] = "message",
                        ["value"] = "Hello from workflow!"
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Log Message",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 250,
                        ["message"] = "{{message}}"
                    }
                }
            }
        });

        // Example 2: HTTP Request Workflow
        SaveWorkflow(new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "API Fetcher",
            Description = "Fetches data from an API and logs the result",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "HttpRequest",
                    Name = "Fetch Users",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 100,
                        ["url"] = "https://jsonplaceholder.typicode.com/users",
                        ["method"] = "GET"
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Transform",
                    Name = "Extract Names",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 250,
                        ["transform"] = "response.data.map(u => u.name)"
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Log Names",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 400,
                        ["message"] = "User names: {{names}}"
                    }
                }
            }
        });

        // Example 3: Conditional Workflow
        SaveWorkflow(new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Conditional Logic",
            Description = "Demonstrates branching based on conditions",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "SetVariable",
                    Name = "Set Temperature",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 100,
                        ["variableName"] = "temperature",
                        ["value"] = 25
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Condition",
                    Name = "Check Temperature",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 250,
                        ["condition"] = "{{temperature}} > 20"
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Log Hot",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 50,
                        ["y"] = 400,
                        ["message"] = "It's hot! Temperature: {{temperature}}"
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Log Cold",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 300,
                        ["y"] = 400,
                        ["message"] = "It's cold! Temperature: {{temperature}}"
                    }
                }
            }
        });

        // Example 4: Delayed Workflow
        SaveWorkflow(new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Scheduled Task",
            Description = "Workflow with delays between actions",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Start",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 100,
                        ["message"] = "Starting task..."
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Delay",
                    Name = "Wait 5 seconds",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 250,
                        ["duration"] = 5000
                    }
                },
                new WorkflowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "Log",
                    Name = "Complete",
                    Inputs = new Dictionary<string, object>
                    {
                        ["x"] = 150,
                        ["y"] = 400,
                        ["message"] = "Task completed after delay!"
                    }
                }
            }
        });
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
