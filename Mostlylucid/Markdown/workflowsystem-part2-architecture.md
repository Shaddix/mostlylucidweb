# Building a Workflow System with HTMX and ASP.NET Core - Part 2: Architecture and Core Engine
<!--category-- ASP.NET, HTMX, Alpine.js, Workflow -->
<datetime class="hidden">2025-01-15T14:00</datetime>

## Introduction

In [Part 1](/blog/workflowsystem-part1-introduction), we introduced the concept of building a custom workflow system. Now it's time to get our hands dirty! In this post, we'll build the core workflow engine - the heart of our system that executes node-based workflows.

By the end of this post, you'll have:
- A solid project structure
- Core workflow models (nodes, connections, definitions)
- A working execution engine
- Database persistence
- Several built-in node types

[TOC]

## Project Structure

We've organized our solution into focused projects for maintainability:

```
Mostlylucid.Workflow.Shared/        # Shared models and DTOs
├── Models/
│   ├── WorkflowNode.cs              # Node definition
│   ├── NodeConnection.cs            # Connections between nodes
│   ├── WorkflowDefinition.cs        # Complete workflow definition
│   └── WorkflowExecution.cs         # Execution tracking

Mostlylucid.Workflow.Engine/         # Core execution engine
├── Interfaces/
│   ├── IWorkflowNode.cs             # Node interface
│   ├── IWorkflowExecutor.cs         # Executor interface
│   └── INodeRegistry.cs             # Node registry interface
├── Execution/
│   ├── NodeRegistry.cs              # Registry for node types
│   └── WorkflowExecutor.cs          # Main execution engine
└── Nodes/
    ├── BaseWorkflowNode.cs          # Base node implementation
    ├── HttpRequestNode.cs           # HTTP API calls
    ├── TransformNode.cs             # Data transformation
    └── DelayNode.cs                 # Delay execution

Mostlylucid.Shared/Entities/         # Database entities (EF Core)
├── WorkflowDefinitionEntity.cs
├── WorkflowExecutionEntity.cs
└── WorkflowTriggerStateEntity.cs
```

## Core Models

### WorkflowNode

The `WorkflowNode` is the building block of all workflows. It's designed to be completely serializable to JSON:

```csharp
public class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Configuration
    public Dictionary<string, object> Inputs { get; set; } = new();
    public Dictionary<string, string> Outputs { get; set; } = new();
    public Dictionary<string, string> Conditions { get; set; } = new();

    // Visual properties
    public NodePosition Position { get; set; } = new();
    public NodeStyle Style { get; set; } = new();
}

public class NodeStyle
{
    public string BackgroundColor { get; set; } = "#3B82F6";
    public string TextColor { get; set; } = "#FFFFFF";
    public string BorderColor { get; set; } = "#2563EB";
    public string? Icon { get; set; }
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 100;
}
```

**Key Design Decisions:**
- **Inputs as Dictionary**: Flexible key-value configuration
- **Visual Properties**: Nodes know how to render themselves
- **Template Support**: Values can use `{{variable}}` syntax for dynamic data

### NodeConnection

Connections define how data flows between nodes:

```csharp
public class NodeConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string SourceOutput { get; set; } = "default";
    public string TargetInput { get; set; } = "default";
    public string? Condition { get; set; }
    public string? Label { get; set; }
}
```

This allows for:
- Multiple outputs per node (success, error, conditional branches)
- Named inputs/outputs for clarity
- Conditional connections ("only connect if X == Y")

### WorkflowDefinition

The complete workflow:

```csharp
public class WorkflowDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; } = 1;

    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<NodeConnection> Connections { get; set; } = new();

    public string? StartNodeId { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, object>? Variables { get; set; }
}
```

## The Workflow Executor

The `WorkflowExecutor` is the brain of our system. It interprets workflow definitions and executes them.

### Core Execution Logic

```csharp
public async Task<WorkflowExecution> ExecuteAsync(
    WorkflowDefinition workflow,
    Dictionary<string, object>? inputData = null,
    string? triggeredBy = null,
    CancellationToken cancellationToken = default)
{
    var execution = new WorkflowExecution
    {
        Id = Guid.NewGuid().ToString(),
        WorkflowId = workflow.Id,
        Status = WorkflowExecutionStatus.Running,
        StartedAt = DateTime.UtcNow,
        InputData = inputData,
        Context = new Dictionary<string, object>(inputData ?? new())
    };

    try
    {
        // Validate workflow
        var validationErrors = ValidateWorkflow(workflow);
        if (validationErrors.Any())
        {
            throw new InvalidOperationException(
                $"Workflow validation failed: {string.Join(", ", validationErrors)}");
        }

        // Create execution context
        var context = new WorkflowExecutionContext
        {
            Execution = execution,
            Workflow = workflow,
            Data = execution.Context,
            Services = _serviceProvider
        };

        // Find and execute start node
        var startNode = workflow.Nodes.FirstOrDefault(n => n.Id == workflow.StartNodeId);
        if (startNode == null)
        {
            throw new InvalidOperationException("No start node found");
        }

        await ExecuteNodeRecursiveAsync(startNode, context, cancellationToken);

        execution.Status = WorkflowExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.OutputData = context.Data;
    }
    catch (Exception ex)
    {
        execution.Status = WorkflowExecutionStatus.Failed;
        execution.ErrorMessage = ex.Message;
        // ... error handling
    }

    return execution;
}
```

### Recursive Node Execution

The key to our execution model is recursion. Each node executes, then triggers its downstream nodes:

```csharp
private async Task ExecuteNodeRecursiveAsync(
    WorkflowNode nodeConfig,
    WorkflowExecutionContext context,
    CancellationToken cancellationToken)
{
    // Get node implementation from registry
    var node = _nodeRegistry.GetNode(nodeConfig.Type);
    if (node == null)
    {
        throw new InvalidOperationException($"Node type '{nodeConfig.Type}' not registered");
    }

    // Execute the node
    var result = await node.ExecuteAsync(nodeConfig, context, cancellationToken);

    // Record execution history
    context.Execution.NodeExecutions.Add(result);

    // Store outputs for downstream nodes
    if (result.OutputData != null)
    {
        context.NodeOutputs[nodeConfig.Id] = result.OutputData;

        // Merge into shared context
        foreach (var (key, value) in result.OutputData)
        {
            context.Data[key] = value;
        }
    }

    // Handle failure with error routing
    if (result.Status == NodeExecutionStatus.Failed)
    {
        var errorConnection = context.Workflow.Connections
            .FirstOrDefault(c => c.SourceNodeId == nodeConfig.Id &&
                                 c.SourceOutput == "error");

        if (errorConnection != null)
        {
            // Route to error handler
            var errorNode = context.Workflow.Nodes
                .FirstOrDefault(n => n.Id == errorConnection.TargetNodeId);
            if (errorNode != null)
            {
                await ExecuteNodeRecursiveAsync(errorNode, context, cancellationToken);
                return;
            }
        }

        throw new Exception($"Node {nodeConfig.Id} failed: {result.ErrorMessage}");
    }

    // Find and execute downstream nodes
    var outgoingConnections = context.Workflow.Connections
        .Where(c => c.SourceNodeId == nodeConfig.Id && c.SourceOutput != "error")
        .ToList();

    foreach (var connection in outgoingConnections)
    {
        // Check connection condition
        if (!string.IsNullOrEmpty(connection.Condition))
        {
            if (!EvaluateCondition(connection.Condition, context))
            {
                continue; // Skip this connection
            }
        }

        // Execute target node
        var targetNode = context.Workflow.Nodes
            .FirstOrDefault(n => n.Id == connection.TargetNodeId);
        if (targetNode != null)
        {
            await ExecuteNodeRecursiveAsync(targetNode, context, cancellationToken);
        }
    }
}
```

**Why Recursive?**
- Simple to understand and implement
- Natural execution flow
- Easy to add parallel execution later
- Handles arbitrary graph structures

## Node Registry

The `NodeRegistry` allows dynamic registration of node types:

```csharp
public class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, Type> _nodeTypes = new();
    private readonly IServiceProvider _serviceProvider;

    public void RegisterNode<TNode>(string nodeType) where TNode : IWorkflowNode
    {
        _nodeTypes[nodeType] = typeof(TNode);
    }

    public IWorkflowNode? GetNode(string nodeType)
    {
        if (!_nodeTypes.TryGetValue(nodeType, out var type))
        {
            return null;
        }

        // Try DI first, fallback to Activator
        return _serviceProvider.GetService(type) as IWorkflowNode
               ?? Activator.CreateInstance(type) as IWorkflowNode;
    }
}
```

This design allows:
- Easy addition of custom nodes
- Dependency injection support
- Runtime node discovery

## Built-in Nodes

### HttpRequestNode

Makes HTTP API calls with full configuration:

```csharp
public class HttpRequestNode : BaseWorkflowNode
{
    private readonly IHttpClientFactory _httpClientFactory;

    public override string NodeType => "HttpRequest";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

        var url = resolvedInputs.GetValueOrDefault("url")?.ToString();
        var method = resolvedInputs.GetValueOrDefault("method")?.ToString() ?? "GET";
        var headers = resolvedInputs.GetValueOrDefault("headers") as Dictionary<string, object>;
        var body = resolvedInputs.GetValueOrDefault("body");

        var client = _httpClientFactory.CreateClient();

        // Add headers
        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    key, value?.ToString() ?? string.Empty);
            }
        }

        // Make request
        HttpResponseMessage response = method.ToUpper() switch
        {
            "GET" => await client.GetAsync(url, cancellationToken),
            "POST" => await client.PostAsJsonAsync(url, body, cancellationToken),
            "PUT" => await client.PutAsJsonAsync(url, body, cancellationToken),
            "DELETE" => await client.DeleteAsync(url, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported method: {method}")
        };

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var outputData = new Dictionary<string, object>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["body"] = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody)
                       ?? responseBody,
            ["isSuccess"] = response.IsSuccessStatusCode
        };

        return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
    }
}
```

**Example Usage:**
```json
{
  "type": "HttpRequest",
  "inputs": {
    "url": "https://api.github.com/repos/{{owner}}/{{repo}}",
    "method": "GET",
    "headers": {
      "Authorization": "Bearer {{apiToken}}",
      "Accept": "application/vnd.github+json"
    }
  },
  "outputs": {
    "repoData": "{{body}}",
    "statusCode": "{{statusCode}}"
  }
}
```

### TransformNode

Simple data transformations:

```csharp
public class TransformNode : BaseWorkflowNode
{
    public override string NodeType => "Transform";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);
        var operation = resolvedInputs.GetValueOrDefault("operation")?.ToString();
        var inputData = resolvedInputs.GetValueOrDefault("data");

        object result = operation?.ToLower() switch
        {
            "uppercase" => inputData?.ToString()?.ToUpper() ?? string.Empty,
            "lowercase" => inputData?.ToString()?.ToLower() ?? string.Empty,
            "trim" => inputData?.ToString()?.Trim() ?? string.Empty,
            "length" => inputData?.ToString()?.Length ?? 0,
            "json_parse" => JsonSerializer.Deserialize<Dictionary<string, object>>(
                inputData?.ToString() ?? "{}"),
            "json_stringify" => JsonSerializer.Serialize(inputData),
            _ => inputData ?? string.Empty
        };

        var outputData = new Dictionary<string, object>
        {
            ["result"] = result
        };

        return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
    }
}
```

### DelayNode

Adds delays to workflows:

```csharp
public class DelayNode : BaseWorkflowNode
{
    public override string NodeType => "Delay";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

        var durationMs = int.Parse(
            resolvedInputs.GetValueOrDefault("durationMs")?.ToString() ?? "0");

        await Task.Delay(durationMs, cancellationToken);

        var outputData = new Dictionary<string, object>
        {
            ["delayedMs"] = durationMs,
            ["completedAt"] = DateTime.UtcNow.ToString("O")
        };

        return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
    }
}
```

## Template System

Nodes support template variables using `{{variable}}` syntax. The `BaseWorkflowNode` provides helper methods:

```csharp
protected string ResolveTemplate(string template, WorkflowExecutionContext context)
{
    if (string.IsNullOrEmpty(template)) return template;

    var result = template;
    var matches = Regex.Matches(template, @"\{\{([^}]+)\}\}");

    foreach (Match match in matches)
    {
        var variable = match.Groups[1].Value.Trim();
        if (context.Data.TryGetValue(variable, out var value))
        {
            result = result.Replace(match.Value, value?.ToString() ?? string.Empty);
        }
    }

    return result;
}
```

This allows powerful dynamic workflows:
```json
{
  "type": "HttpRequest",
  "inputs": {
    "url": "{{apiBaseUrl}}/users/{{userId}}/posts",
    "headers": {
      "Authorization": "Bearer {{authToken}}"
    }
  }
}
```

## Database Persistence

We use Entity Framework Core with PostgreSQL for persistence:

```csharp
[Table("workflow_definitions")]
public class WorkflowDefinitionEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string WorkflowId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string DefinitionJson { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkflowExecutionEntity> Executions { get; set; } = new List<WorkflowExecutionEntity>();
}
```

**Why JSONB?**
- Flexible: Workflow definitions can evolve without migrations
- Fast: PostgreSQL's JSONB is indexed and queryable
- Simple: No need for complex relational mapping

## Example Workflow

Here's a complete workflow that fetches GitHub repo data and transforms it:

```json
{
  "id": "github-repo-workflow",
  "name": "GitHub Repository Info Fetcher",
  "startNodeId": "fetch-repo",
  "nodes": [
    {
      "id": "fetch-repo",
      "type": "HttpRequest",
      "name": "Fetch Repository",
      "inputs": {
        "url": "https://api.github.com/repos/{{owner}}/{{repo}}",
        "method": "GET",
        "headers": {
          "Accept": "application/vnd.github+json"
        }
      },
      "outputs": {
        "repoData": "{{body}}"
      },
      "position": { "x": 100, "y": 100 },
      "style": { "backgroundColor": "#10B981", "icon": "🔍" }
    },
    {
      "id": "extract-name",
      "type": "Transform",
      "name": "Extract Repo Name",
      "inputs": {
        "operation": "json_stringify",
        "data": "{{repoData}}"
      },
      "position": { "x": 100, "y": 250 },
      "style": { "backgroundColor": "#3B82F6", "icon": "🔄" }
    }
  ],
  "connections": [
    {
      "id": "conn-1",
      "sourceNodeId": "fetch-repo",
      "targetNodeId": "extract-name",
      "sourceOutput": "default",
      "label": "On Success"
    }
  ],
  "variables": {
    "owner": "scottgal",
    "repo": "mostlylucidweb"
  }
}
```

## What's Next?

We now have a fully functional workflow engine! But it's only accessible programmatically. In **Part 3**, we'll build the visual editor using HTMX, Alpine.js, TailwindCSS, and DaisyUI.

We'll create:
- A drag-and-drop canvas for nodes
- Visual connection drawing (think "dummie's Node-RED")
- Node configuration panels
- Workflow execution monitoring
- A beautiful, theme-switchable UI

## Conclusion

In this post, we've built the core of our workflow system:
- Flexible node-based architecture
- Recursive execution engine
- Template variable system
- Multiple built-in node types
- Database persistence

The beauty of this design is its simplicity. Nodes are just classes implementing an interface. Workflows are just JSON. Execution is just recursion. Yet from these simple building blocks, we can create powerful automations.

In the next post, we'll bring this to life with a stunning visual interface!

## Source Code

All the code from this post is available in the Mostlylucid repository:
- Workflow models: `Mostlylucid.Workflow.Shared/Models/`
- Workflow engine: `Mostlylucid.Workflow.Engine/`
- Database entities: `Mostlylucid.Shared/Entities/`

Stay tuned for Part 3!
