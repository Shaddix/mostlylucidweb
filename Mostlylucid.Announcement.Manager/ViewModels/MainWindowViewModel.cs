using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Announcement.Manager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AnnouncementDto> _announcements = new();

    [ObservableProperty]
    private AnnouncementDto? _selectedAnnouncement;

    [ObservableProperty]
    private string _editorKey = string.Empty;

    [ObservableProperty]
    private string _editorMarkdown = string.Empty;

    [ObservableProperty]
    private string _editorLanguage = "en";

    [ObservableProperty]
    private bool _editorIsActive = true;

    [ObservableProperty]
    private int _editorPriority = 0;

    [ObservableProperty]
    private DateTimeOffset? _editorStartDate;

    [ObservableProperty]
    private DateTimeOffset? _editorEndDate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _apiToken = string.Empty;

    [ObservableProperty]
    private bool _showSettings;

    public MainWindowViewModel()
    {
        BaseUrl = App.Settings.Settings.BaseUrl;
        ApiToken = App.Settings.Settings.ApiToken;
    }

    partial void OnSelectedAnnouncementChanged(AnnouncementDto? value)
    {
        if (value != null)
        {
            EditorKey = value.Key;
            EditorMarkdown = value.Markdown;
            EditorLanguage = value.Language;
            EditorIsActive = value.IsActive;
            EditorPriority = value.Priority;
            EditorStartDate = value.StartDate;
            EditorEndDate = value.EndDate;
        }
    }

    [RelayCommand]
    private async Task LoadAnnouncementsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading announcements...";

            var announcements = await App.ApiClient.GetAllAnnouncementsAsync();
            Announcements.Clear();
            foreach (var a in announcements)
            {
                Announcements.Add(a);
            }

            IsConnected = true;
            StatusMessage = $"Loaded {announcements.Count} announcements";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NewAnnouncement()
    {
        SelectedAnnouncement = null;
        EditorKey = $"announcement-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        EditorMarkdown = "**New Announcement**\n\nEnter your announcement text here...";
        EditorLanguage = "en";
        EditorIsActive = true;
        EditorPriority = 0;
        EditorStartDate = null;
        EditorEndDate = null;
    }

    [RelayCommand]
    private async Task SaveAnnouncementAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorKey))
        {
            StatusMessage = "Error: Key is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorMarkdown))
        {
            StatusMessage = "Error: Content is required";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Saving announcement...";

            var request = new CreateAnnouncementRequest
            {
                Key = EditorKey,
                Markdown = EditorMarkdown,
                Language = EditorLanguage,
                IsActive = EditorIsActive,
                Priority = EditorPriority,
                StartDate = EditorStartDate,
                EndDate = EditorEndDate
            };

            var result = await App.ApiClient.UpsertAnnouncementAsync(request);
            StatusMessage = $"Saved announcement '{result.Key}'";

            // Refresh list
            await LoadAnnouncementsAsync();

            // Select the saved item
            SelectedAnnouncement = Announcements.FirstOrDefault(a => a.Key == result.Key && a.Language == result.Language);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAnnouncementAsync()
    {
        if (SelectedAnnouncement == null)
        {
            StatusMessage = "No announcement selected";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Deleting announcement...";

            var success = await App.ApiClient.DeleteAnnouncementAsync(SelectedAnnouncement.Key, SelectedAnnouncement.Language);
            if (success)
            {
                StatusMessage = "Announcement deleted";
                await LoadAnnouncementsAsync();
                NewAnnouncement();
            }
            else
            {
                StatusMessage = "Failed to delete announcement";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync()
    {
        if (SelectedAnnouncement == null) return;

        EditorIsActive = !EditorIsActive;
        await SaveAnnouncementAsync();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        App.Settings.Settings.BaseUrl = BaseUrl;
        App.Settings.Settings.ApiToken = ApiToken;
        App.Settings.Save();

        App.ApiClient.BaseUrl = BaseUrl;
        App.ApiClient.ApiToken = ApiToken;

        ShowSettings = false;
        StatusMessage = "Settings saved";
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    public static string[] Languages => new[] { "en", "es", "fr", "de", "it", "zh", "nl", "hi", "ar", "uk", "fi", "sv", "el", "ms" };
}
