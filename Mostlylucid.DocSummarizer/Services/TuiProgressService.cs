using System.Collections.ObjectModel;
using Terminal.Gui;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Terminal UI progress service using Terminal.Gui for reactive console output
/// </summary>
public class TuiProgressService : IProgressReporter, IDisposable
{
    private readonly ObservableCollection<string> _llmActivities = new();
    private readonly object _llmLock = new();
    private readonly List<string> _logLines = new();
    private readonly object _logLock = new();
    private bool _autoExitOnComplete;
    private Label? _batchCountLabel;

    // Main panels
    private FrameView? _batchPanel;

    // Batch progress
    private ProgressBar? _batchProgressBar;
    private Label? _batchStatusLabel;
    private Label? _chunkProgressLabel;
    private int _completedFiles;

    // Completion tracking
    private TaskCompletionSource<bool>? _completionSource;

    // Current file
    private Label? _currentFileLabel;
    private FrameView? _currentFilePanel;
    private Label? _currentStageLabel;
    private int _failedFiles;
    private ProgressBar? _fileProgressBar;
    private bool _initialized;

    // LLM Activity
    private ListView? _llmActivityList;
    private FrameView? _llmActivityPanel;
    private FrameView? _logPanel;

    // Log
    private TextView? _logView;
    private Window? _mainWindow;
    private DateTime _startTime;
    private Toplevel? _top;

    // Stats
    private int _totalFiles;

    public void Dispose()
    {
        if (_initialized)
        {
            Application.Shutdown();
            _initialized = false;
        }
    }

    // IProgressReporter implementation

    public void ReportStage(string stage, float progress = 0)
    {
        Application.Invoke(() =>
        {
            if (_currentStageLabel != null)
                _currentStageLabel.Text = stage;
            if (_fileProgressBar != null)
                _fileProgressBar.Fraction = progress;
        });
    }

    public void ReportLlmActivity(string activity)
    {
        lock (_llmLock)
        {
            _llmActivities.Insert(0, $"{DateTime.Now:HH:mm:ss} {activity}");
            while (_llmActivities.Count > 8)
                _llmActivities.RemoveAt(_llmActivities.Count - 1);
        }

        Application.Invoke(() =>
        {
            if (_llmActivityList != null) _llmActivityList.Source = new ListWrapper<string>(_llmActivities);
        });
    }

    public void ReportLog(string message, LogLevel level = LogLevel.Info)
    {
        Log(message, level);
    }

    public void ReportChunkProgress(int completed, int total)
    {
        Application.Invoke(() =>
        {
            if (_chunkProgressLabel != null)
                _chunkProgressLabel.Text = $"Chunks: {completed} / {total}";
            if (_fileProgressBar != null && total > 0)
                _fileProgressBar.Fraction = (float)completed / total;
        });
    }

    /// <summary>
    ///     Initialize the TUI application
    /// </summary>
    public void Initialize(bool autoExitOnComplete = false)
    {
        if (_initialized) return;

        _autoExitOnComplete = autoExitOnComplete;
        Application.Init();
        _initialized = true;
        _startTime = DateTime.UtcNow;

        _top = Application.Top;

        _mainWindow = new Window
        {
            Title = "DocSummarizer - Batch Processing",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Add keyboard shortcuts
        _mainWindow.KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Q || e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        CreateLayout();

        _top.Add(_mainWindow);
    }

    private void CreateLayout()
    {
        if (_mainWindow == null) return;

        // Batch Progress Panel (top)
        _batchPanel = new FrameView
        {
            Title = "Batch Progress [Q to quit]",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 5
        };

        _batchStatusLabel = new Label
        {
            Text = "Initializing...",
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 2
        };

        _batchProgressBar = new ProgressBar
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = 1,
            Fraction = 0
        };

        _batchCountLabel = new Label
        {
            Text = "0 / 0 files (0 failed)",
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2
        };

        _batchPanel.Add(_batchStatusLabel, _batchProgressBar, _batchCountLabel);

        // Current File Panel (below batch, left side)
        _currentFilePanel = new FrameView
        {
            Title = "Current File",
            X = 0,
            Y = Pos.Bottom(_batchPanel),
            Width = Dim.Percent(50),
            Height = 7
        };

        _currentFileLabel = new Label
        {
            Text = "Waiting...",
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 2
        };

        _currentStageLabel = new Label
        {
            Text = "Idle",
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2
        };

        _fileProgressBar = new ProgressBar
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 1,
            Fraction = 0
        };

        _chunkProgressLabel = new Label
        {
            Text = "",
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2
        };

        _currentFilePanel.Add(_currentFileLabel, _currentStageLabel, _fileProgressBar, _chunkProgressLabel);

        // LLM Activity Panel (right of current file)
        _llmActivityPanel = new FrameView
        {
            Title = "LLM Activity",
            X = Pos.Right(_currentFilePanel),
            Y = Pos.Bottom(_batchPanel),
            Width = Dim.Fill(),
            Height = 7
        };

        _llmActivityList = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(_llmActivities)
        };

        _llmActivityPanel.Add(_llmActivityList);

        // Log Panel (bottom, fills remaining space)
        _logPanel = new FrameView
        {
            Title = "Log",
            X = 0,
            Y = Pos.Bottom(_currentFilePanel),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _logView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false
        };

        _logPanel.Add(_logView);

        _mainWindow.Add(_batchPanel, _currentFilePanel, _llmActivityPanel, _logPanel);
    }

    /// <summary>
    ///     Set the total number of files to process
    /// </summary>
    public void SetTotalFiles(int total)
    {
        _totalFiles = total;
        _completedFiles = 0;
        _failedFiles = 0;
        _startTime = DateTime.UtcNow;

        Application.Invoke(() =>
        {
            if (_batchStatusLabel != null)
                _batchStatusLabel.Text = $"Processing {total} files...";
            if (_batchCountLabel != null)
                _batchCountLabel.Text = $"0 / {total} files (0 failed)";
            if (_batchProgressBar != null)
                _batchProgressBar.Fraction = 0;
        });
    }

    /// <summary>
    ///     Update current file being processed
    /// </summary>
    public void SetCurrentFile(string fileName)
    {
        Application.Invoke(() =>
        {
            if (_currentFileLabel != null)
                _currentFileLabel.Text = TruncateText(fileName, 60);
            if (_currentStageLabel != null)
                _currentStageLabel.Text = "Starting...";
            if (_fileProgressBar != null)
                _fileProgressBar.Fraction = 0;
            if (_chunkProgressLabel != null)
                _chunkProgressLabel.Text = "";
        });

        Log($"Processing: {fileName}");
    }

    /// <summary>
    ///     Mark a file as completed
    /// </summary>
    public void FileCompleted(bool success, string? error = null)
    {
        if (success)
            _completedFiles++;
        else
            _failedFiles++;

        var total = _completedFiles + _failedFiles;
        var elapsed = DateTime.UtcNow - _startTime;
        var avgTime = total > 0 ? elapsed.TotalSeconds / total : 0;
        var remaining = _totalFiles - total;
        var eta = TimeSpan.FromSeconds(avgTime * remaining);

        Application.Invoke(() =>
        {
            if (_batchProgressBar != null && _totalFiles > 0)
                _batchProgressBar.Fraction = (float)total / _totalFiles;

            if (_batchCountLabel != null)
            {
                var etaText = remaining > 0 ? $" - ETA: {eta:mm\\:ss}" : "";
                _batchCountLabel.Text = $"{total} / {_totalFiles} files ({_failedFiles} failed){etaText}";
            }

            if (total == _totalFiles)
            {
                if (_batchStatusLabel != null)
                    _batchStatusLabel.Text =
                        $"Complete! {_completedFiles} succeeded, {_failedFiles} failed in {elapsed:mm\\:ss}";

                if (_autoExitOnComplete)
                    // Auto-exit after a brief delay
                    Task.Delay(2000).ContinueWith(_ => Application.Invoke(() => Application.RequestStop()));
            }
        });

        if (success)
            Log("Completed successfully", LogLevel.Success);
        else
            Log($"Failed: {error ?? "Unknown error"}", LogLevel.Error);
    }

    /// <summary>
    ///     Add a log message
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var prefix = level switch
        {
            LogLevel.Error => "[ERR]",
            LogLevel.Warning => "[WRN]",
            LogLevel.Success => "[OK] ",
            _ => "[---]"
        };

        var line = $"{DateTime.Now:HH:mm:ss} {prefix} {message}";

        lock (_logLock)
        {
            _logLines.Add(line);
            // Keep last 200 lines
            while (_logLines.Count > 200)
                _logLines.RemoveAt(0);
        }

        Application.Invoke(() =>
        {
            if (_logView != null)
            {
                _logView.Text = string.Join("\n", _logLines);
                // Scroll to bottom
                _logView.MoveEnd();
            }
        });
    }

    /// <summary>
    ///     Run the TUI with a batch processing task
    /// </summary>
    public async Task RunBatchAsync(Func<IProgressReporter, Task> batchTask,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized) Initialize(true);

        _completionSource = new TaskCompletionSource<bool>();

        // Start the batch task in background
        _ = Task.Run(async () =>
        {
            try
            {
                await batchTask(this);
                _completionSource.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Log($"Batch processing error: {ex.Message}", LogLevel.Error);
                _completionSource.TrySetException(ex);
            }
        }, cancellationToken);

        // Run the TUI event loop (blocking)
        Application.Run(_top!);

        // Wait for completion
        try
        {
            await _completionSource.Task;
        }
        catch
        {
            // Error already logged
        }
    }

    /// <summary>
    ///     Request the application to quit
    /// </summary>
    public void RequestQuit()
    {
        Application.Invoke(() => Application.RequestStop());
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }
}