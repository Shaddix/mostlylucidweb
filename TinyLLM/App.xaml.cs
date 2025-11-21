using System.Windows;

namespace TinyLLM;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(
                $"An unexpected error occurred:\n{ex?.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }
}
