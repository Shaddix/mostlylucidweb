using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TinyLLM.Models;
using TinyLLM.Services;

namespace TinyLLM;

public partial class MainWindow : Window
{
    private readonly ModelDownloader _modelDownloader;
    private readonly RagService _ragService;
    private readonly OllamaService _ollamaService;
    private ChatService? _llamaSharpChatService;
    private OllamaChatService? _ollamaChatService;
    private readonly ObservableCollection<ChatMessage> _messages;
    private readonly ObservableCollection<ModelInfo> _availableModels;
    private bool _isProcessing;
    private CancellationTokenSource? _cancellationTokenSource;
    private ModelInfo? _currentModel;

    public MainWindow()
    {
        InitializeComponent();

        _modelDownloader = new ModelDownloader();
        _ragService = new RagService();
        _ollamaService = new OllamaService();

        _messages = new ObservableCollection<ChatMessage>();
        _availableModels = new ObservableCollection<ModelInfo>();

        ChatMessagesPanel.ItemsSource = _messages;
        ModelSelector.ItemsSource = _availableModels;

        // Add converters
        Resources.Add("RoleToStyleConverter", new RoleToStyleConverter());
        Resources.Add("RoleToDisplayConverter", new RoleToDisplayConverter());

        UpdateRagCount();

        // Load available models
        _ = LoadAvailableModelsAsync();
    }

    private async Task LoadAvailableModelsAsync()
    {
        ShowProgress("Checking for available models...");

        _availableModels.Clear();

        // Check if Ollama is available
        var ollamaAvailable = await _ollamaService.IsAvailableAsync();

        if (ollamaAvailable)
        {
            // Add Ollama models
            var ollamaModels = await _ollamaService.ListModelsAsync();
            foreach (var model in ollamaModels)
            {
                _availableModels.Add(new ModelInfo
                {
                    Name = model.Name,
                    DisplayName = $"{model.Name} (Ollama)",
                    Source = ModelSource.Ollama,
                    Path = model.Name,
                    Size = model.Size,
                    Description = $"Ollama model - {model.Size / 1024.0 / 1024.0:F1} MB"
                });
            }
        }

        // Add local GGUF files
        var localModels = _modelDownloader.GetAvailableModels();
        foreach (var modelFile in localModels)
        {
            _availableModels.Add(new ModelInfo
            {
                Name = modelFile,
                DisplayName = $"{modelFile} (Local)",
                Source = ModelSource.LocalFile,
                Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", modelFile),
                Description = "Local GGUF model"
            });
        }

        // Add default download option if no models available
        if (_availableModels.Count == 0)
        {
            _availableModels.Add(new ModelInfo
            {
                Name = "gemma-2-2b-it",
                DisplayName = "Download Gemma 2 2B (Recommended)",
                Source = ModelSource.LocalFile,
                Path = "",
                Description = "Will download ~1.7GB model"
            });
        }

        // Select first available model or Gemma if available
        var gemmaModel = _availableModels.FirstOrDefault(m => m.Name.Contains("gemma"));
        ModelSelector.SelectedItem = gemmaModel ?? _availableModels.FirstOrDefault();

        HideProgress();
    }

    private async void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is ModelInfo selectedModel)
        {
            await LoadModelAsync(selectedModel);
        }
    }

    private async Task LoadModelAsync(ModelInfo modelInfo)
    {
        if (_isProcessing)
            return;

        _currentModel = modelInfo;

        try
        {
            // Dispose existing services
            _llamaSharpChatService?.Dispose();
            _ollamaChatService?.Dispose();
            _llamaSharpChatService = null;
            _ollamaChatService = null;

            var settings = new AppSettings
            {
                UseGpu = UseGpuCheckBox.IsChecked ?? false,
                GpuLayers = 35,
                ContextSize = 2048,
                Temperature = 0.7f,
                MaxTokens = 512,
                TopRagResults = 3
            };

            if (modelInfo.Source == ModelSource.Ollama)
            {
                // Use Ollama
                ShowProgress($"Loading {modelInfo.Name} via Ollama...");

                _ollamaChatService = new OllamaChatService(_ragService);
                _ollamaChatService.OnError += OnChatServiceError;

                var success = await _ollamaChatService.InitializeAsync(modelInfo.Name, settings);

                HideProgress();

                if (success)
                {
                    ModelStatusText.Text = $"✓ {modelInfo.Name} (Ollama)";
                    AddSystemMessage($"{modelInfo.Name} is ready via Ollama!");
                }
                else
                {
                    ModelStatusText.Text = "Failed to load model";
                    AddSystemMessage("Failed to load model. Check if Ollama is running.");
                }
            }
            else
            {
                // Use LlamaSharp for local GGUF
                _cancellationTokenSource = new CancellationTokenSource();

                // Check if we need to download
                if (string.IsNullOrEmpty(modelInfo.Path) || !System.IO.File.Exists(modelInfo.Path))
                {
                    ShowProgress("Downloading model...");

                    var progress = new Progress<(long bytesReceived, long? totalBytes, double percentage)>(update =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadProgressBar.Value = update.percentage;
                            if (update.totalBytes.HasValue)
                            {
                                var mbReceived = update.bytesReceived / 1024.0 / 1024.0;
                                var mbTotal = update.totalBytes.Value / 1024.0 / 1024.0;
                                ProgressDetailText.Text = $"{mbReceived:F1} MB / {mbTotal:F1} MB";
                            }
                        });
                    });

                    modelInfo.Path = await _modelDownloader.EnsureModelDownloadedAsync(progress, _cancellationTokenSource.Token);
                }

                ShowProgress("Loading model into memory...");

                _llamaSharpChatService = new ChatService(_ragService);
                _llamaSharpChatService.OnError += OnChatServiceError;

                settings.ModelPath = modelInfo.Path;
                var success = await _llamaSharpChatService.InitializeAsync(modelInfo.Path, settings);

                HideProgress();

                if (success)
                {
                    ModelStatusText.Text = $"✓ {modelInfo.Name} ({(settings.UseGpu ? "GPU" : "CPU")})";
                    AddSystemMessage($"{modelInfo.Name} is ready with RAG memory!");
                }
                else
                {
                    ModelStatusText.Text = "Failed to load model";
                    AddSystemMessage("Failed to load model. Please check the logs.");
                }
            }
        }
        catch (Exception ex)
        {
            HideProgress();
            ModelStatusText.Text = "Error loading model";
            MessageBox.Show($"Failed to load model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnChatServiceError(object? sender, string error)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await LoadAvailableModelsAsync();
    }

    private void ShowProgress(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressText.Text = message;
            DownloadProgressBar.Value = 0;
            ProgressDetailText.Text = "";
        });
    }

    private void HideProgress()
    {
        Dispatcher.Invoke(() =>
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        });
    }

    private void AddSystemMessage(string content)
    {
        Dispatcher.Invoke(() =>
        {
            _messages.Add(new ChatMessage
            {
                Role = "system",
                Content = content,
                Timestamp = DateTime.Now
            });
            ScrollToBottom();
        });
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        if (_isProcessing)
            return;

        var isInitialized = _ollamaChatService?.IsInitialized ?? _llamaSharpChatService?.IsInitialized ?? false;
        if (!isInitialized)
        {
            MessageBox.Show("Please wait for the model to load first.", "Model Not Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var userMessage = UserInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(userMessage))
            return;

        _isProcessing = true;
        SendButton.IsEnabled = false;
        UserInputTextBox.Text = "";

        // Add user message
        _messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.Now
        });

        ScrollToBottom();

        // Create placeholder for assistant response
        var assistantMessage = new ChatMessage
        {
            Role = "assistant",
            Content = "",
            Timestamp = DateTime.Now
        };
        _messages.Add(assistantMessage);

        try
        {
            var useRag = UseRagCheckBox.IsChecked ?? true;
            _cancellationTokenSource = new CancellationTokenSource();

            IAsyncEnumerable<string> responseStream;

            if (_ollamaChatService != null)
            {
                responseStream = _ollamaChatService.ChatAsync(userMessage, useRag, _cancellationTokenSource.Token);
            }
            else if (_llamaSharpChatService != null)
            {
                responseStream = _llamaSharpChatService.ChatAsync(userMessage, useRag);
            }
            else
            {
                throw new InvalidOperationException("No chat service available");
            }

            await foreach (var token in responseStream)
            {
                assistantMessage.Content += token;

                // Force UI update
                Dispatcher.Invoke(() =>
                {
                    var index = _messages.IndexOf(assistantMessage);
                    if (index >= 0)
                    {
                        _messages[index] = new ChatMessage
                        {
                            Role = assistantMessage.Role,
                            Content = assistantMessage.Content,
                            Timestamp = assistantMessage.Timestamp
                        };
                    }
                    ScrollToBottom();
                });

                // Small delay for smooth streaming
                await Task.Delay(10);
            }

            UpdateRagCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isProcessing = false;
            SendButton.IsEnabled = true;
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        ChatScrollViewer.ScrollToEnd();
    }

    private void UpdateRagCount()
    {
        RagCountText.Text = _ragService.GetEntryCount().ToString();
    }

    private void ClearRag_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the RAG database? This will remove all conversation history.",
            "Clear RAG Database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _ragService.ClearDatabase();
            UpdateRagCount();
            AddSystemMessage("RAG database cleared.");
        }
    }

    private async void UseGpuCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Only relevant for LlamaSharp local models
        if (_currentModel?.Source == ModelSource.LocalFile && _llamaSharpChatService?.IsInitialized == true)
        {
            var result = MessageBox.Show(
                "Changing GPU settings requires reloading the model. Do you want to continue?",
                "Reload Model",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes && _currentModel != null)
            {
                _llamaSharpChatService.Dispose();
                _messages.Clear();
                await LoadModelAsync(_currentModel);
            }
            else
            {
                // Revert checkbox state
                UseGpuCheckBox.IsChecked = !UseGpuCheckBox.IsChecked;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _llamaSharpChatService?.Dispose();
        _ollamaChatService?.Dispose();
        base.OnClosed(e);
    }
}

// Converters for XAML
public class RoleToStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = value as string;
        return role == "user" ? "MessageBubbleUser" : "MessageBubbleAssistant";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RoleToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = value as string;
        return role switch
        {
            "user" => "You",
            "assistant" => "TinyLLM",
            "system" => "System",
            _ => role ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
