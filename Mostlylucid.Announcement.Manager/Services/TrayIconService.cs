using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Mostlylucid.Announcement.Manager.Services;

public class TrayIconService : IDisposable
{
    private NativeMenu? _trayMenu;
    private TrayIcon? _trayIcon;
    private readonly Window _mainWindow;
    private bool _isDisposed;

    public TrayIconService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Initialize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        _trayMenu = new NativeMenu();

        var showItem = new NativeMenuItem("Show");
        showItem.Click += (s, e) => ShowWindow();
        _trayMenu.Add(showItem);

        var hideItem = new NativeMenuItem("Hide");
        hideItem.Click += (s, e) => HideWindow();
        _trayMenu.Add(hideItem);

        _trayMenu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();
        _trayMenu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Announcement Manager",
            Menu = _trayMenu,
            IsVisible = true
        };

        _trayIcon.Clicked += (s, e) => ToggleWindow();

        // Handle window close to minimize to tray instead
        _mainWindow.Closing += (s, e) =>
        {
            if (!_isDisposed)
            {
                e.Cancel = true;
                HideWindow();
            }
        };
    }

    private void ShowWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    private void HideWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _mainWindow.Hide();
        });
    }

    private void ToggleWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow.IsVisible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        });
    }

    private void ExitApplication()
    {
        _isDisposed = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    public void ShowNotification(string title, string message)
    {
        // Avalonia doesn't have built-in notifications, but we can show a balloon tip via the tray icon
        // For now, just log it - in production you'd use platform-specific APIs
        Console.WriteLine($"[Notification] {title}: {message}");
    }

    public void Dispose()
    {
        _isDisposed = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
