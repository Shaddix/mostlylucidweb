using System.Windows;

namespace Mostlylucid.Chat.TrayApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hide main window on startup - we only want the tray icon
        MainWindow.WindowState = WindowState.Minimized;
        MainWindow.ShowInTaskbar = false;
    }
}
