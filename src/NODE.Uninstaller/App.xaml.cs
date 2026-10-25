using System.Windows;

namespace NODE.Uninstaller;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"NODE Uninstaller encountered an unexpected error.\n\n{args.Exception.Message}",
                "NODE Uninstaller", MessageBoxButton.OK, MessageBoxImage.Error);
            // Unexpected UI exceptions should not be silently suppressed.
            args.Handled = false;
        };
    }
}
