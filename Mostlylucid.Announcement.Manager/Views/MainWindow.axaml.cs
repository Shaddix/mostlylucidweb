using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using Mostlylucid.Announcement.Manager.ViewModels;

namespace Mostlylucid.Announcement.Manager.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Set up the text editor binding manually (AvaloniaEdit doesn't support direct binding)
        if (MarkdownEditor != null && ViewModel != null)
        {
            MarkdownEditor.Text = ViewModel.EditorMarkdown;

            MarkdownEditor.TextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.EditorMarkdown = MarkdownEditor.Text;
                }
            };

            // Update editor when ViewModel property changes
            ViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.EditorMarkdown))
                {
                    if (MarkdownEditor.Text != ViewModel.EditorMarkdown)
                    {
                        MarkdownEditor.Text = ViewModel.EditorMarkdown;
                    }
                }
            };
        }

        // Load announcements on startup
        if (ViewModel != null)
        {
            await ViewModel.LoadAnnouncementsCommand.ExecuteAsync(null);
        }
    }

    private async void UploadImage_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.svg" }
                }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            try
            {
                ViewModel.StatusMessage = "Uploading image...";
                var localPath = file.Path.LocalPath;
                var imageUrl = await App.ApiClient.UploadImageAsync(localPath);

                // Insert markdown image at cursor
                var imageName = Path.GetFileName(localPath);
                var markdownImage = $"![{imageName}]({imageUrl})";

                if (MarkdownEditor != null)
                {
                    var caretOffset = MarkdownEditor.CaretOffset;
                    MarkdownEditor.Document.Insert(caretOffset, markdownImage);
                    MarkdownEditor.CaretOffset = caretOffset + markdownImage.Length;
                }

                ViewModel.StatusMessage = "Image uploaded and inserted";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Image upload failed: {ex.Message}";
            }
        }
    }

    private void InsertLink_Click(object? sender, RoutedEventArgs e)
    {
        if (MarkdownEditor == null) return;

        var selectedText = MarkdownEditor.SelectedText;
        var linkText = string.IsNullOrEmpty(selectedText) ? "link text" : selectedText;
        var markdown = $"[{linkText}](https://example.com)";

        var caretOffset = MarkdownEditor.CaretOffset;
        if (!string.IsNullOrEmpty(selectedText))
        {
            MarkdownEditor.Document.Replace(MarkdownEditor.SelectionStart, MarkdownEditor.SelectionLength, markdown);
        }
        else
        {
            MarkdownEditor.Document.Insert(caretOffset, markdown);
        }
    }
}
