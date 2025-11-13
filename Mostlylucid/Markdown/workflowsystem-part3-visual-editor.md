# Building a Workflow System with HTMX and ASP.NET Core - Part 3: Building the Visual Editor
<!--category-- ASP.NET, HTMX, Alpine.js, Workflow, TailwindCSS -->
<datetime class="hidden">2025-01-15T16:00</datetime>

## Introduction

In [Part 2](/blog/workflowsystem-part2-architecture), we built a powerful workflow engine. But workflows defined in JSON aren't very user-friendly. In this post, we'll create a stunning visual workflow editor - think "dummy's Node-RED" - using HTMX, Alpine.js, TailwindCSS, and DaisyUI.

By the end of this post, you'll have:
- A drag-and-drop workflow canvas
- Visual node connections with SVG
- Real-time node configuration
- Beautiful, theme-switchable UI
- Full HTMX integration

[TOC]

## The Vision

We want users to:
1. Drag nodes from a palette onto a canvas
2. Connect nodes visually
3. Configure node properties in a side panel
4. Save and execute workflows

All without writing a single line of JSON!

## Technology Stack

### Why This Stack?

**HTMX 2.0**: Server-driven interactions without writing JavaScript
- Perfect for save/load operations
- Reduces client-side complexity
- Server maintains the source of truth

**Alpine.js**: Lightweight reactivity for the canvas
- Only ~15KB minified
- Perfect for interactive UI components
- Simple `x-data` directives

**TailwindCSS + DaisyUI**: Beautiful, themeable UI
- Utility-first CSS
- Dark/light mode out of the box
- Pre-built components (cards, buttons, badges)

**SVG**: Native connection rendering
- Scalable, crisp lines
- CSS-styleable
- No external libraries needed

## Architecture Overview

Our editor has three main panels:

```
┌─────────────────────────────────────────────────────┐
│  Top Toolbar (Name, Save, Run, Cancel)             │
├─────────┬───────────────────────────┬───────────────┤
│  Node   │      Canvas               │    Node       │
│ Palette │    (Drag & Drop)          │  Inspector    │
│         │                            │               │
│ [HTTP]  │   ┌─────┐                 │  Name: ___    │
│ [Xform] │   │Node1│─┐               │  Type: ___    │
│ [Delay] │   └─────┘ │               │  Config: ___  │
│ [Cond]  │           ▼               │  Style: ___   │
│         │      ┌─────┐              │               │
│         │      │Node2│              │               │
│         │      └─────┘              │               │
└─────────┴───────────────────────────┴───────────────┘
```

## The Controller

First, let's set up our controller endpoints:

```csharp
[Route("workflow")]
public class WorkflowController : BaseController
{
    private readonly WorkflowService _workflowService;
    private readonly WorkflowExecutionService _executionService;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var workflows = await _workflowService.GetAllAsync();
        return View("Index", workflows);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        var workflow = new WorkflowDefinition
        {
            Name = "New Workflow",
            Nodes = new List<WorkflowNode>(),
            Connections = new List<NodeConnection>()
        };
        return View("Editor", workflow);
    }

    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);
        return workflow == null ? NotFound() : View("Editor", workflow);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] WorkflowDefinition workflow)
    {
        var saved = await _workflowService.CreateOrUpdateAsync(workflow);
        return Json(new { success = true, workflowId = saved.Id });
    }

    [HttpPost("execute/{id}")]
    public async Task<IActionResult> Execute(string id,
        [FromBody] Dictionary<string, object>? inputData = null)
    {
        var execution = await _executionService.ExecuteWorkflowAsync(
            id, inputData, User.Identity?.Name ?? "Anonymous");

        return Json(new
        {
            success = true,
            executionId = execution.Id,
            status = execution.Status.ToString()
        });
    }
}
```

## The Editor View

Our editor view uses Alpine.js for state management:

```html
@model Mostlylucid.Workflow.Shared.Models.WorkflowDefinition

<div class="container-fluid"
     x-data="workflowEditor(@Html.Raw(JsonSerializer.Serialize(Model)))"
     x-init="init()">

    <!-- Top Toolbar -->
    <div class="navbar bg-base-200 rounded-box shadow-lg mb-6">
        <div class="flex-1">
            <input type="text"
                   x-model="workflow.name"
                   placeholder="Workflow Name"
                   class="input input-ghost text-2xl font-bold w-96" />
        </div>
        <div class="flex-none gap-2">
            <button @click="saveWorkflow()" class="btn btn-success">
                Save
            </button>
            <button @click="runWorkflow()" class="btn btn-primary">
                Run
            </button>
        </div>
    </div>

    <!-- Three-panel layout -->
    <div class="flex gap-4 h-[calc(100vh-200px)]">
        <!-- Left: Node Palette -->
        <!-- Center: Canvas -->
        <!-- Right: Node Inspector -->
    </div>
</div>
```

## Alpine.js State Management

The `workflowEditor` Alpine component manages all editor state:

```javascript
function workflowEditor(initialWorkflow) {
    return {
        workflow: initialWorkflow || {
            id: crypto.randomUUID(),
            name: 'New Workflow',
            nodes: [],
            connections: [],
            isEnabled: true
        },
        selectedNode: null,
        draggedNodeType: null,
        connecting: false,

        init() {
            // Initialize node inputs as JSON for editing
            this.workflow.nodes.forEach(node => {
                node.inputsJson = JSON.stringify(node.inputs || {}, null, 2);
            });
        },

        // ... methods below
    };
}
```

## Drag and Drop: The Node Palette

The left sidebar contains draggable node types:

```html
<div class="w-64 bg-base-200 rounded-box p-4 overflow-y-auto">
    <h3 class="text-lg font-bold mb-4">📦 Available Nodes</h3>

    <div class="space-y-3">
        <div class="card bg-base-100 shadow cursor-pointer"
             draggable="true"
             @dragstart="dragStart($event, 'HttpRequest', '🌐', '#10B981')">
            <div class="card-body p-4">
                <h4 class="font-semibold">🌐 HTTP Request</h4>
                <p class="text-xs">Make API calls</p>
            </div>
        </div>

        <div class="card bg-base-100 shadow cursor-pointer"
             draggable="true"
             @dragstart="dragStart($event, 'Transform', '🔄', '#3B82F6')">
            <div class="card-body p-4">
                <h4 class="font-semibold">🔄 Transform</h4>
                <p class="text-xs">Process data</p>
            </div>
        </div>

        <!-- More node types... -->
    </div>
</div>
```

### Drag Start Handler

```javascript
dragStart(event, nodeType, icon, color) {
    this.draggedNodeType = { type: nodeType, icon, color };
}
```

## The Canvas

The canvas is where the magic happens:

```html
<div class="flex-1 bg-base-300 rounded-box relative overflow-hidden"
     @drop="dropNode($event)"
     @dragover.prevent
     id="workflow-canvas">

    <div class="absolute inset-0 overflow-auto p-8">
        <!-- Grid background -->
        <div class="absolute inset-0 opacity-20"
             style="background-image: repeating-linear-gradient(...);
                    background-size: 20px 20px;">
        </div>

        <!-- Render nodes -->
        <template x-for="node in workflow.nodes" :key="node.id">
            <div :id="'node-' + node.id"
                 class="absolute card shadow-2xl cursor-move"
                 :style="`left: ${node.position.x}px;
                          top: ${node.position.y}px;
                          width: ${node.style.width}px;
                          background-color: ${node.style.backgroundColor};`"
                 @mousedown="selectNode(node)"
                 draggable="true"
                 @dragstart="dragNode($event, node)">

                <div class="card-body p-4">
                    <div class="flex items-center gap-2">
                        <span x-text="node.style.icon || '📦'"></span>
                        <h3 x-text="node.name"></h3>
                    </div>

                    <!-- Connection points -->
                    <div class="flex justify-between mt-2">
                        <div class="badge badge-primary"
                             @click.stop="startConnection(node, 'input')">
                            ◀
                        </div>
                        <div class="badge badge-success"
                             @click.stop="startConnection(node, 'output')">
                            ▶
                        </div>
                    </div>
                </div>
            </div>
        </template>

        <!-- SVG connections layer -->
        <svg class="absolute inset-0 pointer-events-none"
             style="width: 100%; height: 100%;">
            <template x-for="conn in workflow.connections" :key="conn.id">
                <path :d="getConnectionPath(conn)"
                      stroke="#94A3B8"
                      stroke-width="3"
                      fill="none"
                      marker-end="url(#arrowhead)"/>
            </template>
            <defs>
                <marker id="arrowhead" markerWidth="10" markerHeight="10">
                    <polygon points="0 0, 10 3, 0 6" fill="#94A3B8" />
                </marker>
            </defs>
        </svg>
    </div>
</div>
```

### Drop Handler

When a node is dropped on the canvas:

```javascript
dropNode(event) {
    if (!this.draggedNodeType) return;

    const canvas = document.getElementById('canvas-container');
    const rect = canvas.getBoundingClientRect();

    // Calculate drop position
    const x = event.clientX - rect.left + canvas.scrollLeft - 100;
    const y = event.clientY - rect.top + canvas.scrollTop - 50;

    // Create new node
    const newNode = {
        id: crypto.randomUUID(),
        type: this.draggedNodeType.type,
        name: this.draggedNodeType.type,
        inputs: {},
        outputs: {},
        position: { x, y },
        style: {
            backgroundColor: this.draggedNodeType.color,
            textColor: '#FFFFFF',
            borderColor: this.draggedNodeType.color,
            icon: this.draggedNodeType.icon,
            width: 200
        },
        inputsJson: '{}'
    };

    this.workflow.nodes.push(newNode);

    // Set as start node if first node
    if (this.workflow.nodes.length === 1) {
        this.workflow.startNodeId = newNode.id;
    }

    this.draggedNodeType = null;
}
```

## SVG Connection Rendering

Connections between nodes are drawn with SVG paths:

```javascript
getConnectionPath(conn) {
    const source = this.workflow.nodes.find(n => n.id === conn.sourceNodeId);
    const target = this.workflow.nodes.find(n => n.id === conn.targetNodeId);

    if (!source || !target) return '';

    // Calculate connection points
    const x1 = source.position.x + (source.style?.width || 200);
    const y1 = source.position.y + 50;
    const x2 = target.position.x;
    const y2 = target.position.y + 50;

    // Create curved Bezier path
    const midX = (x1 + x2) / 2;
    return `M ${x1} ${y1} C ${midX} ${y1}, ${midX} ${y2}, ${x2} ${y2}`;
}
```

This creates smooth, curved connections between nodes!

## The Node Inspector

The right panel lets users configure selected nodes:

```html
<div class="w-80 bg-base-200 rounded-box p-4 overflow-y-auto"
     x-show="selectedNode">
    <h3 class="text-lg font-bold mb-4">⚙️ Node Settings</h3>

    <template x-if="selectedNode">
        <div class="space-y-4">
            <div class="form-control">
                <label class="label">Node Name</label>
                <input type="text"
                       x-model="selectedNode.name"
                       class="input input-bordered" />
            </div>

            <div class="form-control">
                <label class="label">Background Color</label>
                <input type="color"
                       x-model="selectedNode.style.backgroundColor"
                       class="input w-full h-12" />
            </div>

            <div class="form-control">
                <label class="label">Icon (emoji)</label>
                <input type="text"
                       x-model="selectedNode.style.icon"
                       class="input input-bordered"
                       maxlength="2" />
            </div>

            <div class="form-control">
                <label class="label">Inputs (JSON)</label>
                <textarea x-model="selectedNode.inputsJson"
                          @input="updateNodeInputs()"
                          class="textarea textarea-bordered font-mono"
                          rows="6"></textarea>
            </div>

            <button @click="setStartNode(selectedNode)"
                    class="btn btn-sm"
                    :class="workflow.startNodeId === selectedNode.id ?
                            'btn-success' : 'btn-outline'">
                Set as Start Node
            </button>
        </div>
    </template>
</div>
```

### Live JSON Parsing

As users edit the JSON inputs, we parse them in real-time:

```javascript
updateNodeInputs() {
    if (!this.selectedNode) return;

    try {
        this.selectedNode.inputs = JSON.parse(this.selectedNode.inputsJson);
    } catch (e) {
        console.error('Invalid JSON:', e);
        // Could show error badge
    }
}
```

## Connecting Nodes

Users connect nodes by clicking connection points:

```javascript
startConnection(node, type) {
    if (this.connecting) {
        // Complete connection
        if (this.connectionStart.node.id !== node.id) {
            this.workflow.connections.push({
                id: crypto.randomUUID(),
                sourceNodeId: this.connectionStart.node.id,
                targetNodeId: node.id,
                sourceOutput: 'default',
                targetInput: 'default'
            });
        }
        this.connecting = false;
        this.connectionStart = null;
    } else {
        // Start connection
        this.connecting = true;
        this.connectionStart = { node, type };
    }
}
```

## Saving Workflows with HTMX

When the user clicks "Save", we use a simple fetch call (could be HTMX too):

```javascript
async saveWorkflow() {
    try {
        const response = await fetch('/workflow/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(this.workflow)
        });

        const result = await response.json();
        if (result.success) {
            alert('Workflow saved!');
            window.location.href = '/workflow';
        } else {
            alert('Error: ' + result.message);
        }
    } catch (error) {
        alert('Error saving: ' + error.message);
    }
}
```

## Running Workflows

Execute workflows right from the editor:

```javascript
async runWorkflow() {
    try {
        // Save first
        await this.saveWorkflow();

        // Then execute
        const response = await fetch(`/workflow/execute/${this.workflow.id}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
        });

        const result = await response.json();
        if (result.success) {
            alert(`Workflow started! Execution ID: ${result.executionId}`);
        }
    } catch (error) {
        alert('Error: ' + error.message);
    }
}
```

## Theme Switching with DaisyUI

DaisyUI provides automatic theme switching. Users can toggle between light and dark modes, and all our workflow nodes adapt automatically!

```html
<!-- In your layout -->
<select data-choose-theme class="select select-bordered">
    <option value="light">Light</option>
    <option value="dark">Dark</option>
    <option value="cupcake">Cupcake</option>
    <option value="synthwave">Synthwave</option>
</select>
```

All our colors use DaisyUI's semantic classes (`bg-base-200`, `text-base-content`), so they automatically adapt to the chosen theme!

## Responsive Design

Our three-panel layout adapts to different screen sizes:

```html
<div class="flex flex-col lg:flex-row gap-4">
    <!-- On mobile: stack vertically -->
    <!-- On desktop: side-by-side -->
</div>
```

## Performance Optimizations

### Virtual Scrolling

For workflows with hundreds of nodes, we could implement virtual scrolling (only render visible nodes). But for most use cases, rendering 50-100 nodes is perfectly fine with modern browsers.

### Debounced Updates

When dragging nodes, we could debounce position updates:

```javascript
let dragTimeout;
dragNode(event, node) {
    clearTimeout(dragTimeout);
    dragTimeout = setTimeout(() => {
        // Update position
    }, 16); // ~60fps
}
```

## Accessibility

We've added proper ARIA labels and keyboard navigation:

```html
<button @click="deleteNode(node)"
        class="btn"
        aria-label="Delete node"
        @keydown.delete="deleteNode(node)">
    🗑️
</button>
```

## What We've Built

In this post, we created:

✅ **Drag-and-drop node palette** - Draggable node types
✅ **Visual canvas** - Grid background with positioned nodes
✅ **SVG connections** - Curved paths between nodes
✅ **Node inspector** - Real-time configuration panel
✅ **Theme support** - Works with all DaisyUI themes
✅ **Responsive layout** - Adapts to screen size
✅ **HTMX integration** - Server-driven save/load
✅ **Alpine.js reactivity** - Smooth, reactive UI

## Example Workflow

Here's a complete workflow you can build in the editor:

```json
{
  "name": "GitHub Stars Monitor",
  "nodes": [
    {
      "id": "1",
      "type": "HttpRequest",
      "name": "Fetch Repo Data",
      "inputs": {
        "url": "https://api.github.com/repos/{{owner}}/{{repo}}",
        "method": "GET"
      },
      "position": { "x": 100, "y": 100 },
      "style": { "backgroundColor": "#10B981", "icon": "🌐" }
    },
    {
      "id": "2",
      "type": "Condition",
      "name": "Check Stars",
      "inputs": {
        "condition": "{{stargazers_count}} > 100"
      },
      "position": { "x": 100, "y": 250 },
      "style": { "backgroundColor": "#8B5CF6", "icon": "🔀" }
    },
    {
      "id": "3",
      "type": "Log",
      "name": "Log Success",
      "inputs": {
        "message": "Repo has {{stargazers_count}} stars!",
        "level": "info"
      },
      "position": { "x": 100, "y": 400 },
      "style": { "backgroundColor": "#3B82F6", "icon": "📝" }
    }
  ],
  "connections": [
    { "sourceNodeId": "1", "targetNodeId": "2" },
    { "sourceNodeId": "2", "targetNodeId": "3" }
  ],
  "startNodeId": "1"
}
```

## What's Next?

In **Part 4**, we'll integrate Hangfire for:
- Scheduled workflow execution
- API polling triggers
- Trigger state management
- Background job processing
- Monitoring dashboard

We'll make our workflows truly autonomous!

## Conclusion

We've built a production-ready visual workflow editor with minimal JavaScript! By combining HTMX for server communication, Alpine.js for interactivity, and TailwindCSS/DaisyUI for styling, we've created something that feels modern and is maintainable.

The key insights:
- **SVG** is perfect for connection rendering
- **Alpine.js** provides just enough reactivity
- **DaisyUI** makes theming trivial
- **Drag-and-drop API** is surprisingly simple

Stay tuned for Part 4 where we make workflows run on autopilot!
