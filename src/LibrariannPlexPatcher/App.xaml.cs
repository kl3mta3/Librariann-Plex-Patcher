using System.Windows;
using LibrariannPlexPatcher.Services;

namespace LibrariannPlexPatcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // No tray icon, no background service by design - this either runs headless as the elevated
        // file-writer (see ElevatedActionRunner) or shows the normal window. Never both, never neither.
        if (e.Args.Length >= 2 && e.Args[0] == "--elevated-action")
        {
            ElevatedActionExecutor.RunAndExit(e.Args[1]);
            return; // unreachable in practice - RunAndExit always calls Environment.Exit
        }

        new MainWindow().Show();
    }
}
