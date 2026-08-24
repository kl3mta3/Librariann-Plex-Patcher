using System.IO;
using System.Reflection;
using System.Text.Json;
using LibrariannPlexPatcher.Models;

namespace LibrariannPlexPatcher.Services;

/// <summary>
/// Runs entirely inside the elevated relaunch (see ElevatedActionRunner) - no window, reads its args
/// from a temp file, does the actual privileged file writes, reports the outcome to another temp file,
/// and exits. Never runs in the normal, unelevated app instance.
/// </summary>
public static class ElevatedActionExecutor
{
    public static void RunAndExit(string argsPath)
    {
        var result = new ElevatedActionResult();
        string? resultPath = null;
        try
        {
            var argsJson = File.ReadAllText(argsPath);
            var args = JsonSerializer.Deserialize<ElevatedActionArgs>(argsJson)
                ?? throw new InvalidOperationException("Could not parse action arguments.");
            resultPath = args.ResultFilePath;

            switch (args.Action)
            {
                case "patch":
                    Patch(args, result);
                    break;
                case "restore":
                    Restore(args, result);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown action '{args.Action}'.");
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        finally
        {
            if (resultPath != null)
            {
                try { File.WriteAllText(resultPath, JsonSerializer.Serialize(result)); }
                catch (IOException) { /* if we can't even write the result, there's nothing further to do */ }
            }
        }

        Environment.Exit(result.Success ? 0 : 1);
    }

    private static void Patch(ElevatedActionArgs args, ElevatedActionResult result)
    {
        var currentContent = File.ReadAllText(args.IndexHtmlPath);
        var alreadyPatched = PatchScriptBuilder.IsAlreadyPatched(currentContent);

        // Only ever back up a genuinely pristine file - never overwrite an existing backup with
        // something we've already modified ourselves, so it always represents Plex's true original.
        var backupPath = args.ExistingBackupPath;
        if (!alreadyPatched)
        {
            Directory.CreateDirectory(args.BackupFolder);
            backupPath = Path.Combine(args.BackupFolder, $"index.html.{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak");
            File.Copy(args.IndexHtmlPath, backupPath, overwrite: true);
        }

        var (newContent, _) = PatchScriptBuilder.Apply(currentContent, args.LibrariannUrl);
        File.WriteAllText(args.IndexHtmlPath, newContent);

        DeployStaticAssets(args.StaticFolderPath);

        result.BackupPath = backupPath;
        result.TimestampUtc = DateTime.UtcNow.ToString("O");
    }

    private static void Restore(ElevatedActionArgs args, ElevatedActionResult result)
    {
        if (string.IsNullOrWhiteSpace(args.ExistingBackupPath) || !File.Exists(args.ExistingBackupPath))
            throw new InvalidOperationException("No backup file was found to restore from.");

        File.Copy(args.ExistingBackupPath, args.IndexHtmlPath, overwrite: true);
        result.BackupPath = args.ExistingBackupPath;
        result.TimestampUtc = DateTime.UtcNow.ToString("O");
    }

    private static void DeployStaticAssets(string staticFolderPath)
    {
        Directory.CreateDirectory(staticFolderPath);
        CopyEmbeddedAsset("logo-white-64.png", staticFolderPath);
        CopyEmbeddedAsset("logo-gold-64.png", staticFolderPath);
    }

    private static void CopyEmbeddedAsset(string fileName, string destinationFolder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"LibrariannPlexPatcher.Assets.static.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found.");
        using var fileStream = File.Create(Path.Combine(destinationFolder, fileName));
        stream.CopyTo(fileStream);
    }
}
