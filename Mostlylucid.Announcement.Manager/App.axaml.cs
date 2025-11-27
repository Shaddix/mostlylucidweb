using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mostlylucid.Announcement.Manager.Services;
using Mostlylucid.Announcement.Manager.ViewModels;
using Mostlylucid.Announcement.Manager.Views;

namespace Mostlylucid.Announcement.Manager;

public partial class App : Application
{
    public static SettingsService Settings { get; } = new();
    public static AnnouncementApiClient ApiClient { get; } = new();
    private TrayIconService? _trayIconService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Load settings
        Settings.Load();
        ApiClient.BaseUrl = Settings.Settings.BaseUrl;
        ApiClient.ApiToken = Settings.Settings.ApiToken;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow = mainWindow;

            // Initialize tray icon service
            _trayIconService = new TrayIconService(mainWindow);
            _trayIconService.Initialize();

            // Clean up on shutdown
            desktop.ShutdownRequested += (s, e) =>
            {
                _trayIconService?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
