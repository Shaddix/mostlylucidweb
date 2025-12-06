using System.ComponentModel.DataAnnotations;

namespace Mostlylucid.MarkdownTranslator.Models;

public class TranslateTask(string taskId,DateTime startTime,  string language, Task<TaskCompletion>? task) 
    : TranslateResultTask(taskId, startTime, language)
{
    
    
    public Task<TaskCompletion>? Task { get; init; } = task;
}

public class TranslateResultTask
{
    
    public TranslateResultTask(string taskId, DateTime startTime, string language)
    {
        TaskId = taskId;
        StartTime = startTime;
        Language = language;
    }
    
    public TranslateResultTask(TranslateTask task, bool includeMarkdown = false)
    {
        TaskId = task.TaskId;
        StartTime = task.StartTime;
        Language = task.Language;

        // Check for faulted state first - a faulted task is also "completed" in .NET terms
        if (task.Task?.IsFaulted == true)
        {
            Failed = true;
            Completed = false;
            TotalMilliseconds = (int)(DateTime.Now - task.StartTime).TotalMilliseconds;
        }
        else if (task.Task?.IsCompletedSuccessfully == true)
        {
            Completed = true;
            Failed = false;
            var endTime = task.Task.Result.EndTime;
            TotalMilliseconds = (int)((endTime - task.StartTime)!).Value.TotalMilliseconds;
            EndTime = endTime;
        }
        else
        {
            // Still in progress
            Completed = false;
            Failed = false;
            TotalMilliseconds = (int)(DateTime.Now - task.StartTime).TotalMilliseconds;
        }

        if (Completed && includeMarkdown)
        {
            var result = task.Task?.Result;
            if (result == null) return;
            OriginalMarkdown = result.OriginalMarkdown;
            TranslatedMarkdown = result.TranslatedMarkdown;
        }
    }
    [Display(Name ="Task ID")]
    public string TaskId { get; set; }

    [Display(Name = "Original Markdown")]
    public string? OriginalMarkdown { get; set; }
    
    [Display(Name = "Translated Markdown")]
    public string? TranslatedMarkdown { get; set; }
    [Display(Name = "End Time")]
    public DateTime? EndTime { get; set; }
    [Display(Name = "Start Time")]
    public DateTime StartTime { get; set; }
    [Display(Name = "Time Taken")]
    public int TotalMilliseconds { get; set; } 
    [Display(Name = "Language")]
    public string Language { get; set; }

    [Display(Name = "Failed")]
    public bool Failed { get; set; }
    
    [Display(Name = "Status")]
    public bool Completed
    {
        get;
        set;
    }
}