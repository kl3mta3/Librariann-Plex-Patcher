using System;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LibrariannPlexPatcher.Services;

namespace LibrariannPlexPatcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // No dialog, no exception, just a blank painted window is the classic signature of WPF's
        // hardware/DirectX rendering pipeline having nothing usable to render onto (common on a bare
        // VM, Server Core, or an RDP session without graphics acceleration) - the native window frame
        // still draws fine either way since that's plain Win32/DWM chrome, not WPF's own renderer, and
        // that failure mode doesn't surface as a catchable .NET exception at all. Forcing pure software
        // rendering, set before anything else initializes, sidesteps the hardware pipeline entirely -
        // negligible cost for a small settings-form UI like this one.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        base.OnStartup(e);

        // Without these, a startup exception on a machine we can't attach a debugger to (e.g. a clean
        // machine with no .NET pre-installed, testing the self-contained publish) just leaves a blank
        // native window - Windows draws the frame regardless, but WPF never gets to paint content if
        // construction throws. Surfacing the real exception beats guessing at a fix blind.
        DispatcherUnhandledException += (_, args) =>
        {
            ShowFatalError(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) ShowFatalError(ex);
        };

        // No tray icon, no background service by design - this either runs headless as the elevated
        // file-writer (see ElevatedActionRunner) or shows the normal window. Never both, never neither.
        if (e.Args.Length >= 2 && e.Args[0] == "--elevated-action")
        {
            ElevatedActionExecutor.RunAndExit(e.Args[1]);
            return; // unreachable in practice - RunAndExit always calls Environment.Exit
        }

        try
        {
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
            Shutdown(1);
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Librariann Plex Patcher hit an error it couldn't recover from:");
        sb.AppendLine();
        var current = ex;
        while (current != null)
        {
            sb.AppendLine(current.GetType().FullName + ": " + current.Message);
            current = current.InnerException;
        }
        sb.AppendLine();
        sb.AppendLine(ex.StackTrace);

        MessageBox.Show(sb.ToString(), "Librariann Plex Patcher - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
