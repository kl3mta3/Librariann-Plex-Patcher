using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LibrariannPlexPatcher.Models;

namespace LibrariannPlexPatcher.Services;

/// <summary>
/// Relaunches this same executable with a hidden command-line mode, elevated via the UAC "runas" verb,
/// to do the one thing that actually needs admin rights - writing into Plex's own Program Files
/// install folder. Everything else in the app (browsing, searching for index.html, testing the
/// Librariann connection) runs unelevated in the normal visible window - the UAC prompt only appears
/// at the moment Patch or Restore is actually clicked. Args and the result both cross the process
/// boundary via temp JSON files, since UseShellExecute (required for the runas verb) rules out
/// redirecting stdin/stdout directly.
/// </summary>
public static class ElevatedActionRunner
{
    public static async Task<ElevatedActionResult> RunAsync(ElevatedActionArgs args)
    {
        var argsPath = Path.Combine(Path.GetTempPath(), $"lpp-args-{Guid.NewGuid():N}.json");
        var resultPath = Path.Combine(Path.GetTempPath(), $"lpp-result-{Guid.NewGuid():N}.json");
        args.ResultFilePath = resultPath;

        await File.WriteAllTextAsync(argsPath, JsonSerializer.Serialize(args));

        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable's path.");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--elevated-action \"{argsPath}\"",
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
                return new ElevatedActionResult {Success = false, Error = "Could not start the elevated process."};

            await process.WaitForExitAsync();

            if (!File.Exists(resultPath))
                return new ElevatedActionResult {Success = false, Error = "The elevated action didn't report a result - it may have exited unexpectedly."};

            var resultJson = await File.ReadAllTextAsync(resultPath);
            return JsonSerializer.Deserialize<ElevatedActionResult>(resultJson)
                ?? new ElevatedActionResult {Success = false, Error = "Couldn't parse the elevated action's result."};
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED - the user clicked "No" on the UAC prompt.
            return new ElevatedActionResult {Success = false, Error = "Admin permission was declined."};
        }
        finally
        {
            TryDelete(argsPath);
            TryDelete(resultPath);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best-effort cleanup */ }
    }
}
