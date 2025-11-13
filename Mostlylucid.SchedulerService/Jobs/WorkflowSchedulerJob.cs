using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Workflow;
using System.Text.Json;

namespace Mostlylucid.SchedulerService.Jobs;

/// <summary>
/// Hangfire job that executes scheduled workflows
/// </summary>
public class WorkflowSchedulerJob
{
    private readonly MostlylucidDbContext _context;
    private readonly WorkflowExecutionService _executionService;
    private readonly ILogger<WorkflowSchedulerJob> _logger;

    public WorkflowSchedulerJob(
        MostlylucidDbContext context,
        WorkflowExecutionService executionService,
        ILogger<WorkflowSchedulerJob> logger)
    {
        _context = context;
        _executionService = executionService;
        _logger = logger;
    }

    /// <summary>
    /// Check and execute scheduled workflows
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledWorkflowsAsync()
    {
        _logger.LogInformation("Checking for scheduled workflows");

        try
        {
            // Get all enabled trigger states with schedule type
            var triggers = await _context.WorkflowTriggerStates
                .Include(t => t.WorkflowDefinition)
                .Where(t => t.IsEnabled && t.TriggerType == "Schedule")
                .ToListAsync();

            foreach (var trigger in triggers)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<ScheduleTriggerConfig>(trigger.ConfigJson);
                    if (config == null) continue;

                    // Check if it's time to run
                    var shouldRun = ShouldRunScheduledWorkflow(trigger, config);
                    if (!shouldRun) continue;

                    _logger.LogInformation("Executing scheduled workflow {WorkflowId}",
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

                    var state = JsonSerializer.Deserialize<Dictionary<string, object>>(trigger.StateJson)
                                ?? new Dictionary<string, object>();
                    state["lastRun"] = DateTime.UtcNow.ToString("O");
                    trigger.StateJson = JsonSerializer.Serialize(state);

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing scheduled workflow {TriggerId}", trigger.Id);
                    trigger.LastError = ex.Message;
                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WorkflowSchedulerJob");
            throw;
        }
    }

    /// <summary>
    /// Poll API endpoints and trigger workflows based on changes
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task PollApiTriggersAsync()
    {
        _logger.LogInformation("Polling API triggers");

        try
        {
            var triggers = await _context.WorkflowTriggerStates
                .Include(t => t.WorkflowDefinition)
                .Where(t => t.IsEnabled && t.TriggerType == "ApiPoll")
                .ToListAsync();

            foreach (var trigger in triggers)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<ApiPollTriggerConfig>(trigger.ConfigJson);
                    if (config == null) continue;

                    // Check if it's time to poll
                    if (trigger.LastCheckedAt.HasValue)
                    {
                        var timeSinceLastCheck = DateTime.UtcNow - trigger.LastCheckedAt.Value;
                        if (timeSinceLastCheck.TotalSeconds < config.IntervalSeconds)
                        {
                            continue; // Not yet time to poll
                        }
                    }

                    _logger.LogInformation("Polling API for workflow {WorkflowId}",
                        trigger.WorkflowDefinition.WorkflowId);

                    // Poll the API
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(config.Url);
                    var content = await response.Content.ReadAsStringAsync();

                    // Get previous state
                    var state = JsonSerializer.Deserialize<Dictionary<string, object>>(trigger.StateJson)
                                ?? new Dictionary<string, object>();

                    var previousHash = state.GetValueOrDefault("contentHash")?.ToString();
                    var currentHash = ComputeHash(content);

                    // Check if content has changed
                    if (previousHash != currentHash || config.AlwaysTrigger)
                    {
                        _logger.LogInformation("API content changed, triggering workflow {WorkflowId}",
                            trigger.WorkflowDefinition.WorkflowId);

                        // Parse response and pass as input
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
                        state["lastContent"] = content.Length > 1000 ? content.Substring(0, 1000) : content;
                        state["lastPoll"] = DateTime.UtcNow.ToString("O");
                    }

                    trigger.LastCheckedAt = DateTime.UtcNow;
                    trigger.StateJson = JsonSerializer.Serialize(state);
                    trigger.LastError = null;

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling API trigger {TriggerId}", trigger.Id);
                    trigger.LastError = ex.Message;
                    trigger.LastCheckedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PollApiTriggersAsync");
            throw;
        }
    }

    private bool ShouldRunScheduledWorkflow(
        Shared.Entities.WorkflowTriggerStateEntity trigger,
        ScheduleTriggerConfig config)
    {
        // Check if enough time has passed
        if (trigger.LastFiredAt.HasValue)
        {
            var timeSinceLastRun = DateTime.UtcNow - trigger.LastFiredAt.Value;

            return config.IntervalType.ToLower() switch
            {
                "minutes" => timeSinceLastRun.TotalMinutes >= config.IntervalValue,
                "hours" => timeSinceLastRun.TotalHours >= config.IntervalValue,
                "days" => timeSinceLastRun.TotalDays >= config.IntervalValue,
                _ => false
            };
        }

        // First run
        return true;
    }

    private string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// Configuration for schedule-based triggers
/// </summary>
public class ScheduleTriggerConfig
{
    public string IntervalType { get; set; } = "minutes"; // minutes, hours, days
    public int IntervalValue { get; set; } = 60;
    public Dictionary<string, object>? InputData { get; set; }
}

/// <summary>
/// Configuration for API polling triggers
/// </summary>
public class ApiPollTriggerConfig
{
    public string Url { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 300; // 5 minutes
    public bool AlwaysTrigger { get; set; } = false; // Trigger even if content hasn't changed
    public Dictionary<string, string>? Headers { get; set; }
}
