# Building a Workflow System with HTMX and ASP.NET Core - Part 4: Hangfire Integration and Automation
<!--category-- ASP.NET, Hangfire, Workflow, Background Jobs -->
<datetime class="hidden">2025-01-15T18:00</datetime>

## Introduction

In [Part 3](/blog/workflowsystem-part3-visual-editor), we built a beautiful visual editor. But our workflows only run when we manually trigger them. In this final post, we'll make workflows truly autonomous using Hangfire for:

- **Scheduled execution** - Run workflows on a schedule
- **API polling** - Monitor external APIs and trigger on changes
- **State management** - Track trigger states across executions
- **Dashboard** - Monitor all background jobs

[TOC]

## Why Hangfire?

Hangfire is perfect for our needs because it:
- Stores jobs in our existing PostgreSQL database
- Provides a built-in dashboard
- Supports recurring jobs
- Has automatic retry logic
- Scales horizontally

## The Trigger State Model

First, let's understand our trigger state entity (we already created this in Part 2):

```csharp
[Table("workflow_trigger_states")]
public class WorkflowTriggerStateEntity
{
    public int Id { get; set; }
    public int WorkflowDefinitionId { get; set; }

    // Type: "Schedule", "ApiPoll", "Webhook"
    public string TriggerType { get; set; } = string.Empty;

    // Configuration as JSON
    public string ConfigJson { get; set; } = "{}";

    // Current state as JSON (stores last poll time, content hash, etc.)
    public string StateJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? LastFiredAt { get; set; }
    public int FireCount { get; set; } = 0;
    public string? LastError { get; set; }
}
```

This entity tracks everything about a workflow trigger:
- When it last ran
- What its configuration is
- What state it's in (for stateful triggers)
- Any errors that occurred

## Scheduled Workflows

### Configuration Model

```csharp
public class ScheduleTriggerConfig
{
    public string IntervalType { get; set; } = "minutes"; // minutes, hours, days
    public int IntervalValue { get; set; } = 60;
    public Dictionary<string, object>? InputData { get; set; }
}
```

### The Scheduler Job

```csharp
public class WorkflowSchedulerJob
{
    private readonly MostlylucidDbContext _context;
    private readonly WorkflowExecutionService _executionService;
    private readonly ILogger<WorkflowSchedulerJob> _logger;

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledWorkflowsAsync()
    {
        _logger.LogInformation("Checking for scheduled workflows");

        // Get all enabled schedule triggers
        var triggers = await _context.WorkflowTriggerStates
            .Include(t => t.WorkflowDefinition)
            .Where(t => t.IsEnabled && t.TriggerType == "Schedule")
            .ToListAsync();

        foreach (var trigger in triggers)
        {
            try
            {
                var config = JsonSerializer.Deserialize<ScheduleTriggerConfig>(
                    trigger.ConfigJson);

                if (config == null) continue;

                // Check if it's time to run
                if (!ShouldRunScheduledWorkflow(trigger, config))
                    continue;

                _logger.LogInformation(
                    "Executing scheduled workflow {WorkflowId}",
                    trigger.WorkflowDefinition.WorkflowId);

                // Execute the workflow
                await _executionService.ExecuteWorkflowAsync(
                    trigger.WorkflowDefinition.WorkflowId,
                    config.InputData,
                    "Scheduler");

                // Update trigger state
                trigger.LastCheckedAt = DateTime.UtcNow;
                trigger.LastFiredAt = DateTime.UtcNow;
                trigger.FireCount++;

                var state = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                trigger.StateJson) ?? new();
                state["lastRun"] = DateTime.UtcNow.ToString("O");
                trigger.StateJson = JsonSerializer.Serialize(state);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error executing scheduled workflow {TriggerId}",
                    trigger.Id);
                trigger.LastError = ex.Message;
                await _context.SaveChangesAsync();
            }
        }
    }

    private bool ShouldRunScheduledWorkflow(
        WorkflowTriggerStateEntity trigger,
        ScheduleTriggerConfig config)
    {
        // First run?
        if (!trigger.LastFiredAt.HasValue)
            return true;

        var timeSinceLastRun = DateTime.UtcNow - trigger.LastFiredAt.Value;

        return config.IntervalType.ToLower() switch
        {
            "minutes" => timeSinceLastRun.TotalMinutes >= config.IntervalValue,
            "hours" => timeSinceLastRun.TotalHours >= config.IntervalValue,
            "days" => timeSinceLastRun.TotalDays >= config.IntervalValue,
            _ => false
        };
    }
}
```

**How It Works:**
1. Every minute, Hangfire calls `ExecuteScheduledWorkflowsAsync()`
2. We query for enabled schedule triggers
3. For each trigger, check if enough time has passed
4. If yes, execute the workflow
5. Update the trigger state with last run time

## API Polling

API polling is more interesting - we monitor external APIs and trigger workflows when content changes!

### Configuration Model

```csharp
public class ApiPollTriggerConfig
{
    public string Url { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 300; // 5 minutes
    public bool AlwaysTrigger { get; set; } = false;
    public Dictionary<string, string>? Headers { get; set; }
}
```

### The Polling Job

```csharp
[AutomaticRetry(Attempts = 3)]
public async Task PollApiTriggersAsync()
{
    _logger.LogInformation("Polling API triggers");

    var triggers = await _context.WorkflowTriggerStates
        .Include(t => t.WorkflowDefinition)
        .Where(t => t.IsEnabled && t.TriggerType == "ApiPoll")
        .ToListAsync();

    foreach (var trigger in triggers)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ApiPollTriggerConfig>(
                trigger.ConfigJson);

            if (config == null) continue;

            // Check if it's time to poll
            if (trigger.LastCheckedAt.HasValue)
            {
                var timeSinceLastCheck = DateTime.UtcNow - trigger.LastCheckedAt.Value;
                if (timeSinceLastCheck.TotalSeconds < config.IntervalSeconds)
                    continue;
            }

            _logger.LogInformation("Polling API for workflow {WorkflowId}",
                trigger.WorkflowDefinition.WorkflowId);

            // Poll the API
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(config.Url);
            var content = await response.Content.ReadAsStringAsync();

            // Get previous state
            var state = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            trigger.StateJson) ?? new();

            var previousHash = state.GetValueOrDefault("contentHash")?.ToString();
            var currentHash = ComputeHash(content);

            // Has content changed?
            if (previousHash != currentHash || config.AlwaysTrigger)
            {
                _logger.LogInformation(
                    "API content changed, triggering workflow {WorkflowId}",
                    trigger.WorkflowDefinition.WorkflowId);

                // Pass response as input to workflow
                var inputData = new Dictionary<string, object>
                {
                    ["apiResponse"] = content,
                    ["statusCode"] = (int)response.StatusCode,
                    ["previousHash"] = previousHash ?? string.Empty,
                    ["currentHash"] = currentHash
                };

                // Execute the workflow
                await _executionService.ExecuteWorkflowAsync(
                    trigger.WorkflowDefinition.WorkflowId,
                    inputData,
                    $"ApiPoll:{config.Url}");

                trigger.LastFiredAt = DateTime.UtcNow;
                trigger.FireCount++;

                // Update state
                state["contentHash"] = currentHash;
                state["lastContent"] = content.Length > 1000
                    ? content.Substring(0, 1000)
                    : content;
                state["lastPoll"] = DateTime.UtcNow.ToString("O");
            }

            trigger.LastCheckedAt = DateTime.UtcNow;
            trigger.StateJson = JsonSerializer.Serialize(state);
            trigger.LastError = null;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling API trigger {TriggerId}",
                trigger.Id);
            trigger.LastError = ex.Message;
            trigger.LastCheckedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

private string ComputeHash(string content)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}
```

**How It Works:**
1. Every minute, check all API poll triggers
2. For each trigger, check if enough time has passed since last poll
3. Poll the configured URL
4. Compute a hash of the response content
5. Compare with previous hash stored in state
6. If changed (or `AlwaysTrigger` is true), execute the workflow
7. Pass the API response as input data to the workflow
8. Update the state with new hash

### Use Case Example

**Monitor GitHub Releases:**

```json
{
  "triggerType": "ApiPoll",
  "config": {
    "url": "https://api.github.com/repos/dotnet/aspnetcore/releases/latest",
    "intervalSeconds": 3600,
    "alwaysTrigger": false
  }
}
```

This polls the GitHub API every hour. When a new release is published, the content hash changes, and the workflow executes with the release data!

## Registering Hangfire Jobs

In your `Program.cs` or startup configuration:

```csharp
// Add Hangfire services
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHangfireServer();

// Register our job
builder.Services.AddScoped<WorkflowSchedulerJob>();
```

Then, after the app starts, register recurring jobs:

```csharp
app.UseHangfireDashboard("/hangfire");

// Register recurring jobs
RecurringJob.AddOrUpdate<WorkflowSchedulerJob>(
    "scheduled-workflows",
    job => job.ExecuteScheduledWorkflowsAsync(),
    Cron.Minutely);

RecurringJob.AddOrUpdate<WorkflowSchedulerJob>(
    "api-poll-triggers",
    job => job.PollApiTriggersAsync(),
    Cron.Minutely);
```

## The Hangfire Dashboard

Hangfire includes a built-in dashboard accessible at `/hangfire`:

- **Jobs**: See all queued, processing, and completed jobs
- **Recurring Jobs**: Manage our workflow schedulers
- **Retries**: View and retry failed jobs
- **Servers**: Monitor Hangfire servers

### Securing the Dashboard

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireAuthorizationFilter()
    }
});

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Only allow authenticated users
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
```

## Managing Triggers via UI

Let's add UI for creating and managing triggers:

```csharp
[HttpPost("workflow/{id}/triggers")]
public async Task<IActionResult> CreateTrigger(
    string id,
    [FromBody] TriggerCreateRequest request)
{
    var workflow = await _context.WorkflowDefinitions
        .FirstOrDefaultAsync(w => w.WorkflowId == id);

    if (workflow == null)
        return NotFound();

    var trigger = new WorkflowTriggerStateEntity
    {
        WorkflowDefinitionId = workflow.Id,
        TriggerType = request.Type,
        ConfigJson = JsonSerializer.Serialize(request.Config),
        StateJson = "{}",
        IsEnabled = true
    };

    await _context.WorkflowTriggerStates.AddAsync(trigger);
    await _context.SaveChangesAsync();

    return Json(new { success = true, triggerId = trigger.Id });
}

public class TriggerCreateRequest
{
    public string Type { get; set; } = string.Empty; // Schedule, ApiPoll
    public object Config { get; set; } = new();
}
```

### UI Component

```html
<div class="card bg-base-100 shadow-xl">
    <div class="card-body">
        <h2 class="card-title">⏰ Add Trigger</h2>

        <div class="form-control">
            <label class="label">Trigger Type</label>
            <select class="select select-bordered" x-model="triggerType">
                <option value="Schedule">Schedule</option>
                <option value="ApiPoll">API Poll</option>
            </select>
        </div>

        <!-- Schedule Config -->
        <template x-if="triggerType === 'Schedule'">
            <div class="space-y-4">
                <div class="form-control">
                    <label class="label">Interval</label>
                    <div class="flex gap-2">
                        <input type="number"
                               x-model="scheduleConfig.intervalValue"
                               class="input input-bordered flex-1" />
                        <select x-model="scheduleConfig.intervalType"
                                class="select select-bordered">
                            <option value="minutes">Minutes</option>
                            <option value="hours">Hours</option>
                            <option value="days">Days</option>
                        </select>
                    </div>
                </div>
            </div>
        </template>

        <!-- API Poll Config -->
        <template x-if="triggerType === 'ApiPoll'">
            <div class="space-y-4">
                <div class="form-control">
                    <label class="label">API URL</label>
                    <input type="url"
                           x-model="apiConfig.url"
                           class="input input-bordered"
                           placeholder="https://api.example.com/data" />
                </div>

                <div class="form-control">
                    <label class="label">Poll Interval (seconds)</label>
                    <input type="number"
                           x-model="apiConfig.intervalSeconds"
                           class="input input-bordered"
                           value="300" />
                </div>
            </div>
        </template>

        <button @click="createTrigger()" class="btn btn-primary mt-4">
            Create Trigger
        </button>
    </div>
</div>
```

## Real-World Example Workflow

Let's build a complete automated workflow that:
1. Polls the GitHub API for new releases
2. Checks if the version is newer than what we've seen
3. Logs a message
4. (Could send an email, post to Slack, etc.)

### Step 1: Create the Workflow

```json
{
  "name": "GitHub Release Monitor",
  "startNodeId": "parse-data",
  "nodes": [
    {
      "id": "parse-data",
      "type": "Transform",
      "name": "Extract Version",
      "inputs": {
        "operation": "json_parse",
        "data": "{{apiResponse}}"
      }
    },
    {
      "id": "log-release",
      "type": "Log",
      "name": "Log New Release",
      "inputs": {
        "message": "New release: {{tag_name}} - {{name}}",
        "level": "info"
      }
    }
  ],
  "connections": [
    {
      "sourceNodeId": "parse-data",
      "targetNodeId": "log-release"
    }
  ]
}
```

### Step 2: Create the API Poll Trigger

```json
{
  "type": "ApiPoll",
  "config": {
    "url": "https://api.github.com/repos/dotnet/aspnetcore/releases/latest",
    "intervalSeconds": 3600
  }
}
```

Now, every hour, Hangfire will:
1. Poll the GitHub API
2. Compare the content hash with previous poll
3. If changed, execute the workflow
4. The workflow parses the JSON and logs the release info

## Monitoring and Observability

### Logging

All workflow executions are logged:

```csharp
_logger.LogInformation(
    "Workflow {WorkflowId} execution {ExecutionId} completed in {Duration}ms with status {Status}",
    execution.WorkflowId,
    execution.Id,
    execution.DurationMs,
    execution.Status);
```

### Metrics

We can add Prometheus metrics:

```csharp
private static readonly Counter WorkflowExecutions = Metrics
    .CreateCounter("workflow_executions_total",
        "Total workflow executions",
        new CounterConfiguration
        {
            LabelNames = new[] { "workflow_id", "status" }
        });

// In execution service
WorkflowExecutions
    .WithLabels(workflow.Id, execution.Status.ToString())
    .Inc();
```

### Alerts

Set up alerts for:
- Failed workflows (Status == Failed)
- Workflows taking too long
- API polling failures
- Triggers that haven't fired in expected timeframe

## Performance Considerations

### Database Load

With many workflows polling frequently, database load can be significant:

**Solution: Batch queries**
```csharp
// Instead of querying per trigger
var triggers = await _context.WorkflowTriggerStates
    .Include(t => t.WorkflowDefinition)
    .Where(t => t.IsEnabled && t.TriggerType == "ApiPoll")
    .AsNoTracking() // Read-only
    .ToListAsync();
```

### API Rate Limiting

When polling external APIs:

**Solution: Exponential backoff**
```csharp
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
{
    // Back off
    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5);
    state["backoffUntil"] = DateTime.UtcNow.Add(retryAfter).ToString("O");
}
```

## Advanced Features

### Conditional Triggers

Only trigger if certain conditions are met:

```csharp
public class ConditionalTriggerConfig : ApiPollTriggerConfig
{
    public string? Condition { get; set; } // e.g., "{{stars}} > 1000"
}
```

### Trigger Dependencies

Chain triggers - one workflow's completion triggers another:

```csharp
// After workflow completes
if (execution.Status == WorkflowExecutionStatus.Completed)
{
    var dependentTriggers = await _context.WorkflowTriggerStates
        .Where(t => t.TriggerType == "WorkflowComplete" &&
                    t.ConfigJson.Contains(execution.WorkflowId))
        .ToListAsync();

    foreach (var trigger in dependentTriggers)
    {
        await _executionService.ExecuteWorkflowAsync(
            trigger.WorkflowDefinition.WorkflowId,
            execution.OutputData,
            $"Triggered by {execution.WorkflowId}");
    }
}
```

## Testing Hangfire Jobs

Unit test your jobs:

```csharp
[Fact]
public async Task ExecuteScheduledWorkflows_ShouldExecuteWhenIntervalPassed()
{
    // Arrange
    var mockContext = CreateMockContext();
    var mockExecutionService = new Mock<IWorkflowExecutionService>();
    var job = new WorkflowSchedulerJob(mockContext.Object,
        mockExecutionService.Object, Mock.Of<ILogger>());

    // Act
    await job.ExecuteScheduledWorkflowsAsync();

    // Assert
    mockExecutionService.Verify(s => s.ExecuteWorkflowAsync(
        It.IsAny<string>(),
        It.IsAny<Dictionary<string, object>>(),
        "Scheduler",
        It.IsAny<CancellationToken>()), Times.Once);
}
```

## Conclusion

We've built a complete automation system! Our workflows can now:

✅ **Run on schedules** - Hourly, daily, or custom intervals
✅ **Poll APIs** - Monitor external services for changes
✅ **Track state** - Remember what we've seen before
✅ **Auto-retry** - Handle transient failures
✅ **Monitor** - Dashboard for all jobs
✅ **Scale** - Hangfire handles load balancing

## The Complete Series

We've built an enterprise-grade workflow system from scratch:

- **Part 1**: Introduction and architecture
- **Part 2**: Core workflow engine
- **Part 3**: Visual workflow editor
- **Part 4**: Hangfire integration (this post)

You now have:
- A powerful workflow engine
- A beautiful visual editor
- Automated execution
- API monitoring
- Full observability

## What's Next?

Possible enhancements:
- **Webhooks**: Trigger workflows via HTTP endpoints
- **Email nodes**: Send emails from workflows
- **Database nodes**: Query databases
- **AI nodes**: Integrate with LLMs
- **Sub-workflows**: Compose workflows together
- **Workflow marketplace**: Share workflow templates

## Source Code

All code is available in the repository:
- Hangfire jobs: `Mostlylucid.SchedulerService/Jobs/`
- Workflow models: `Mostlylucid.Workflow.Shared/`
- Workflow engine: `Mostlylucid.Workflow.Engine/`

Thank you for following this series! Happy workflow building! 🎉
