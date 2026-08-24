using System.IO;
using System.Text.Json;
using LibrariannPlexPatcher.Models;

namespace LibrariannPlexPatcher.Services;

public static class SettingsStore
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibrariannPlexPatcher");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    public static string DefaultBackupFolder => Path.Combine(SettingsFolder, "backups");

    public static PatcherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new PatcherSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<PatcherSettings>(json) ?? new PatcherSettings();
        }
        catch
        {
            // A corrupt or unreadable settings file shouldn't stop the app from launching - just start
            // fresh, same as a first run.
            return new PatcherSettings();
        }
    }

    public static void Save(PatcherSettings settings)
    {
        Directory.CreateDirectory(SettingsFolder);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions {WriteIndented = true});
        File.WriteAllText(SettingsPath, json);
    }
}
