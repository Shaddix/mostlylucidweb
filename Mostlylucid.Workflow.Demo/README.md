# Workflow Demo Application

A complete ASP.NET Core workflow builder demo application with HTMX and Alpine.js, complementing the workflow system articles.

## Features

- **Visual Workflow Editor**: Drag-and-drop interface for building workflows
- **HTMX Integration**: Server-rendered partial views with client-side updates
- **Alpine.js Interactivity**: Reactive UI components without heavy JavaScript
- **In-Memory Storage**: IMemoryCache with 10-minute sliding window (demo only)
- **Hangfire Dashboard**: Monitor scheduled workflow executions
- **Node Types**: HTTP Request, Transform, Condition, SetVariable, Log

## Technology Stack

- ASP.NET Core 9.0
- HTMX 2.0
- Alpine.js 3.x
- Tailwind CSS
- Hangfire (in-memory)
- IMemoryCache

## Quick Start

### Run Locally

```bash
cd Mostlylucid.Workflow.Demo
dotnet restore
dotnet run
```

Navigate to `http://localhost:5000`

### Run with Docker

```bash
# From the repository root
docker build -t workflow-demo -f Mostlylucid.Workflow.Demo/Dockerfile .
docker run -p 8080:8080 workflow-demo
```

Navigate to `http://localhost:8080`

## Usage

### Creating a Workflow

1. Click "Create Workflow" on the main page
2. Enter a name and description
3. Click "Create" to open the editor

### Building the Workflow

1. **Add Nodes**: Click node types in the left palette
2. **Configure**: Select a node and edit properties in the right panel
3. **Connect**: Use "Next Nodes" dropdown to link nodes
4. **Position**: Drag nodes around the canvas
5. **Save**: Click "Save" to persist (resets 10-min expiration)

### Executing Workflows

1. Click "Execute" in the editor
2. Check the Hangfire dashboard at `/hangfire` for execution status

### Hangfire Dashboard

Access at `/hangfire` to see:
- Scheduled jobs
- Recurring jobs
- Succeeded/failed executions
- Job details and logs

## Architecture

### Hybrid Rendering

- **Initial Load**: ASP.NET Core renders full page
- **Interactions**: HTMX fetches partial views
- **State**: Alpine.js manages client-side state
- **Persistence**: Automatic cache refresh on client activity

### Cache Strategy

- 10-minute sliding expiration
- Auto-refresh every 5 minutes (JavaScript keep-alive)
- Client-side state allows recovery after expiration
- No database required (demo only)

### Node System

All workflow nodes inherit from `BaseWorkflowNode`:
- **HttpRequest**: Make HTTP API calls
- **Transform**: Transform data with templates
- **Condition**: Conditional branching
- **SetVariable**: Set workflow variables
- **Log**: Log messages

## Project Structure

```
Mostlylucid.Workflow.Demo/
├── Controllers/
│   ├── HomeController.cs
│   └── WorkflowController.cs
├── Services/
│   └── WorkflowCacheService.cs
├── Views/
│   ├── Shared/
│   │   └── _Layout.cshtml
│   └── Workflow/
│       ├── Index.cshtml
│       ├── Edit.cshtml
│       ├── _WorkflowList.cshtml
│       └── _CreateWorkflow.cshtml
├── Program.cs
├── Dockerfile
└── README.md
```

## Dependencies

Referenced projects:
- `Mostlylucid.Workflow.Engine` - Workflow execution engine
- `Mostlylucid.Workflow.Shared` - Shared models

## Development Notes

### Extending Node Types

1. Create new node class in `Mostlylucid.Workflow.Engine/Nodes/`
2. Inherit from `BaseWorkflowNode`
3. Implement `ExecuteAsync()` method
4. Add to node palette in `Edit.cshtml`

### Customizing UI

All styling uses Tailwind CSS utility classes. The layout is responsive and works on mobile/tablet/desktop.

## Limitations (Demo)

- In-memory storage only (no persistence)
- No authentication/authorization
- Single-instance only (no distributed cache)
- 10-minute cache expiration

For production use, consider:
- Database storage (Entity Framework)
- Distributed cache (Redis)
- Authentication middleware
- Persistent Hangfire storage (SQL/Postgres)

## Related Articles

This demo complements the workflow system article series:
1. Introduction to Workflow Systems
2. Workflow Architecture
3. Visual Workflow Editor
4. Hangfire Integration

## License

MIT
